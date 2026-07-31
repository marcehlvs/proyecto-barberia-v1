using barberia_turnos_mvc.Data;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Cliente")]
    public class PagosController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public PagosController(BarberiaDbContext context, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _config = config;
            _env = env;
        }

        public async Task<IActionResult> IniciarPago(int turnoId)
        {
            var turno = await _context.Turnos
                .Include(t => t.Servicio)
                .FirstOrDefaultAsync(t => t.Id == turnoId);

            if (turno == null) return NotFound();
            if (turno.MontoSeña == null || turno.MontoSeña.Value <= 0)
                return BadRequest("El turno no tiene un monto de seña válido.");

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var notificationBase = _config["MercadoPago:NotificationUrlBase"] ?? baseUrl;
            var notificationUrlFinal = $"{notificationBase}/Pagos/Notificacion";

            var request = new PreferenceRequest
            {
                Items = new List<PreferenceItemRequest>
                {
                    new PreferenceItemRequest
                    {
                        Title = $"Seña turno - {turno.Servicio.Nombre}",
                        Quantity = 1,
                        CurrencyId = "ARS",
                        UnitPrice = turno.MontoSeña.Value
                    }
                },
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = $"{baseUrl}/Pagos/Exito",
                    Failure = $"{baseUrl}/Pagos/Fallo",
                    Pending = $"{baseUrl}/Pagos/Pendiente"
                },
                AutoReturn = "approved",
                NotificationUrl = notificationUrlFinal,
                ExternalReference = turno.Id.ToString()
            };

            var client = new PreferenceClient();
            var preference = await client.CreateAsync(request);

            turno.MercadoPagoPreferenceId = preference.Id;
            await _context.SaveChangesAsync();

            return Redirect(preference.InitPoint);
        }

        public IActionResult Exito() => View();
        public IActionResult Fallo() => View();
        public IActionResult Pendiente() => View();

        [HttpPost]
        [AllowAnonymous]
        [Route("Pagos/Notificacion")]
        public async Task<IActionResult> Notificacion(
    [FromQuery(Name = "type")] string? type,
    [FromQuery(Name = "topic")] string? topic,
    [FromQuery(Name = "data.id")] string? dataId,
    [FromQuery(Name = "id")] string? id)
        {
            var tipoNotificacion = type ?? topic;
            var paymentId = dataId ?? id; // esto sigue sirviendo para BUSCAR el pago en la API

            if (tipoNotificacion != "payment" || string.IsNullOrEmpty(paymentId))
                return Ok();

            if (!ValidarFirma())
            {
                return Unauthorized();
            }

            var paymentClient = new PaymentClient();
            var payment = await paymentClient.GetAsync(long.Parse(paymentId));

            if (payment?.ExternalReference == null) return Ok();

            var turno = await _context.Turnos
                .FirstOrDefaultAsync(t => t.Id == int.Parse(payment.ExternalReference));

            if (turno == null) return Ok();

            turno.MercadoPagoPaymentId = payment.Id.ToString();

            if (payment.Status == "approved")
            {
                turno.SeñaPagada = true;
                turno.Estado = barberia_turnos_mvc.Models.EstadoTurno.Confirmado;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
        private bool ValidarFirma()
        {
            var xSignature = Request.Headers["x-signature"].ToString();
            var xRequestId = Request.Headers["x-request-id"].ToString();

            // Antes: solo leía "data.id". Ahora: si no está, cae a "id" (formato viejo)
            var dataId = Request.Query.ContainsKey("data.id")
                ? Request.Query["data.id"].ToString()
                : Request.Query["id"].ToString();

            if (string.IsNullOrEmpty(xSignature)) return false;

            string? ts = null;
            string? hash = null;

            foreach (var parte in xSignature.Split(','))
            {
                var eq = parte.IndexOf('=');
                if (eq < 0) continue;
                var clave = parte[..eq].Trim();
                var valor = parte[(eq + 1)..].Trim();
                if (clave == "ts") ts = valor;
                else if (clave == "v1") hash = valor;
            }

            if (ts == null || hash == null) return false;

            var secreto = _config["MercadoPago:WebhookSecret"]?.Trim();
            if (string.IsNullOrEmpty(secreto)) return false;

            var partes = new List<string>();
            if (!string.IsNullOrEmpty(dataId)) partes.Add($"id:{dataId.ToLowerInvariant()}");
            if (!string.IsNullOrEmpty(xRequestId)) partes.Add($"request-id:{xRequestId}");
            partes.Add($"ts:{ts}");

            var manifest = string.Concat(partes);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
            var hashCalculado = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();

            var esValido = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(hashCalculado),
                Encoding.UTF8.GetBytes(hash)
            );

            if (!esValido)
            {
                Console.WriteLine($"[MP Webhook] dataId={dataId} | xRequestId={xRequestId} | ts={ts}");
                Console.WriteLine($"[MP Webhook] Manifest={manifest}");
                Console.WriteLine($"[MP Webhook] Hash recibido={hash} | Hash calculado={hashCalculado}");

                if (_env.IsDevelopment())
                {
                    Console.WriteLine("[MP Webhook] ⚠️ Permitida solo por Development.");
                    return true;
                }
            }

            return esValido;
        }




    }
}