using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace barberia_turnos_mvc.Filters
{
    // Filtro de autorización GLOBAL (se registra una sola vez en Program.cs y
    // corre en TODAS las requests, incluidas las de controllers que se
    // agreguen después). Cierra el agujero de seguridad cross-tenant: hasta
    // ahora, [Authorize(Roles = "Dueño")] solo verificaba que el usuario
    // TENGA el rol Dueño, sin chequear que sea dueño de LA BARBERÍA que está
    // pisando (resuelta por el slug de la URL vía CurrentBarberiaMiddleware).
    // Sin este filtro, el dueño de "elcorte" podía escribir a mano
    // "/barberia-test/Turno/Index" y operar sobre datos de otra barbería.
    //
    // No reemplaza a [Authorize(Roles = "Dueño")]: ese sigue haciendo falta
    // para bloquear a usuarios sin el rol. Este filtro agrega la segunda
    // capa: "sí sos Dueño, pero ¿sos el dueño de ESTA barbería puntual?".
    public class RequiereBarberiaPropiaFilter : IAsyncAuthorizationFilter
    {
        private readonly ICurrentBarberiaService _currentBarberia;
        private readonly UserManager<ApplicationUser> _userManager;

        public RequiereBarberiaPropiaFilter(ICurrentBarberiaService currentBarberia, UserManager<ApplicationUser> userManager)
        {
            _currentBarberia = currentBarberia;
            _userManager = userManager;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // No autenticado, o no tiene rol Dueño: no es responsabilidad de
            // este filtro (eso lo maneja [Authorize] normalmente).
            if (user.Identity?.IsAuthenticated != true) return;
            if (!user.IsInRole("Dueño")) return;

            // Si la request no tiene una barbería resuelta por slug (ej: una
            // página que no vive bajo la ruta {barberiaSlug}/...), no hay
            // nada que comparar acá.
            var barberiaActual = _currentBarberia.Barberia;
            if (barberiaActual == null) return;

            var appUser = await _userManager.GetUserAsync(user);

            if (appUser?.BarberiaId != barberiaActual.Id)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
