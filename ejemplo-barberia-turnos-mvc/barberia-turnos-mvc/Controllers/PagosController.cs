using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Services;
using MercadoPago.Client;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Http;
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
        private readonly IMercadoPagoTokenService _tokenService;

        public PagosController(BarberiaDbContext context, IConfiguration config, IWebHostEnvironment env, IMercadoPagoTokenService tokenService)
        {
            _context = context;
            _config = config;
            _env = env;
            _tokenService = tokenService;
        }

        public async Task<IActionResult> IniciarPago(int turnoId)
        {
            Console.WriteLine($"[MP Pago] IniciarPago llamado. turnoId={turnoId}");

            var turno = await _context.Turnos
                .Include(t => t.Servicio)
                .Include(t => t.Barberia)
                .FirstOrDefaultAsync(t => t.Id == turnoId);

            if (turno == null)
            {
                Console.WriteLine($"[MP Pago] Turno {turnoId} no encontrado.");
                return NotFound();
            }

            Console.WriteLine($"[MP Pago] Turno encontrado. BarberiaId={turno.BarberiaId} ({turno.Barberia?.Nombre}), MontoSeña={turno.MontoSeña}");

            if (turno.MontoSeña == null || turno.MontoSeña.Value <= 0)
            {
                Console.WriteLine("[MP Pago] MontoSeña inválido, se corta acá.");
                return BadRequest("El turno no tiene un monto de seña válido.");
            }

            // La barbería tiene que haber conectado su propia cuenta de MP
            // antes de poder cobrar. Si todavía no lo hizo, no hay a nombre
            // de quién cobrar la seña.
            var accessToken = await _tokenService.ObtenerAccessTokenValidoAsync(turno.Barberia);
            Console.WriteLine($"[MP Pago] accessToken obtenido: {(accessToken == null ? "NULL (no hay conexión válida)" : accessToken[..Math.Min(12, accessToken.Length)] + "...")}");

            if (accessToken == null)
            {
                TempData["ErrorPago"] = "Esta barbería todavía no configuró Mercado Pago. Contactala para que pueda cobrar señas.";
                return RedirectToAction("MisTurnos", "MiCuenta");
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var slug = turno.Barberia.Slug;

            // Las BackUrls tienen que incluir el slug de la barbería: la ruta
            // convencional de la app exige {barberiaSlug}/{controller}/{action}.
            var notificationBase = _config["MercadoPago:NotificationUrlBase"] ?? baseUrl;
            // Le agregamos el barberiaId a la propia NotificationUrl: cuando
            // llegue el webhook, así sabemos de entrada con qué token de
            // acceso hay que consultar el pago (ver comentario en Notificacion).
            var notificationUrlFinal = $"{notificationBase}/Pagos/Notificacion?barberiaId={turno.BarberiaId}";

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
                    Success = $"{baseUrl}/{slug}/Pagos/Exito",
                    Failure = $"{baseUrl}/{slug}/Pagos/Fallo",
                    Pending = $"{baseUrl}/{slug}/Pagos/Pendiente"
                },
                AutoReturn = "approved",
                NotificationUrl = notificationUrlFinal,
                ExternalReference = turno.Id.ToString(),
                MarketplaceFee = CalcularComision(turno.MontoSeña.Value)
            };

            var requestOptions = new RequestOptions { AccessToken = accessToken };

            var client = new PreferenceClient();
            MercadoPago.Resource.Preference.Preference preference;
            try
            {
                preference = await client.CreateAsync(request, requestOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MP Pago] ERROR creando la preferencia: {ex.GetType().Name} - {ex.Message}");
                throw;
            }

            Console.WriteLine($"[MP Pago] Preferencia creada OK. Id={preference.Id}, InitPoint={preference.InitPoint}");

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
    [FromQuery(Name = "id")] string? id,
    [FromQuery(Name = "barberiaId")] int? barberiaId)
        {
            var tipoNotificacion = type ?? topic;
            var paymentId = dataId ?? id; // esto sigue sirviendo para BUSCAR el pago en la API

            Console.WriteLine($"[MP Webhook] Notificacion recibida. type={type}, topic={topic}, dataId={dataId}, id={id}, barberiaId={barberiaId}");

            if (tipoNotificacion != "payment" || string.IsNullOrEmpty(paymentId))
            {
                Console.WriteLine("[MP Webhook] Ignorada: no es notificación de tipo 'payment', o falta el paymentId.");
                return Ok();
            }

            if (!ValidarFirma())
            {
                Console.WriteLine("[MP Webhook] Firma inválida -> Unauthorized.");
                return Unauthorized();
            }

            if (barberiaId == null)
            {
                Console.WriteLine("[MP Webhook] Sin barberiaId en la query -> se corta.");
                return Ok();
            }

            var barberia = await _context.Barberias.FirstOrDefaultAsync(b => b.Id == barberiaId.Value);
            if (barberia == null)
            {
                Console.WriteLine($"[MP Webhook] No existe ninguna Barberia con Id={barberiaId.Value}.");
                return Ok();
            }

            // El pago pertenece a la cuenta de MP de ESTA barbería, así que
            // hay que consultarlo con SU token (no con uno global). Ver
            // comentario en IniciarPago sobre por qué viaja el barberiaId
            // en la propia NotificationUrl.
            var accessToken = await _tokenService.ObtenerAccessTokenValidoAsync(barberia);
            if (accessToken == null)
            {
                Console.WriteLine($"[MP Webhook] La barbería {barberia.Nombre} no tiene un accessToken válido -> se corta.");
                return Ok();
            }

            var paymentClient = new PaymentClient();
            var payment = await paymentClient.GetAsync(long.Parse(paymentId), new RequestOptions { AccessToken = accessToken });

            Console.WriteLine($"[MP Webhook] Payment consultado. Id={payment?.Id}, Status={payment?.Status}, ExternalReference={payment?.ExternalReference}");

            if (payment?.ExternalReference == null)
            {
                Console.WriteLine("[MP Webhook] payment.ExternalReference viene null -> se corta.");
                return Ok();
            }

            var turno = await _context.Turnos
                .FirstOrDefaultAsync(t => t.Id == int.Parse(payment.ExternalReference) && t.BarberiaId == barberiaId.Value);

            if (turno == null)
            {
                Console.WriteLine($"[MP Webhook] No se encontró el Turno {payment.ExternalReference} para BarberiaId={barberiaId.Value}.");
                return Ok();
            }

            turno.MercadoPagoPaymentId = payment.Id.ToString();

            if (payment.Status == "approved")
            {
                turno.SeñaPagada = true;
                turno.Estado = barberia_turnos_mvc.Models.EstadoTurno.Confirmado;
                Console.WriteLine($"[MP Webhook] Turno {turno.Id} marcado como Confirmado/Pagada.");
            }
            else
            {
                Console.WriteLine($"[MP Webhook] Status='{payment.Status}' no es 'approved', el turno NO se marca como pagado.");
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // Comisión del marketplace (lo que te queda a vos por cada seña
        // cobrada). 0 hasta que configures un porcentaje — así el
        // comportamiento no cambia para nadie hasta que lo actives a propósito.
        private decimal CalcularComision(decimal montoSeña)
        {
            var porcentaje = _config.GetValue<decimal?>("MercadoPago:ComisionPorcentaje") ?? 0;
            if (porcentaje <= 0) return 0;
            return Math.Round(montoSeña * porcentaje / 100m, 2);
        }

        private bool ValidarFirma()
        {
            var xSignature = Request.Headers["x-signature"].ToString();
            var xRequestId = Request.Headers["x-request-id"].ToString();

            // Antes: solo leía "data.id". Ahora: si no está, cae a "id" (formato viejo)
            var dataId = Request.Query.ContainsKey("data.id")
                ? Request.Query["data.id"].ToString()
                : Request.Query["id"].ToString();

            if (string.IsNullOrEmpty(xSignature))
            {
                Console.WriteLine("[MP Webhook] ValidarFirma: falta el header x-signature.");
                return false;
            }

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

            if (ts == null || hash == null)
            {
                Console.WriteLine($"[MP Webhook] ValidarFirma: no se pudo parsear ts/hash del header x-signature='{xSignature}'.");
                return false;
            }

            var secreto = _config["MercadoPago:WebhookSecret"]?.Trim();
            if (string.IsNullOrEmpty(secreto))
            {
                Console.WriteLine("[MP Webhook] ValidarFirma: falta configurar MercadoPago:WebhookSecret.");
                return false;
            }

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