using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Data;
using Microsoft.AspNetCore.Authorization;



namespace barberia_turnos_mvc.Controllers
{
    
    public class ServicioController : Controller
    {
        private readonly BarberiaDbContext _context;

        public ServicioController(BarberiaDbContext context)
        {
            _context = context;
        }
        [Authorize(Roles = "Dueño")]

        public async Task<IActionResult> Index()
        {
            var servicios = await _context.Servicios
                .Include(s => s.Barberia)
                .ToListAsync();
            return View(servicios);
        }
        [Authorize(Roles = "Dueño")]

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var servicio = await _context.Servicios
                .Include(s => s.Barberia)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (servicio == null) return NotFound();
            return View(servicio);
        }
        [Authorize(Roles = "Dueño")]

        public IActionResult Create()
        {
            return View();
        }
        [Authorize(Roles = "Dueño")]

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Precio,DuracionMinutos")] Servicio servicio)
        {
            if (ModelState.IsValid)
            {
                var barberia = await _context.Barberias.FirstOrDefaultAsync();
                if (barberia == null)
                {
                    ModelState.AddModelError("", "No hay ninguna barbería cargada todavía.");
                    return View(servicio);
                }

                servicio.BarberiaId = barberia.Id;
                _context.Add(servicio);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(servicio);
        }
        [Authorize(Roles = "Dueño")]

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var servicio = await _context.Servicios.FindAsync(id);
            if (servicio == null) return NotFound();
            return View(servicio);
        }
        [Authorize(Roles = "Dueño")]

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Precio,DuracionMinutos,BarberiaId")] Servicio servicio)
        {
            if (id != servicio.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(servicio);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServicioExists(servicio.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(servicio);
        }
        [Authorize(Roles = "Dueño")]

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var servicio = await _context.Servicios
                .Include(s => s.Barberia)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (servicio == null) return NotFound();
            return View(servicio);
        }

        [Authorize(Roles = "Dueño")]

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var servicio = await _context.Servicios.FindAsync(id);
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

        private bool ServicioExists(int id)
        {
            return _context.Servicios.Any(e => e.Id == id);
        }
    }
}