using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Helpers;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Dueño")]
    public class ClienteController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;

        public ClienteController(BarberiaDbContext context, ICurrentBarberiaService currentBarberia)
        {
            _context = context;
            _currentBarberia = currentBarberia;
        }

        public async Task<IActionResult> Index(int pagina = 1)
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            const int tamañoPagina = 15;
            var clientesQuery = _context.Clientes.Where(c => c.BarberiaId == barberiaId);
            var clientesPaginados = await PaginatedList<Cliente>.CreateAsync(clientesQuery, pagina, tamañoPagina);
            return View(clientesPaginados);
        }


        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var cliente = await _context.Clientes
                .Include(c => c.Turnos)
                .FirstOrDefaultAsync(m => m.Id == id && m.BarberiaId == barberiaId);

            if (cliente == null) return NotFound();
            return View(cliente);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Apellido,Telefono")] Cliente cliente)
        {
            cliente.BarberiaId = _currentBarberia.GetRequerida().Id;

            if (ModelState.IsValid)
            {
                _context.Add(cliente);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id && c.BarberiaId == barberiaId);
            if (cliente == null) return NotFound();
            return View(cliente);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Apellido,Telefono")] Cliente cliente)
        {
            if (id != cliente.Id) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            // Aseguramos que el cliente que se está editando realmente pertenezca a esta barbería.
            var perteneceABarberia = await _context.Clientes
                .AsNoTracking()
                .AnyAsync(c => c.Id == id && c.BarberiaId == barberiaId);
            if (!perteneceABarberia) return NotFound();

            cliente.BarberiaId = barberiaId;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cliente);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClienteExists(cliente.Id, barberiaId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }


        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var cliente = await _context.Clientes
                .Include(c => c.Turnos)
                .FirstOrDefaultAsync(m => m.Id == id && m.BarberiaId == barberiaId);

            if (cliente == null) return NotFound();
            return View(cliente);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id && c.BarberiaId == barberiaId);
            if (cliente != null)
            {
                try
                {
                    _context.Clientes.Remove(cliente);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "No se puede eliminar este cliente porque tiene turnos asociados.");
                    return View("Delete", cliente);
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ClienteExists(int id, int barberiaId)
        {
            return _context.Clientes.Any(e => e.Id == id && e.BarberiaId == barberiaId);
        }
    }
}




