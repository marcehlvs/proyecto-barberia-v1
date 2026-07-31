using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace barberia_turnos_mvc.Models
{
    public class Servicio
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
        [StringLength(60, ErrorMessage = "El nombre no puede superar los 60 caracteres.")]
        public string Nombre { get; set; } = string.Empty;
        [Range(0.01, 999999.99, ErrorMessage = "El precio debe ser mayor a 0.")]
        public decimal Precio { get; set; }

        [Range(5, 240, ErrorMessage = "La duración debe estar entre 5 y 240 minutos.")]
        public int DuracionMinutos { get; set; }

        public int BarberiaId { get; set; }

        [ValidateNever]
        public Barberia Barberia { get; set; } = null!;

    }
}

