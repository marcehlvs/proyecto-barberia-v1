using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Dueño")]
    public class BarberiaController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;
        private readonly IMercadoPagoTokenService _mpTokenService;

        public BarberiaController(BarberiaDbContext context, ICurrentBarberiaService currentBarberia, IMercadoPagoTokenService mpTokenService)
        {
            _context = context;
            _currentBarberia = currentBarberia;
            _mpTokenService = mpTokenService;
        }

        public async Task<IActionResult> Configuracion()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var barberia = await _context.Barberias.FirstOrDefaultAsync(b => b.Id == barberiaId);
            if (barberia == null) return NotFound();

            if (barberia.TieneMercadoPagoConectado)
            {
                ViewData["MpCuentaConectada"] = await _mpTokenService.ObtenerInfoCuentaConectadaAsync(barberia);
            }

            return View(barberia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Configuracion([Bind("Id,Nombre,Direccion,Telefono,PorcentajeSeña,MinutosEntreTurnos,HoraApertura,HoraCierre")] Barberia barberia)
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;

            // Que el Dueño solo pueda editar SU PROPIA barbería, nunca otra por ID a mano.
            if (barberia.Id != barberiaId)
            {
                return Forbid();
            }

            // El Slug no se toca desde este formulario (no está en el Bind),
            // así que lo tenemos que reasignar antes de guardar o EF lo pisaría con vacío.
            barberia.Slug = (await _context.Barberias.AsNoTracking().FirstOrDefaultAsync(b => b.Id == barberiaId))?.Slug ?? barberia.Slug;

            if (ModelState.IsValid)
            {
                _context.Update(barberia);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Configuracion));
            }
            return View(barberia);
        }
    }
}
