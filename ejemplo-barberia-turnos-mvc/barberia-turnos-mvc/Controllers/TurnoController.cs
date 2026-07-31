using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Helpers;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Dueño")]
    public class TurnoController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly TurnoValidacionService _validacionService;

        public TurnoController(BarberiaDbContext context, TurnoValidacionService validacionService)
        {
            _context = context;
            _validacionService = validacionService;
        }

        // GET: Turno
        public async Task<IActionResult> Index(int pagina = 1)
        {
            await _validacionService.ExpirarTurnosVencidosAsync();

            const int tamañoPagina = 15;

            var turnosQuery = _context.Turnos
                .Include(t => t.Cliente)
                .Include(t => t.Barberia)
                .Include(t => t.Servicio)
                .OrderBy(t => t.FechaHora)
                .AsQueryable();

            var turnosPaginados = await PaginatedList<Turno>.CreateAsync(turnosQuery, pagina, tamañoPagina);

            return View(turnosPaginados);
        }

        // GET: Turno/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var turno = await _context.Turnos
                .Include(t => t.Cliente)
                .Include(t => t.Barberia)
                .Include(t => t.Servicio)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (turno == null) return NotFound();

            return View(turno);
        }

        // GET: Turno/Create
        public IActionResult Create()
        {
            CargarSelectLists();
            return View();
        }

        // POST: Turno/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FechaHora,Estado,ClienteId,ServicioId")] Turno turno)
        {
            if (turno.FechaHora < DateTime.Now)
            {
                ModelState.AddModelError("FechaHora", "No se puede reservar un turno en una fecha u hora pasada.");
            }

            if (turno.FechaHora.Minute % 5 != 0)
            {
                ModelState.AddModelError("FechaHora", "El turno debe comenzar en un múltiplo de 5 minutos (ej: 10:00, 10:05, 10:10).");
            }
            var errorDisponibilidad = await _validacionService.ValidarDisponibilidad(turno.FechaHora, turno.ServicioId);
            
            if (errorDisponibilidad != null)
            {
                ModelState.AddModelError("", errorDisponibilidad);
            }

            if (ModelState.IsValid)
            {
                var barberia = await _context.Barberias.FirstOrDefaultAsync();
                if (barberia == null)
                {
                    ModelState.AddModelError("", "No hay ninguna barbería cargada todavía.");
                    CargarSelectLists(turno);
                    return View(turno);
                }

                var servicio = await _context.Servicios.FindAsync(turno.ServicioId);
                if (servicio == null)
                {
                    ModelState.AddModelError("", "El servicio seleccionado no existe.");
                    CargarSelectLists(turno);
                    return View(turno);
                }

                turno.BarberiaId = barberia.Id;
                turno.MontoSeña = Math.Round(servicio.Precio * barberia.PorcentajeSeña / 100m, 2);
                turno.FechaCreacion = DateTime.Now;

                _context.Add(turno);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            CargarSelectLists(turno);
            return View(turno);
        }

        // GET: Turno/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var turno = await _context.Turnos.FindAsync(id);
            if (turno == null) return NotFound();

            CargarSelectLists(turno);
            return View(turno);
        }

        // POST: Turno/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FechaHora,Estado,ClienteId,ServicioId,BarberiaId")] Turno turno)
        {
            if (turno.FechaHora < DateTime.Now)
            {
                ModelState.AddModelError("FechaHora", "No se puede mover un turno a una fecha u hora pasada.");
            }

            if (turno.FechaHora.Minute % 5 != 0)
            {
                ModelState.AddModelError("FechaHora", "El turno debe comenzar en un múltiplo de 5 minutos (ej: 10:00, 10:05, 10:10).");
            }


            if (id != turno.Id) return NotFound();

            var errorDisponibilidad = await _validacionService.ValidarDisponibilidad(turno.FechaHora, turno.ServicioId, turnoIdAExcluir: turno.Id);
            if (errorDisponibilidad != null)
            {
                ModelState.AddModelError("", errorDisponibilidad);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(turno);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TurnoExists(turno.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            CargarSelectLists(turno);
            return View(turno);
        }

        // GET: Turno/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var turno = await _context.Turnos
                .Include(t => t.Cliente)
                .Include(t => t.Barberia)
                .Include(t => t.Servicio)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (turno == null) return NotFound();

            return View(turno);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var turno = await _context.Turnos.FindAsync(id);
            if (turno != null)
            {
                try
                {
                    _context.Turnos.Remove(turno);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    var turnoConDatos = await _context.Turnos
                        .Include(t => t.Cliente)
                        .Include(t => t.Servicio)
                        .FirstOrDefaultAsync(t => t.Id == id);

                    ModelState.AddModelError("", "No se pudo eliminar el turno.");
                    return View("Delete", turnoConDatos);
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TurnoExists(int id)
        {
            return _context.Turnos.Any(e => e.Id == id);
        }

        private void CargarSelectLists(Turno? turno = null)
        {
            ViewData["ClienteId"] = new SelectList(_context.Clientes, "Id", "NombreCompleto", turno?.ClienteId);
            ViewData["ServicioId"] = new SelectList(_context.Servicios, "Id", "Nombre", turno?.ServicioId);
        }
    }
}