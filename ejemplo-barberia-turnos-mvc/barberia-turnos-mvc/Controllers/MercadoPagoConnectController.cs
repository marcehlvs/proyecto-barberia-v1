using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace barberia_turnos_mvc.Controllers
{
    // Maneja la conexión OAuth de cada barbería con su propia cuenta de
    // Mercado Pago. "Conectar" se dispara desde dentro del panel del dueño
    // (con slug, como cualquier otra acción). "Callback" en cambio NO puede
    // depender del slug: es Mercado Pago quien pega directo a esta URL, y la
    // Redirect URL configurada en tu Application de MP es fija (no lleva
    // slug). Por eso usa [Route] explícito, igual que hicimos con
    // Pagos/Notificacion y Home/Error.
    public class MercadoPagoConnectController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly IConfiguration _config;
        private readonly IMercadoPagoTokenService _tokenService;
        private readonly ICurrentBarberiaService _currentBarberia;

        public MercadoPagoConnectController(BarberiaDbContext context, IConfiguration config, IMercadoPagoTokenService tokenService, ICurrentBarberiaService currentBarberia)
        {
            _context = context;
            _config = config;
            _tokenService = tokenService;
            _currentBarberia = currentBarberia;
        }

        // GET: /{barberiaSlug}/MercadoPagoConnect/Conectar
        [Authorize(Roles = "Dueño")]
        public IActionResult Conectar()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var clientId = _config["MercadoPago:ClientId"];
            var redirectUri = ArmarRedirectUri();
            var state = GenerarState(barberiaId);

            var authorizeUrl =
                "https://auth.mercadopago.com/authorization" +
                $"?client_id={Uri.EscapeDataString(clientId!)}" +
                "&response_type=code" +
                "&platform_id=mp" +
                $"&state={Uri.EscapeDataString(state)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";

            return Redirect(authorizeUrl);
        }

        // GET: /MercadoPagoConnect/Callback  (SIN slug — ver comentario de arriba)
        [AllowAnonymous]
        [Route("MercadoPagoConnect/Callback")]
        public async Task<IActionResult> Callback(string? code, string? state, string? error)
        {
            var barberiaId = ValidarState(state);
            if (barberiaId == null)
            {
                return BadRequest("El enlace de conexión con Mercado Pago no es válido o expiró. Volvé a intentarlo desde Configuración.");
            }

            var barberia = await _context.Barberias.FirstOrDefaultAsync(b => b.Id == barberiaId.Value);
            if (barberia == null) return NotFound();

            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            {
                // El dueño canceló la autorización desde el lado de MP.
                TempData["MpConnectError"] = "No se completó la conexión con Mercado Pago.";
                return RedirectToAction("Configuracion", "Barberia", new { barberiaSlug = barberia.Slug });
            }

            try
            {
                await _tokenService.IntercambiarCodigoPorTokenAsync(barberia, code, ArmarRedirectUri());
            }
            catch (Exception)
            {
                TempData["MpConnectError"] = "Hubo un error al conectar con Mercado Pago. Probá de nuevo.";
                return RedirectToAction("Configuracion", "Barberia", new { barberiaSlug = barberia.Slug });
            }

            TempData["MpConnectOk"] = "¡Cuenta de Mercado Pago conectada correctamente!";
            return RedirectToAction("Configuracion", "Barberia", new { barberiaSlug = barberia.Slug });
        }

        // POST: /{barberiaSlug}/MercadoPagoConnect/Desconectar
        [Authorize(Roles = "Dueño")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Desconectar()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var barberia = await _context.Barberias.FirstOrDefaultAsync(b => b.Id == barberiaId);
            if (barberia == null) return NotFound();

            _tokenService.Desconectar(barberia);
            await _context.SaveChangesAsync();

            return RedirectToAction("Configuracion", "Barberia", new { barberiaSlug = barberia.Slug });
        }

        private string ArmarRedirectUri()
        {
            // Tiene que coincidir EXACTO (carácter por carácter) con la
            // "Redirect URL" que configures en el panel de tu Application
            // en developers.mercadopago.com.
            var baseUrl = _config["MercadoPago:RedirectUriBase"] ?? $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/MercadoPagoConnect/Callback";
        }

        // El "state" viaja por una URL pública (se lo pasamos a MP y vuelve
        // en el callback), así que lo firmamos con HMAC para asegurarnos de
        // que nadie lo pueda falsear y conectar su propia cuenta de MP a
        // una barbería ajena. Mismo patrón que ValidarFirma en PagosController.
        private string GenerarState(int barberiaId)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = $"{barberiaId}.{timestamp}";
            var firma = FirmarPayload(payload);
            return $"{payload}.{firma}";
        }

        private int? ValidarState(string? state)
        {
            if (string.IsNullOrEmpty(state)) return null;

            var partes = state.Split('.');
            if (partes.Length != 3) return null;

            var barberiaIdTexto = partes[0];
            var timestampTexto = partes[1];
            var firmaRecibida = partes[2];

            var payload = $"{barberiaIdTexto}.{timestampTexto}";
            var firmaEsperada = FirmarPayload(payload);

            var firmaValida = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(firmaRecibida),
                Encoding.UTF8.GetBytes(firmaEsperada));

            if (!firmaValida) return null;

            if (!long.TryParse(timestampTexto, out var timestamp)) return null;
            var emitido = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            if (DateTimeOffset.UtcNow - emitido > TimeSpan.FromMinutes(15)) return null; // el "state" venció

            return int.TryParse(barberiaIdTexto, out var barberiaId) ? barberiaId : null;
        }

        private string FirmarPayload(string payload)
        {
            var secreto = _config["MercadoPago:StateSecret"]
                ?? throw new InvalidOperationException("Falta configurar MercadoPago:StateSecret.");
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }
    }
}
