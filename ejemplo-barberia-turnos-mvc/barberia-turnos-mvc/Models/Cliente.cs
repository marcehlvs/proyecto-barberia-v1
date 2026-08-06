using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace barberia_turnos_mvc.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50, ErrorMessage = "El nombre no puede superar los 50 caracteres.")]
        public string Nombre { get; set; } =string.Empty;
        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [StringLength(50, ErrorMessage = "El apellido no puede superar los 50 caracteres.")]
        public string Apellido { get; set; } = string.Empty;
        
        [Phone(ErrorMessage = "Ingresá un número de teléfono válido.")]
        [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
        public string?  Telefono { get; set; }

        public string? ApplicationUserId { get; set; }
        [ValidateNever]
        public ApplicationUser? ApplicationUser { get; set; }

        public int BarberiaId { get; set; }
        [ValidateNever]
        public Barberia Barberia { get; set; } = null!;

        [NotMapped]
        public string NombreCompleto => $"{Nombre} {Apellido}";
        public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
        
    }
}




