using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Cliente")]
    public class MiCuentaController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TurnoValidacionService _validacionService;

        public MiCuentaController(
        BarberiaDbContext context,
        UserManager<ApplicationUser> userManager,
        TurnoValidacionService validacionService)   
        {
            _context = context;
            _userManager = userManager;
            _validacionService = validacionService;   
        }
        

        // GET: MiCuenta/Editar
        public async Task<IActionResult> Editar()
        {
            var userId = _userManager.GetUserId(User);

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.ApplicationUserId == userId);

            if (cliente == null) return NotFound();

            return View(cliente);
        }

        // POST: MiCuenta/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar([Bind("Id,Nombre,Apellido,Telefono")] Cliente clienteEditado)
        {
            var userId = _userManager.GetUserId(User);

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.ApplicationUserId == userId);

            if (cliente == null) return NotFound();

            // Chequeo de seguridad: que el ID enviado coincida con el cliente real del usuario logueado
            if (cliente.Id != clienteEditado.Id) return Forbid();

            if (ModelState.IsValid)
            {
                cliente.Nombre = clienteEditado.Nombre;
                cliente.Apellido = clienteEditado.Apellido;
                cliente.Telefono = clienteEditado.Telefono;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(MisTurnos));
            }

            return View(clienteEditado);
        }

        [HttpGet]
        public async Task<IActionResult> HorariosDisponibles(DateTime fecha, int servicioId)
        {
            var horarios = await _validacionService.GenerarHorariosDelDia(fecha, servicioId);

            var resultado = horarios.Select(h => new
            {
                hora = h.Hora.ToString(@"hh\:mm"),
                disponible = h.Disponible
            });

            return Json(resultado);
        }
        
        public async Task<IActionResult> MisTurnos()
        {
            var userId = _userManager.GetUserId(User);

            var cliente = await _context.Clientes
                .Include(c => c.Turnos)
                    .ThenInclude(t => t.Servicio)
                .FirstOrDefaultAsync(c => c.ApplicationUserId == userId);

            if (cliente == null) return NotFound();

            return View(cliente);
        }
        [HttpGet]
        // GET: MiCuenta/Reservar
        public IActionResult Reservar()
        {
            ViewData["ServicioId"] = new SelectList(_context.Servicios, "Id", "Nombre");
            return View();
        }

        // POST: MiCuenta/Reservar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reservar(DateTime fechaHora, int servicioId)
        {
            var userId = _userManager.GetUserId(User);

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.ApplicationUserId == userId);

            if (cliente == null) return NotFound();

            if (fechaHora < DateTime.Now)
            {
                ModelState.AddModelError("", "No se puede reservar un turno en una fecha u hora pasada.");
            }

            if (fechaHora.Minute % 5 != 0)
            {
                ModelState.AddModelError("", "El turno debe comenzar en un múltiplo de 5 minutos (ej: 10:00, 10:05, 10:10).");
            }

            var errorDisponibilidad = await _validacionService.ValidarDisponibilidad(fechaHora, servicioId);
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
                    ViewData["ServicioId"] = new SelectList(_context.Servicios, "Id", "Nombre", servicioId);
                    return View();
                }

                var servicio = await _context.Servicios.FindAsync(servicioId);
                if (servicio == null) return NotFound();

                var montoSeña = Math.Round(servicio.Precio * (barberia.PorcentajeSeña / 100m), 2);

                var turno = new Turno
                {
                    FechaHora = fechaHora,
                    Estado = EstadoTurno.Pendiente,
                    ClienteId = cliente.Id,
                    ServicioId = servicioId,
                    BarberiaId = barberia.Id,
                    MontoSeña = montoSeña,
                    SeñaPagada = false,
                    FechaCreacion = DateTime.Now
                };

                _context.Turnos.Add(turno);
                await _context.SaveChangesAsync();
                return RedirectToAction("IniciarPago", "Pagos", new { turnoId = turno.Id });
            }

            ViewData["ServicioId"] = new SelectList(_context.Servicios, "Id", "Nombre", servicioId);
            return View();
        }
    }
}
 