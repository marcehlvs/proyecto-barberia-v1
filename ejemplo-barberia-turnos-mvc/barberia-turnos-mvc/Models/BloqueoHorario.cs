using barberia_turnos_mvc.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace barberia_turnos_mvc.Models
{
    public class BloqueoHorario
    {
        public int Id { get; set; }

        public int BarberiaId { get; set; }
        [ValidateNever]
        public Barberia Barberia { get; set; } = null!;
        [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
        public DateTime FechaInicio { get; set; }
        [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
        public DateTime FechaFin { get; set; }
        [StringLength(100, ErrorMessage = "El motivo no puede superar los 100 caracteres.")]

        public string? Motivo { get; set; }
    }
}
