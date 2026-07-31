using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Services
{
    public class TurnoValidacionService
    {
        private readonly BarberiaDbContext _context;

        public TurnoValidacionService(BarberiaDbContext context)
        {
            _context = context;
            
        }

        public async Task<string?> ValidarDisponibilidad(DateTime fechaHora, int servicioId, int? turnoIdAExcluir = null)
        {
            await ExpirarTurnosVencidosAsync();
            var servicio = await _context.Servicios.FindAsync(servicioId);
            if (servicio == null) return "El servicio seleccionado no existe.";

            var inicioNuevo = fechaHora;
            var finNuevo = fechaHora.AddMinutes(servicio.DuracionMinutos);

            var turnosActivos = await _context.Turnos
                .Include(t => t.Servicio)
                .Where(t => t.Estado != EstadoTurno.Cancelado && t.Estado != EstadoTurno.NoShow)
                .Where(t => turnoIdAExcluir == null || t.Id != turnoIdAExcluir)
                .ToListAsync();

            foreach (var turno in turnosActivos)
            {
                var inicioExistente = turno.FechaHora;
                var finExistente = turno.FechaHora.AddMinutes(turno.Servicio.DuracionMinutos);

                if (inicioNuevo < finExistente && inicioExistente < finNuevo)
                {
                    return $"Ya hay un turno agendado en ese horario ({inicioExistente:dd/MM HH:mm} - {finExistente:HH:mm}).";
                }
            }

            var bloqueos = await _context.BloqueoHorarios.ToListAsync();

            foreach (var bloqueo in bloqueos)
            {
                if (inicioNuevo < bloqueo.FechaFin && bloqueo.FechaInicio < finNuevo)
                {
                    return $"La barbería no atiende en ese horario ({bloqueo.FechaInicio:dd/MM HH:mm} - {bloqueo.FechaFin:dd/MM HH:mm}" +
                           (string.IsNullOrEmpty(bloqueo.Motivo) ? "" : $", motivo: {bloqueo.Motivo}") + ").";
                }
            }

            return null;
        }
        // Services/TurnoValidacionService.cs
        public async Task ExpirarTurnosVencidosAsync()
        {
            var limite = DateTime.Now.AddMinutes(-2);

            var turnosVencidos = await _context.Turnos
                .Where(t => t.Estado == EstadoTurno.Pendiente
                    && !t.SeñaPagada
                    && t.FechaCreacion < limite)
                .ToListAsync();

            if (turnosVencidos.Any())
            {
                foreach (var turno in turnosVencidos)
                {
                    turno.Estado = EstadoTurno.Cancelado;
                }
                await _context.SaveChangesAsync();
            }
        }
    }
}
