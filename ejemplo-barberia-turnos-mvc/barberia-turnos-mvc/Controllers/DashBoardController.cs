using barberia_turnos_mvc.Helpers;
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
        private readonly ICurrentBarberiaService _currentBarberia;

        public DashboardController(BarberiaDbContext context, TurnoValidacionService turnoValidacionService, ICurrentBarberiaService currentBarberia)
        {
            _context = context;
            _validacionService = turnoValidacionService;
            _currentBarberia = currentBarberia;
        }

        public async Task<IActionResult> Index()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;

            await _validacionService.ExpirarTurnosVencidosAsync();
            var hoy = HoraArgentina.Hoy;
            var mananaInicio = hoy.AddDays(1);

            var turnosDeHoy = await _context.Turnos
                .Include(t => t.Cliente)
                .Include(t => t.Servicio)
                .Where(t => t.BarberiaId == barberiaId)
                .Where(t => t.FechaHora >= hoy && t.FechaHora < mananaInicio)
                .OrderBy(t => t.FechaHora)
                .ToListAsync();

            var primerDiaDelMes = new DateTime(hoy.Year, hoy.Month, 1);
            var primerDiaDelMesSiguiente = primerDiaDelMes.AddMonths(1);

            var ingresosDelMes = await _context.Turnos
                .Where(t => t.BarberiaId == barberiaId)
                .Where(t => t.SeñaPagada
                    && t.FechaHora >= primerDiaDelMes
                    && t.FechaHora < primerDiaDelMesSiguiente)
                .SumAsync(t => t.MontoSeña ?? 0);

            var turnosPendientesDePago = await _context.Turnos
                .Where(t => t.BarberiaId == barberiaId)
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

        public async Task<IActionResult> Estadisticas()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var hoy = HoraArgentina.Hoy;

            // Ventana de 6 meses, incluyendo el mes actual.
            var inicioVentana = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-5);

            var turnos = await _context.Turnos
                .Include(t => t.Servicio)
                .Where(t => t.BarberiaId == barberiaId && t.FechaHora >= inicioVentana)
                .ToListAsync();

            // Turnos "Cancelado" no reflejan actividad real de la barbería:
            // los sacamos de los conteos por mes y del ranking de servicios.
            var turnosValidos = turnos.Where(t => t.Estado != EstadoTurno.Cancelado).ToList();

            var meses = Enumerable.Range(0, 6)
                .Select(i => inicioVentana.AddMonths(i))
                .ToList();

            var modelo = new EstadisticasViewModel
            {
                MesesLabels = meses.Select(m => m.ToString("MMM yyyy")).ToList(),
                IngresosPorMes = meses
                    .Select(m => turnosValidos
                        .Where(t => t.SeñaPagada && t.FechaHora.Year == m.Year && t.FechaHora.Month == m.Month)
                        .Sum(t => t.MontoSeña ?? 0))
                    .ToList(),
                TurnosPorMes = meses
                    .Select(m => turnosValidos.Count(t => t.FechaHora.Year == m.Year && t.FechaHora.Month == m.Month))
                    .ToList(),
                TopServicios = turnosValidos
                    .GroupBy(t => t.Servicio.Nombre)
                    .Select(g => new RankingServicioDto
                    {
                        Nombre = g.Key,
                        Cantidad = g.Count(),
                        IngresoTotal = g.Where(t => t.SeñaPagada).Sum(t => t.MontoSeña ?? 0)
                    })
                    .OrderByDescending(s => s.Cantidad)
                    .Take(5)
                    .ToList(),
                TotalTurnosUltimos6Meses = turnosValidos.Count,
                IngresosUltimos6Meses = turnosValidos.Where(t => t.SeñaPagada).Sum(t => t.MontoSeña ?? 0)
            };

            // Tasa de no-show: solo tiene sentido sobre turnos que ya pasaron
            // (Completado o NoShow). Si todavía no hubo ninguno, dejamos null
            // para que la vista muestre "sin datos" en vez de un 0% engañoso.
            var turnosPasados = turnosValidos.Count(t => t.Estado == EstadoTurno.Completado || t.Estado == EstadoTurno.NoShow);
            if (turnosPasados > 0)
            {
                var noShows = turnosValidos.Count(t => t.Estado == EstadoTurno.NoShow);
                modelo.TasaNoShow = Math.Round(noShows * 100.0 / turnosPasados, 1);
            }

            return View(modelo);
        }
    }
}