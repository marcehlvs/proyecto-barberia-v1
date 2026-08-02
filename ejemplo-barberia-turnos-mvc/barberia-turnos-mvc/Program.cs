using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Helpers;
using barberia_turnos_mvc.Services;
using barberia_turnos_mvc.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MercadoPago.Config;
using Microsoft.AspNetCore.Identity.UI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<BarberiaDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("BarberiaConnection")));
builder.Services.AddScoped<TurnoValidacionService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<CurrentBarberiaService>();
builder.Services.AddScoped<ICurrentBarberiaService>(sp => sp.GetRequiredService<CurrentBarberiaService>());


builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
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

MercadoPagoConfig.AccessToken = builder.Configuration["MercadoPago:AccessToken"];

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<BarberiaDbContext>();

    DbSeeder.Seed(context);

    await DbSeeder.SeedRolesYUsuarios(services);
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
