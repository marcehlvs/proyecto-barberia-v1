using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Helpers;
using barberia_turnos_mvc.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Services
{
    // Corre en segundo plano durante toda la vida de la app. Cada una hora
    // revisa si hay turnos de "mañana" (Pendiente o Confirmado) que todavía
    // no recibieron el recordatorio, y les manda un mail.
    //
    // Es un BackgroundService (singleton), así que NO puede inyectar
    // BarberiaDbContext directo (es Scoped) — por eso usa IServiceScopeFactory
    // para crear un scope nuevo en cada corrida, igual que se hace en
    // Program.cs para el seed inicial.
    public class RecordatorioTurnoService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RecordatorioTurnoService> _logger;
        private static readonly TimeSpan IntervaloEntreRevisiones = TimeSpan.FromHours(1);

        public RecordatorioTurnoService(IServiceScopeFactory scopeFactory, ILogger<RecordatorioTurnoService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(IntervaloEntreRevisiones);

            // Corre una vez apenas arranca la app, y después según el timer.
            do
            {
                try
                {
                    await EnviarRecordatoriosDeMañana(stoppingToken);
                }
                catch (Exception ex)
                {
                    // Un error acá (ej: SMTP caído un rato) no debe tirar abajo
                    // el servicio entero: seguimos intentando en la próxima vuelta.
                    _logger.LogError(ex, "Error revisando recordatorios de turno.");
                }
            }
            while (!stoppingToken.IsCancellationRequested
                && await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task EnviarRecordatoriosDeMañana(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BarberiaDbContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var inicioMañana = HoraArgentina.Hoy.AddDays(1);
            var finMañana = inicioMañana.AddDays(1);

            var turnos = await db.Turnos
                .Include(t => t.Cliente).ThenInclude(c => c.ApplicationUser)
                .Include(t => t.Servicio)
                .Include(t => t.Barberia)
                .Where(t => t.FechaHora >= inicioMañana && t.FechaHora < finMañana)
                .Where(t => t.Estado == EstadoTurno.Pendiente || t.Estado == EstadoTurno.Confirmado)
                .Where(t => !t.RecordatorioEnviado)
                .ToListAsync(ct);

            if (turnos.Count == 0) return;

            foreach (var turno in turnos)
            {
                var email = turno.Cliente?.ApplicationUser?.Email;
                if (string.IsNullOrEmpty(email))
                {
                    // Cliente sin cuenta vinculada (o sin email): no hay a
                    // dónde mandarlo. Lo marcamos igual para no reintentar
                    // cada hora en vano.
                    turno.RecordatorioEnviado = true;
                    continue;
                }

                var asunto = $"Recordatorio: tu turno mañana en {turno.Barberia.Nombre}";
                var cuerpo = $@"
                    <p>Hola {turno.Cliente!.Nombre}!</p>
                    <p>Te recordamos tu turno para mañana en <strong>{turno.Barberia.Nombre}</strong>:</p>
                    <ul>
                        <li><strong>Servicio:</strong> {turno.Servicio.Nombre}</li>
                        <li><strong>Hora:</strong> {turno.FechaHora:HH:mm} hs</li>
                    </ul>
                    <p>Si no podés asistir, por favor cancelalo con anticipación.</p>
                ";

                try
                {
                    await emailSender.SendEmailAsync(email, asunto, cuerpo);
                    turno.RecordatorioEnviado = true;
                }
                catch (Exception ex)
                {
                    // No marcamos RecordatorioEnviado si el envío falló: así
                    // se reintenta en la próxima corrida (dentro de la ventana
                    // de "mañana"), en vez de perder el recordatorio.
                    _logger.LogError(ex, "No se pudo enviar el recordatorio del turno {TurnoId}", turno.Id);
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
