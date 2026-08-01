using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Dueño")]
    public class BarberiaController : Controller
    {
        private readonly BarberiaDbContext _context;

        public BarberiaController(BarberiaDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Configuracion()
        {
            var barberia = await _context.Barberias.FirstOrDefaultAsync();
            if (barberia == null) return NotFound();
            return View(barberia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Configuracion([Bind("Id,Nombre,Direccion,Telefono,PorcentajeSeña,MinutosEntreTurnos,HoraApertura,HoraCierre")] Barberia barberia)
        {
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