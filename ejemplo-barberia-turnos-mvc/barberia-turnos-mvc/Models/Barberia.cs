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

   [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
   public decimal PorcentajeSeña { get; set; } = 30;

   public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
   public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
   public ICollection<BloqueoHorario> Bloqueos { get; set; } = new List<BloqueoHorario>();
  }
}
