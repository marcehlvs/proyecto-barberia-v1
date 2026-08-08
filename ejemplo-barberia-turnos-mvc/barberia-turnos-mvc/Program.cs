using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Filters;
using barberia_turnos_mvc.Helpers;
using barberia_turnos_mvc.Services;
using barberia_turnos_mvc.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Globalization;

// Fuerza la cultura Argentina SIEMPRE, sin importar la configuración
// regional del servidor (que en Azure suele venir en inglés/EE.UU. por
// default). Sin esto, el mismo formulario de precio puede funcionar
// local y fallar (o interpretar mal) en Azure.
var culturaArgentina = new CultureInfo("es-AR");
CultureInfo.DefaultThreadCurrentCulture = culturaArgentina;
CultureInfo.DefaultThreadCurrentUICulture = culturaArgentina;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<RequiereBarberiaPropiaFilter>();
builder.Services.AddControllersWithViews(options =>
{
    // Global: corre en TODOS los controllers, actuales y futuros. Ver
    // comentario en RequiereBarberiaPropiaFilter.cs para el detalle de qué
    // problema de seguridad resuelve.
    options.Filters.Add<RequiereBarberiaPropiaFilter>();

    // Con <Nullable>enable</Nullable> activo, MVC trata implícitamente
    // como obligatorio cualquier propiedad "string" (no "string?"), sin
    // que haga falta poner [Required]. Eso generó un bug real: Barberia.
    // Direccion y Telefono son "string" pero NO son obligatorios en el
    // registro (RegisterDueno), así que una barbería que arrancó sin
    // cargar dirección queda con el campo vacío en la base — y después,
    // en Configuración, el navegador bloquea el guardado en silencio
    // porque considera ese campo "requerido e inválido", sin mostrar
    // ningún error (Direccion no tiene <span asp-validation-for> en la
    // vista). Lo desactivamos acá para que la obligatoriedad de cada
    // campo dependa solo de los [Required] explícitos que ya se usan en
    // todo el resto del proyecto (Servicio.Nombre, Cliente.Nombre, etc.).
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

builder.Services.AddDbContext<BarberiaDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("BarberiaConnection"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

builder.Services.AddScoped<TurnoValidacionService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<CurrentBarberiaService>();
builder.Services.AddScoped<ICurrentBarberiaService>(sp => sp.GetRequiredService<CurrentBarberiaService>());
builder.Services.AddHttpClient();
builder.Services.AddScoped<IMercadoPagoTokenService, MercadoPagoTokenService>();
builder.Services.AddHostedService<RecordatorioTurnoService>();

// Rate limiting: Login y Register son los blancos típicos de fuerza bruta
// y de bots creando cuentas en cadena. Particionamos por IP: cada IP tiene
// su propia ventana, así que un usuario real nunca se ve afectado por lo
// que hagan otras IPs, pero un script probando contraseñas o emails desde
// la misma IP sí agota su cupo.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("LoginPolicy", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("RegisterPolicy", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});


builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<BarberiaDbContext>()
    .AddErrorDescriber<SpanishIdentityErrorDescriber>();
builder.Services.AddRazorPages();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Home/AccesoDenegado";
});

// Ya NO seteamos un AccessToken global acá: desde que cada barbería conecta
// su propia cuenta de Mercado Pago (ver MercadoPagoConnectController), cada
// llamada a la API de MP pasa su propio token explícitamente vía
// RequestOptions (en PagosController). Un AccessToken global sería el token
// de TU cuenta, y usarlo por accidente en vez del de la barbería
// correspondiente cobraría la seña a nombre tuyo, no del dueño real.

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<BarberiaDbContext>();

    // Aplica las migraciones pendientes SIEMPRE (dev y producción).
    // EnsureCreated() y las migraciones son mutuamente excluyentes:
    // en Azure SQL necesitamos que el historial de migraciones quede
    // registrado en la base para poder seguir evolucionando el esquema.
    context.Database.Migrate();

    // Los roles son necesarios en cualquier ambiente para que Identity
    // funcione (los chequeos de [Authorize(Roles = "Dueño")] dependen
    // de que existan), así que esto corre siempre.
    await DbSeeder.SeedRoles(services);

    // Los datos de EJEMPLO (barbería "El Corte", clientes/turnos de
    // prueba, y el usuario Dueño con contraseña conocida) solo tienen
    // sentido en desarrollo. Sembrar esto en producción dejaría una
    // cuenta con contraseña pública (visible en el historial de git)
    // funcionando contra datos reales.
    if (app.Environment.IsDevelopment())
    {
        DbSeeder.SeedDatosDeEjemplo(context);
        await DbSeeder.SeedDuenoDeEjemplo(services);
    }
}



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

app.UseHttpsRedirection();

app.UseRouting();

app.UseMiddleware<CurrentBarberiaMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();

// Ruta raíz (sin slug): mientras exista una sola barbería, redirigimos directo a ella.
// TODO: cuando haya más de una barbería, reemplazar esto por una landing real
// (marketing del SaaS, o un selector de barberías).
app.MapGet("/", async (BarberiaDbContext db) =>
{
    var primeraBarberia = await db.Barberias.OrderBy(b => b.Id).FirstOrDefaultAsync();
    return primeraBarberia is null
        ? Results.NotFound("Todavía no hay ninguna barbería cargada.")
        : Results.Redirect($"/{primeraBarberia.Slug}");
});

// Home/Error y Home/AccesoDenegado ya no se registran acá: son rutas fijas
// con [Route] puesto directamente en HomeController, para que nunca compitan
// con la generación de links normales de Home/Index (que sí necesitan el
// slug de la barbería en el path). Antes, una ruta convencional genérica
// "Home/{action}/{statusCode?}" registrada acá matcheaba CUALQUIER acción
// de Home (incluyendo Index), y por estar registrada antes que "barberia"
// terminaba generando links tipo /Home/Index?barberiaSlug=elcorte en vez
// de /elcorte/Home/Index, rompiendo CurrentBarberiaMiddleware.
app.MapControllerRoute(
    name: "barberia",
    pattern: "{barberiaSlug}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

app.Run();