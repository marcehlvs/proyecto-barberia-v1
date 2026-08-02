using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Dueño")]
    public class BloqueoHorarioController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;

        public BloqueoHorarioController(BarberiaDbContext context, ICurrentBarberiaService currentBarberia)
        {
            _context = context;
            _currentBarberia = currentBarberia;
        }

        public async Task<IActionResult> Index()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var bloqueos = await _context.BloqueoHorarios
                .Include(b => b.Barberia)
                .Where(b => b.BarberiaId == barberiaId)
                .OrderBy(b => b.FechaInicio)
                .ToListAsync();
            return View(bloqueos);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var bloqueo = await _context.BloqueoHorarios
                .Include(b => b.Barberia)
                .FirstOrDefaultAsync(m => m.Id == id && m.BarberiaId == barberiaId);

            if (bloqueo == null) return NotFound();
            return View(bloqueo);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FechaInicio,FechaFin,Motivo")] BloqueoHorario bloqueo)
        {
            if (bloqueo.FechaFin <= bloqueo.FechaInicio)
            {
                ModelState.AddModelError("", "La fecha de fin debe ser posterior a la fecha de inicio.");
            }

            bloqueo.BarberiaId = _currentBarberia.GetRequerida().Id;

            if (ModelState.IsValid)
            {
                _context.Add(bloqueo);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(bloqueo);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var bloqueo = await _context.BloqueoHorarios.FirstOrDefaultAsync(b => b.Id == id && b.BarberiaId == barberiaId);
            if (bloqueo == null) return NotFound();
            return View(bloqueo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FechaInicio,FechaFin,Motivo,BarberiaId")] BloqueoHorario bloqueo)
        {
            if (id != bloqueo.Id) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var perteneceABarberia = await _context.BloqueoHorarios
                .AsNoTracking()
                .AnyAsync(b => b.Id == id && b.BarberiaId == barberiaId);
            if (!perteneceABarberia) return NotFound();

            if (bloqueo.FechaFin <= bloqueo.FechaInicio)
            {
                ModelState.AddModelError("", "La fecha de fin debe ser posterior a la fecha de inicio.");
            }

            bloqueo.BarberiaId = barberiaId;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bloqueo);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BloqueoExists(bloqueo.Id, barberiaId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(bloqueo);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var bloqueo = await _context.BloqueoHorarios
                .Include(b => b.Barberia)
                .FirstOrDefaultAsync(m => m.Id == id && m.BarberiaId == barberiaId);

            if (bloqueo == null) return NotFound();
            return View(bloqueo);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var bloqueo = await _context.BloqueoHorarios.FirstOrDefaultAsync(b => b.Id == id && b.BarberiaId == barberiaId);
            if (bloqueo != null)
            {
                _context.BloqueoHorarios.Remove(bloqueo);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool BloqueoExists(int id, int barberiaId)
        {
            return _context.BloqueoHorarios.Any(e => e.Id == id && e.BarberiaId == barberiaId);
        }
    }
}
