using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Services;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Middleware
{
    // Debe registrarse DESPUÉS de app.UseRouting() y ANTES de app.UseAuthorization(),
    // porque necesita que el routing ya haya resuelto los valores de la ruta (RouteValues),
    // pero tiene que correr antes de que el controller/acción se ejecute.
    public class CurrentBarberiaMiddleware
    {
        private readonly RequestDelegate _next;

        public CurrentBarberiaMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, BarberiaDbContext db, CurrentBarberiaService currentBarberia)
        {
            // 1) Prioridad: segmento de ruta de los controllers MVC (/elcorte/Turno/Index)
            var slug = context.GetRouteValue("barberiaSlug") as string;

            // 2) Fallback: query string, para páginas de Identity que no comparten
            //    el mismo patrón de rutas (/Identity/Account/Register?barberia=elcorte)
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = context.Request.Query["barberia"].ToString();
            }

            if (!string.IsNullOrWhiteSpace(slug))
            {
                var barberia = await db.Barberias
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Slug == slug.ToLower());

                if (barberia is null)
                {
                    // Slug no existe: 404 directo, no seguimos ejecutando el resto del pipeline.
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsync("Barbería no encontrada.");
                    return;
                }

                currentBarberia.Establecer(barberia);
            }

            await _next(context);
        }
    }
}
