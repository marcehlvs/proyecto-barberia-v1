using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace barberia_turnos_mvc.Models
{
 // Barberia.cs (por ahora representa el único negocio/barbero)
 public class Barberia
  {
   public int Id { get; set; }

   [Required(ErrorMessage = "El nombre de la barbería es obligatorio.")]
   public string Nombre { get; set; } = string.Empty;
   public string Direccion { get; set; } = string.Empty;
   public string Telefono { get; set; } = string.Empty;

   // Identificador único en la URL, ej: "elcorte" -> tuapp.com/elcorte/turnos
   // Solo minúsculas, números y guiones. Se genera a partir del Nombre y se puede editar.
   [Required]
   [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Solo minúsculas, números y guiones.")]
   [MaxLength(60)]
   public string Slug { get; set; } = string.Empty;

   [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
   public decimal PorcentajeSeña { get; set; } = 30;

   // --- Sección "Nosotros" de la página pública ---
   // Ambos son opcionales a propósito: mientras una barbería no cargue su
   // propia Descripcion, el index no debe mostrar un bloque "Nosotros"
   // vacío o con contenido genérico que le reste confianza al visitante
   // (ver Home/Index.cshtml, que solo renderiza la sección si hay texto).
   [MaxLength(600, ErrorMessage = "La descripción no puede superar los 600 caracteres.")]
   public string? Descripcion { get; set; }

   // URLs de las fotos para el slide, una por línea (se cargan y se
   // separan en el formulario de Configuración). Guardadas como texto
   // plano en vez de una tabla aparte: para las 3-5 fotos que necesita
   // una barbería chica, una tabla extra con su propio CRUD es más
   // complejidad de la que vale la pena mantener por ahora.
   [MaxLength(2000)]
   public string? FotosUrls { get; set; }

   [NotMapped]
   public List<string> Fotos =>
       (FotosUrls ?? string.Empty)
           .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .ToList();

   public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
   public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
   public ICollection<BloqueoHorario> Bloqueos { get; set; } = new List<BloqueoHorario>();
   public ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();

   [Range(0, 60, ErrorMessage = "El colchón debe estar entre 0 y 60 minutos.")]
   public int MinutosEntreTurnos { get; set; } = 10;

   public TimeSpan HoraApertura { get; set; } = new TimeSpan(10, 0, 0);
   public TimeSpan HoraCierre { get; set; } = new TimeSpan(20, 0, 0);

   // --- Conexión Mercado Pago (OAuth marketplace) ---
   // Cada barbería conecta SU PROPIA cuenta de Mercado Pago. Las señas de sus
   // turnos se cobran con estas credenciales, no con la cuenta global de la app.
   // Null mientras la barbería no haya conectado su cuenta todavía.
   public string? MercadoPagoUserId { get; set; }
   public string? MercadoPagoAccessToken { get; set; }
   public string? MercadoPagoRefreshToken { get; set; }
   public string? MercadoPagoPublicKey { get; set; }
   public DateTime? MercadoPagoTokenExpira { get; set; }

   // Conveniencia: true si la barbería ya conectó su cuenta de MP.
   public bool TieneMercadoPagoConectado => !string.IsNullOrEmpty(MercadoPagoAccessToken);

    }
}
