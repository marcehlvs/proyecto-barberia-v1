using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace barberia_turnos_mvc.Models
{
    public enum EstadoTurno
    { 
        Pendiente, 
        Confirmado, 
        Cancelado, 
        Completado,
        [Display(Name = "No se presentó")]
        NoShow 
    }

    public class Turno
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "La fecha y hora del turno son obligatorias.")]

        public DateTime FechaHora { get; set; }
        public EstadoTurno Estado { get; set; }

        public int ClienteId { get; set; }
        [ValidateNever]
        public Cliente Cliente { get; set; } = null!;

        public int BarberiaId { get; set; }
        [ValidateNever]
        public Barberia Barberia { get; set; } = null!;

        public int ServicioId { get; set; }
        [ValidateNever]
        public Servicio Servicio { get; set; } = null!;

        // Nuevo: datos de la seña
        public decimal? MontoSeña { get; set; }
        public bool SeñaPagada { get; set; } = false;
        public string? MercadoPagoPreferenceId { get; set; }
        public string? MercadoPagoPaymentId { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

    }
}
