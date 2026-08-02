using System.ComponentModel.DataAnnotations;

namespace barberia_turnos_mvc.Models
{
 // Barberia.cs (por ahora representa el único negocio/barbero)
 public class Barberia
  {
   public int Id { get; set; }
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

   public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
   public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
   public ICollection<BloqueoHorario> Bloqueos { get; set; } = new List<BloqueoHorario>();
   public ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();

   [Range(0, 60, ErrorMessage = "El colchón debe estar entre 0 y 60 minutos.")]
   public int MinutosEntreTurnos { get; set; } = 10;

   public TimeSpan HoraApertura { get; set; } = new TimeSpan(10, 0, 0);
   public TimeSpan HoraCierre { get; set; } = new TimeSpan(20, 0, 0);

    }
}
