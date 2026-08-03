using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace barberia_turnos_mvc.Services
{
    // Respuesta del endpoint https://api.mercadopago.com/oauth/token,
    // tanto para el intercambio inicial (authorization_code) como para
    // el refresh (refresh_token). Mismo shape en ambos casos.
    public class MercadoPagoTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";

        [JsonPropertyName("public_key")]
        public string? PublicKey { get; set; }

        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        // Segundos hasta que expire (MP hoy devuelve 15552000 ~ 180 días).
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public interface IMercadoPagoTokenService
    {
        // Intercambia el "code" que devuelve MP tras la autorización por
        // un access_token + refresh_token, y los guarda en la Barberia.
        Task IntercambiarCodigoPorTokenAsync(Barberia barberia, string code, string redirectUri);

        // Devuelve un access_token vigente para esta barbería, renovándolo
        // primero si está vencido o a punto de vencer. Null si la barbería
        // nunca conectó su cuenta de Mercado Pago.
        Task<string?> ObtenerAccessTokenValidoAsync(Barberia barberia);

        // Consulta a la propia API de MP (GET /users/me) de quién es
        // realmente la cuenta conectada. Sirve para que el dueño confirme
        // a simple vista si conectó la cuenta correcta (y no, por ejemplo,
        // la misma que usa para comprar al probar).
        Task<MercadoPagoCuentaInfo?> ObtenerInfoCuentaConectadaAsync(Barberia barberia);

        // Borra la conexión (el dueño puede desvincular la cuenta).
        void Desconectar(Barberia barberia);
    }

    public class MercadoPagoCuentaInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("nickname")]
        public string? Nickname { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("first_name")]
        public string? Nombre { get; set; }

        [JsonPropertyName("last_name")]
        public string? Apellido { get; set; }
    }

    public class MercadoPagoTokenService : IMercadoPagoTokenService
    {
        private const string TokenEndpoint = "https://api.mercadopago.com/oauth/token";
        // Colchón de seguridad: renovamos un poco antes de que venza de verdad,
        // para no arriesgarnos a que expire a mitad de una request.
        private static readonly TimeSpan ColchonExpiracion = TimeSpan.FromMinutes(10);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly BarberiaDbContext _context;

        public MercadoPagoTokenService(IHttpClientFactory httpClientFactory, IConfiguration config, BarberiaDbContext context)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _context = context;
        }

        // Cuando está en true (MercadoPago:UsarTokensDePrueba=true en config),
        // MP devuelve un access_token de tipo TEST en vez de APP_USR — el
        // único que puede procesar tarjetas ficticias y saldo de cuentas de
        // prueba. Un token APP_USR (producción real) rechaza cualquier medio
        // de pago de prueba, sin importar si la cuenta conectada es de test.
        // Poné esto en false (o sacalo directamente) cuando conectes
        // barberías reales en producción.
        private bool UsarTokensDePrueba => _config.GetValue<bool>("MercadoPago:UsarTokensDePrueba");

        public async Task IntercambiarCodigoPorTokenAsync(Barberia barberia, string code, string redirectUri)
        {
            var clientId = _config["MercadoPago:ClientId"];
            var clientSecret = _config["MercadoPago:ClientSecret"];

            var http = _httpClientFactory.CreateClient();
            var respuesta = await http.PostAsJsonAsync(TokenEndpoint, new
            {
                client_id = clientId,
                client_secret = clientSecret,
                grant_type = "authorization_code",
                code,
                redirect_uri = redirectUri,
                test_token = UsarTokensDePrueba
            });

            respuesta.EnsureSuccessStatusCode();
            var datos = await respuesta.Content.ReadFromJsonAsync<MercadoPagoTokenResponse>()
                ?? throw new InvalidOperationException("Mercado Pago no devolvió un token válido.");

            GuardarToken(barberia, datos);
            await _context.SaveChangesAsync();
        }

        public async Task<string?> ObtenerAccessTokenValidoAsync(Barberia barberia)
        {
            if (string.IsNullOrEmpty(barberia.MercadoPagoAccessToken)) return null;

            var vigente = barberia.MercadoPagoTokenExpira == null
                || barberia.MercadoPagoTokenExpira.Value > DateTime.UtcNow.Add(ColchonExpiracion);

            if (vigente) return barberia.MercadoPagoAccessToken;

            // Vencido (o por vencer): lo renovamos con el refresh_token.
            var clientId = _config["MercadoPago:ClientId"];
            var clientSecret = _config["MercadoPago:ClientSecret"];

            var http = _httpClientFactory.CreateClient();
            var respuesta = await http.PostAsJsonAsync(TokenEndpoint, new
            {
                client_id = clientId,
                client_secret = clientSecret,
                grant_type = "refresh_token",
                refresh_token = barberia.MercadoPagoRefreshToken,
                test_token = UsarTokensDePrueba
            });

            if (!respuesta.IsSuccessStatusCode)
            {
                // El refresh_token puede haber sido revocado por el dueño desde
                // su cuenta de MP. En ese caso no hay forma de recuperarlo acá:
                // hay que pedirle que vuelva a conectar la cuenta.
                return null;
            }

            var datos = await respuesta.Content.ReadFromJsonAsync<MercadoPagoTokenResponse>();
            if (datos == null) return null;

            GuardarToken(barberia, datos);
            await _context.SaveChangesAsync();

            return barberia.MercadoPagoAccessToken;
        }

        public async Task<MercadoPagoCuentaInfo?> ObtenerInfoCuentaConectadaAsync(Barberia barberia)
        {
            var accessToken = await ObtenerAccessTokenValidoAsync(barberia);
            if (accessToken == null) return null;

            var http = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.mercadopago.com/users/me");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var respuesta = await http.SendAsync(request);
            if (!respuesta.IsSuccessStatusCode) return null;

            return await respuesta.Content.ReadFromJsonAsync<MercadoPagoCuentaInfo>();
        }

        public void Desconectar(Barberia barberia)
        {
            barberia.MercadoPagoUserId = null;
            barberia.MercadoPagoAccessToken = null;
            barberia.MercadoPagoRefreshToken = null;
            barberia.MercadoPagoPublicKey = null;
            barberia.MercadoPagoTokenExpira = null;
        }

        private static void GuardarToken(Barberia barberia, MercadoPagoTokenResponse datos)
        {
            barberia.MercadoPagoAccessToken = datos.AccessToken;
            barberia.MercadoPagoRefreshToken = datos.RefreshToken;
            barberia.MercadoPagoPublicKey = datos.PublicKey;
            barberia.MercadoPagoUserId = datos.UserId.ToString();
            barberia.MercadoPagoTokenExpira = DateTime.UtcNow.AddSeconds(datos.ExpiresIn);
        }
    }
}