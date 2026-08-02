using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Services
{
    public class TurnoValidacionService
    {
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;

        public TurnoValidacionService(BarberiaDbContext context, ICurrentBarberiaService currentBarberia)
        {
            _context = context;
            _currentBarberia = currentBarberia;
        }

        public async Task<string?> ValidarDisponibilidad(DateTime fechaHora, int servicioId, int? turnoIdAExcluir = null)
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
            var limite = DateTime.Now.AddMinutes(-2);

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
                if (inicioSlot < DateTime.Now) continue;

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
