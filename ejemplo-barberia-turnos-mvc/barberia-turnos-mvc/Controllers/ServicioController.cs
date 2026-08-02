using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Authorization;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Dueño")]
    public class ServicioController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;

        public ServicioController(BarberiaDbContext context, ICurrentBarberiaService currentBarberia)
        {
            _context = context;
            _currentBarberia = currentBarberia;
        }

        public async Task<IActionResult> Index()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var servicios = await _context.Servicios
                .Include(s => s.Barberia)
                .Where(s => s.BarberiaId == barberiaId)
                .ToListAsync();
            return View(servicios);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var servicio = await _context.Servicios
                .Include(s => s.Barberia)
                .FirstOrDefaultAsync(m => m.Id == id && m.BarberiaId == barberiaId);

            if (servicio == null) return NotFound();
            return View(servicio);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Precio,DuracionMinutos")] Servicio servicio)
        {
            servicio.BarberiaId = _currentBarberia.GetRequerida().Id;

            if (ModelState.IsValid)
            {
                _context.Add(servicio);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(servicio);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.Id == id && s.BarberiaId == barberiaId);
            if (servicio == null) return NotFound();
            return View(servicio);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Precio,DuracionMinutos,BarberiaId")] Servicio servicio)
        {
            if (id != servicio.Id) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var perteneceABarberia = await _context.Servicios
                .AsNoTracking()
                .AnyAsync(s => s.Id == id && s.BarberiaId == barberiaId);
            if (!perteneceABarberia) return NotFound();

            servicio.BarberiaId = barberiaId;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(servicio);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServicioExists(servicio.Id, barberiaId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(servicio);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var servicio = await _context.Servicios
                .Include(s => s.Barberia)
                .FirstOrDefaultAsync(m => m.Id == id && m.BarberiaId == barberiaId);

            if (servicio == null) return NotFound();
            return View(servicio);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.Id == id && s.BarberiaId == barberiaId);
            if (servicio != null)
            {
                try
                {
                    _context.Servicios.Remove(servicio);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    var servicioConBarberia = await _context.Servicios
                        .Include(s => s.Barberia)
                        .FirstOrDefaultAsync(s => s.Id == id);

                    ModelState.AddModelError("", "No se puede eliminar este servicio porque tiene turnos asociados.");
                    return View("Delete", servicioConBarberia);
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ServicioExists(int id, int barberiaId)
        {
            return _context.Servicios.Any(e => e.Id == id && e.BarberiaId == barberiaId);
        }
    }
}
