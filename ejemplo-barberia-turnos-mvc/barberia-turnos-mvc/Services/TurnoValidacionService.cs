using barberia_turnos_mvc.Helpers;
using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Services
{
    public class TurnoValidacionService
    {
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;
        private readonly IConfiguration _config;

        public TurnoValidacionService(BarberiaDbContext context, ICurrentBarberiaService currentBarberia, IConfiguration config)
        {
            _context = context;
            _currentBarberia = currentBarberia;
            _config = config;
        }

        public async Task<string?> ValidarDisponibilidad(DateTime fechaHora, int servicioId, int? turnoIdAExcluir = null, int? clienteId = null)
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;

            await ExpirarTurnosVencidosAsync();

            var servicio = await _context.Servicios
                .FirstOrDefaultAsync(s => s.Id == servicioId && s.BarberiaId == barberiaId);
            if (servicio == null) return "El servicio seleccionado no existe.";

            var inicioNuevo = fechaHora;
            var finNuevo = fechaHora.AddMinutes(servicio.DuracionMinutos);

            var turnosActivos = await _context.Turnos
                .Include(t => t.Servicio)
                .Where(t => t.BarberiaId == barberiaId)
                .Where(t => t.Estado != EstadoTurno.Cancelado && t.Estado != EstadoTurno.NoShow)
                .Where(t => turnoIdAExcluir == null || t.Id != turnoIdAExcluir)
                .ToListAsync();

            foreach (var turno in turnosActivos)
            {
                var inicioExistente = turno.FechaHora;
                var finExistente = turno.FechaHora.AddMinutes(turno.Servicio.DuracionMinutos);

                if (inicioNuevo < finExistente && inicioExistente < finNuevo)
                {
                    // Si el turno que choca es del mismo cliente y todavía está
                    // pendiente de pago, el mensaje genérico ("ya hay un turno
                    // agendado") confunde: parece que el horario lo tomó otra
                    // persona, cuando en realidad es una reserva propia sin
                    // completar (típicamente por un doble-submit del form).
                    if (clienteId != null && turno.ClienteId == clienteId.Value && turno.Estado == EstadoTurno.Pendiente)
                    {
                        return "Ya tenés una reserva en ese horario esperando el pago de la seña. " +
                               "Completá el pago o esperá unos minutos a que se libere para volver a intentar.";
                    }

                    return $"Ya hay un turno agendado en ese horario ({inicioExistente:dd/MM HH:mm} - {finExistente:HH:mm}).";
                }
            }

            var bloqueos = await _context.BloqueoHorarios
                .Where(b => b.BarberiaId == barberiaId)
                .ToListAsync();

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

        public async Task ExpirarTurnosVencidosAsync()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var minutosExpiracion = _config.GetValue<int?>("Turnos:MinutosExpiracionPendiente") ?? 2;
            var limite = HoraArgentina.Ahora.AddMinutes(-minutosExpiracion);

            var turnosVencidos = await _context.Turnos
                .Where(t => t.BarberiaId == barberiaId)
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

        public async Task<List<(TimeSpan Hora, bool Disponible)>> GenerarHorariosDelDia(DateTime fecha, int servicioId)
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;

            await ExpirarTurnosVencidosAsync();

            var barberia = await _context.Barberias.FirstOrDefaultAsync(b => b.Id == barberiaId);
            var servicio = await _context.Servicios
                .FirstOrDefaultAsync(s => s.Id == servicioId && s.BarberiaId == barberiaId);
            if (barberia == null || servicio == null) return new List<(TimeSpan, bool)>();

            var colchon = barberia.MinutosEntreTurnos;
            var duracion = servicio.DuracionMinutos;

            var inicioDelDia = fecha.Date;

            var turnosDelDia = await _context.Turnos
                .Include(t => t.Servicio)
                .Where(t => t.BarberiaId == barberiaId)
                .Where(t => t.FechaHora.Date == fecha.Date
                    && t.Estado != EstadoTurno.Cancelado
                    && t.Estado != EstadoTurno.NoShow)
                .ToListAsync();

            var bloqueosDelDia = await _context.BloqueoHorarios
                .Where(b => b.BarberiaId == barberiaId)
                .Where(b => b.FechaInicio.Date <= fecha.Date && b.FechaFin.Date >= fecha.Date)
                .ToListAsync();

            var resultado = new List<(TimeSpan, bool)>();

            for (var hora = barberia.HoraApertura; hora < barberia.HoraCierre; hora = hora.Add(TimeSpan.FromMinutes(5)))
            {
                var inicioSlot = inicioDelDia.Add(hora);
                var finSlot = inicioSlot.AddMinutes(duracion);

                if (finSlot.TimeOfDay > barberia.HoraCierre) continue;
                if (inicioSlot < HoraArgentina.Ahora) continue;

                var disponible = true;

                foreach (var turno in turnosDelDia)
                {
                    var inicioExistenteConColchon = turno.FechaHora.AddMinutes(-colchon);
                    var finExistenteConColchon = turno.FechaHora.AddMinutes(turno.Servicio.DuracionMinutos + colchon);

                    if (inicioSlot < finExistenteConColchon && inicioExistenteConColchon < finSlot)
                    {
                        disponible = false;
                        break;
                    }
                }

                if (disponible)
                {
                    foreach (var bloqueo in bloqueosDelDia)
                    {
                        if (inicioSlot < bloqueo.FechaFin && bloqueo.FechaInicio < finSlot)
                        {
                            disponible = false;
                            break;
                        }
                    }
                }

                resultado.Add((hora, disponible));
            }

            return resultado;
        }
    }
}
