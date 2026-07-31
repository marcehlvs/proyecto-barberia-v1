using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Dueño")]
    public class BloqueoHorarioController : Controller
    {
        private readonly BarberiaDbContext _context;

        public BloqueoHorarioController(BarberiaDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var bloqueos = await _context.BloqueoHorarios
                .Include(b => b.Barberia)
                .OrderBy(b => b.FechaInicio)
                .ToListAsync();
            return View(bloqueos);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var bloqueo = await _context.BloqueoHorarios
                .Include(b => b.Barberia)
                .FirstOrDefaultAsync(m => m.Id == id);

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

            if (ModelState.IsValid)
            {
                var barberia = await _context.Barberias.FirstOrDefaultAsync();
                if (barberia == null)
                {
                    ModelState.AddModelError("", "No hay ninguna barbería cargada todavía.");
                    return View(bloqueo);
                }

                bloqueo.BarberiaId = barberia.Id;
                _context.Add(bloqueo);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(bloqueo);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var bloqueo = await _context.BloqueoHorarios.FindAsync(id);
            if (bloqueo == null) return NotFound();
            return View(bloqueo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FechaInicio,FechaFin,Motivo,BarberiaId")] BloqueoHorario bloqueo)
        {
            if (id != bloqueo.Id) return NotFound();

            if (bloqueo.FechaFin <= bloqueo.FechaInicio)
            {
                ModelState.AddModelError("", "La fecha de fin debe ser posterior a la fecha de inicio.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bloqueo);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BloqueoExists(bloqueo.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(bloqueo);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var bloqueo = await _context.BloqueoHorarios
                .Include(b => b.Barberia)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (bloqueo == null) return NotFound();
            return View(bloqueo);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bloqueo = await _context.BloqueoHorarios.FindAsync(id);
            if (bloqueo != null)
            {
                _context.BloqueoHorarios.Remove(bloqueo);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool BloqueoExists(int id)
        {
            return _context.BloqueoHorarios.Any(e => e.Id == id);
        }
    }
}