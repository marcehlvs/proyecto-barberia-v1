using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using MercadoPago.Client;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Cliente")]
    public class PagosController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly IMercadoPagoTokenService _tokenService;
        private readonly IEmailSender _emailSender;

        public PagosController(BarberiaDbContext context, IConfiguration config, IWebHostEnvironment env, IMercadoPagoTokenService tokenService, IEmailSender emailSender)
        {
            _context = context;
            _config = config;
            _env = env;
            _tokenService = tokenService;
            _emailSender = emailSender;
        }

        public async Task<IActionResult> IniciarPago(int turnoId)
        {
            var turno = await _context.Turnos
                .Include(t => t.Servicio)
                .Include(t => t.Barberia)
                .FirstOrDefaultAsync(t => t.Id == turnoId);

            if (turno == null) return NotFound();
            if (turno.MontoSeña == null || turno.MontoSeña.Value <= 0)
                return BadRequest("El turno no tiene un monto de seña válido.");

            // La barbería tiene que haber conectado su propia cuenta de MP
            // antes de poder cobrar. Si todavía no lo hizo, no hay a nombre
            // de quién cobrar la seña.
            var accessToken = await _tokenService.ObtenerAccessTokenValidoAsync(turno.Barberia);
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
            var preference = await client.CreateAsync(request, requestOptions);

            Console.WriteLine($"[MP Pago] Preferencia creada OK. Id={preference.Id}, InitPoint={preference.InitPoint}, SandboxInitPoint={preference.SandboxInitPoint}");

            turno.MercadoPagoPreferenceId = preference.Id;
            await _context.SaveChangesAsync();

            // Con un access_token normal (APP_USR) de una cuenta conectada,
            // MP arma DOS checkouts distintos para la misma preferencia:
            // init_point (producción real) y sandbox_init_point (acepta
            // cuentas/tarjetas de prueba). Usar init_point para pagar con
            // credenciales de prueba tira un error genérico en el checkout,
            // aunque el token y la preferencia estén bien armados.
            var urlDestino = _config.GetValue<bool>("MercadoPago:ModoPruebas")
                ? preference.SandboxInitPoint
                : preference.InitPoint;

            return Redirect(urlDestino);
        }

        public IActionResult Exito() => View();
        public IActionResult Fallo() => View();
        public IActionResult Pendiente() => View();

        [HttpPost]
        [AllowAnonymous]
        [Route("Pagos/Notificacion")]
        public async Task<IActionResult> Notificacion()
        {
            // Leemos el body crudo UNA sola vez: lo necesitamos tanto para
            // el formato nuevo de webhook (JSON con type/data.id/user_id,
            // que llega cuando MP entrega a la URL fija configurada en el
            // panel de la Aplicación) como para la validación de firma, que
            // tiene que calcularse sobre el MISMO "id" que usamos para
            // consultar el pago — antes solo mirábamos la query string, y
            // si el id venía únicamente en el body, el hash no coincidía
            // nunca (por eso el 401 persistente).
            string bodyRaw;
            using (var reader = new StreamReader(Request.Body))
            {
                bodyRaw = await reader.ReadToEndAsync();
            }

            var tipoNotificacion = Request.Query["type"].ToString();
            if (string.IsNullOrEmpty(tipoNotificacion)) tipoNotificacion = Request.Query["topic"].ToString();

            var dataId = Request.Query["data.id"].ToString();
            if (string.IsNullOrEmpty(dataId)) dataId = Request.Query["id"].ToString();

            string? userIdBody = null;

            if (string.IsNullOrEmpty(tipoNotificacion) || string.IsNullOrEmpty(dataId) || string.IsNullOrEmpty(userIdBody))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(bodyRaw);
                    var root = doc.RootElement;

                    if (string.IsNullOrEmpty(tipoNotificacion) && root.TryGetProperty("type", out var typeEl))
                        tipoNotificacion = typeEl.GetString();

                    if (string.IsNullOrEmpty(dataId) && root.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("id", out var idEl))
                        dataId = idEl.GetString();

                    if (root.TryGetProperty("user_id", out var userIdEl))
                        userIdBody = userIdEl.ValueKind == System.Text.Json.JsonValueKind.String
                            ? userIdEl.GetString()
                            : userIdEl.GetRawText();
                }
                catch
                {
                    // Body vacío o no es JSON (ej: entrega en formato viejo,
                    // todo por query string) — no hay nada más para leer.
                }
            }

            Console.WriteLine($"[MP Webhook] tipo={tipoNotificacion}, dataId={dataId}, userIdBody={userIdBody}");

            if (tipoNotificacion != "payment" || string.IsNullOrEmpty(dataId))
                return Ok();

            if (!ValidarFirma(dataId))
            {
                return Unauthorized();
            }

            // Preferimos el barberiaId de la query string (entrega vía el
            // notification_url de la preferencia, que sí lo incluye). Si no
            // está —típicamente porque la entrega vino de la URL FIJA
            // configurada en el panel de la Aplicación, que no puede llevar
            // query params dinámicos por barbería— usamos el user_id del
            // body para encontrar qué barbería es dueña de este pago.
            Barberia? barberia = null;

            if (int.TryParse(Request.Query["barberiaId"].ToString(), out var barberiaIdQuery))
            {
                barberia = await _context.Barberias.FirstOrDefaultAsync(b => b.Id == barberiaIdQuery);
            }

            if (barberia == null && !string.IsNullOrEmpty(userIdBody))
            {
                barberia = await _context.Barberias.FirstOrDefaultAsync(b => b.MercadoPagoUserId == userIdBody);
            }

            if (barberia == null)
            {
                Console.WriteLine("[MP Webhook] No se pudo identificar la barbería (ni por barberiaId ni por user_id) -> se corta.");
                return Ok();
            }

            var accessToken = await _tokenService.ObtenerAccessTokenValidoAsync(barberia);
            if (accessToken == null) return Ok();

            var paymentClient = new PaymentClient();
            var payment = await paymentClient.GetAsync(long.Parse(dataId), new RequestOptions { AccessToken = accessToken });

            if (payment?.ExternalReference == null) return Ok();

            var turno = await _context.Turnos
                .Include(t => t.Servicio)
                .Include(t => t.Cliente)
                    .ThenInclude(c => c.ApplicationUser)
                .FirstOrDefaultAsync(t => t.Id == int.Parse(payment.ExternalReference) && t.BarberiaId == barberia.Id);

            if (turno == null) return Ok();

            turno.MercadoPagoPaymentId = payment.Id.ToString();

            if (payment.Status == "approved")
            {
                turno.SeñaPagada = true;
                turno.Estado = barberia_turnos_mvc.Models.EstadoTurno.Confirmado;

                var emailCliente = turno.Cliente?.ApplicationUser?.Email;
                if (!string.IsNullOrEmpty(emailCliente))
                {
                    var htmlMessage = $@"
<div style='font-family: Arial, Helvetica, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f4f4f4; padding: 30px 0;'>
    <div style='background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 6px rgba(0,0,0,0.08);'>
        <div style='background-color: #1a1a1a; padding: 32px 24px; text-align: center;'>
            <h1 style='color: #ffffff; margin: 0; font-size: 24px; letter-spacing: 1px;'>💈 {HtmlEncoder.Default.Encode(barberia.Nombre.ToUpper())}</h1>
        </div>
        <div style='padding: 32px 24px;'>
            <h2 style='color: #1a1a1a; margin-top: 0; font-size: 20px;'>¡Turno confirmado!</h2>
            <p style='color: #444444; font-size: 15px; line-height: 1.6;'>
                Recibimos tu seña y tu turno quedó confirmado.
            </p>
            <table style='width: 100%; margin: 20px 0; font-size: 15px; color: #444444;'>
                <tr><td style='padding: 4px 0;'><strong>Servicio:</strong></td><td style='padding: 4px 0;'>{HtmlEncoder.Default.Encode(turno.Servicio.Nombre)}</td></tr>
                <tr><td style='padding: 4px 0;'><strong>Fecha y hora:</strong></td><td style='padding: 4px 0;'>{turno.FechaHora:dd/MM/yyyy HH:mm}</td></tr>
                <tr><td style='padding: 4px 0;'><strong>Seña pagada:</strong></td><td style='padding: 4px 0;'>${turno.MontoSeña:0.00}</td></tr>
            </table>
            <hr style='border: none; border-top: 1px solid #eeeeee; margin: 28px 0;'>
            <p style='color: #aaaaaa; font-size: 12px; line-height: 1.5;'>
                Te esperamos en {HtmlEncoder.Default.Encode(barberia.Nombre)}. Si necesitás cambiar o cancelar el turno, hacelo desde tu cuenta.
            </p>
        </div>
        <div style='background-color: #f0f0f0; padding: 16px 24px; text-align: center;'>
            <p style='color: #999999; font-size: 11px; margin: 0;'>© {DateTime.Now.Year} {HtmlEncoder.Default.Encode(barberia.Nombre)} · Todos los derechos reservados</p>
        </div>
    </div>
</div>";
                    try
                    {
                        await _emailSender.SendEmailAsync(emailCliente, $"Turno confirmado - {barberia.Nombre}", htmlMessage);
                    }
                    catch
                    {
                        // Igual que en el registro: si falla el envío de mail no
                        // queremos que se caiga el webhook (MP reintenta si le
                        // devolvemos un error, y el turno ya quedó confirmado
                        // en la base, que es lo importante).
                    }
                }
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

        // Recibe el "id" ya resuelto (venga de query string o del body),
        // así el hash se calcula siempre sobre el mismo valor que usamos
        // para buscar el pago — antes este método volvía a leer la query
        // string por su cuenta, y podía terminar validando contra un id
        // distinto (o vacío) del que realmente se usó.
        private bool ValidarFirma(string dataId)
        {
            var xSignature = Request.Headers["x-signature"].ToString();
            var xRequestId = Request.Headers["x-request-id"].ToString();

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