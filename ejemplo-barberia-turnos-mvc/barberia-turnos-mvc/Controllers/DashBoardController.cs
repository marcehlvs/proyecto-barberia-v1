using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Dueño")]
    public class DashboardController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly TurnoValidacionService _validacionService;



        public DashboardController(BarberiaDbContext context, TurnoValidacionService turnoValidacionService)
        {
            _context = context;
            _validacionService = turnoValidacionService;
        }

        public async Task<IActionResult> Index()
        {
            await _validacionService.ExpirarTurnosVencidosAsync();
            var hoy = DateTime.Today;
            var mananaInicio = hoy.AddDays(1);

            var turnosDeHoy = await _context.Turnos
                .Include(t => t.Cliente)
                .Include(t => t.Servicio)
                .Where(t => t.FechaHora >= hoy && t.FechaHora < mananaInicio)
                .OrderBy(t => t.FechaHora)
                .ToListAsync();

            var primerDiaDelMes = new DateTime(hoy.Year, hoy.Month, 1);
            var primerDiaDelMesSiguiente = primerDiaDelMes.AddMonths(1);

            var ingresosDelMes = await _context.Turnos
                .Where(t => t.SeñaPagada
                    && t.FechaHora >= primerDiaDelMes
                    && t.FechaHora < primerDiaDelMesSiguiente)
                .SumAsync(t => t.MontoSeña ?? 0);

            var turnosPendientesDePago = await _context.Turnos
                .CountAsync(t => !t.SeñaPagada && t.Estado == EstadoTurno.Pendiente);

            var modelo = new DashboardViewModel
            {
                TurnosDeHoy = turnosDeHoy,
                IngresosDelMes = ingresosDelMes,
                TurnosPendientesDePago = turnosPendientesDePago,
                TurnosConfirmadosHoy = turnosDeHoy.Count(t => t.Estado == EstadoTurno.Confirmado)
            };

            return View(modelo);
        }
    }
}