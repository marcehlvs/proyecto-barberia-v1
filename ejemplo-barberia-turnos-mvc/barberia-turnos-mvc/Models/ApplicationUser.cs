using Microsoft.AspNetCore.Identity;
using barberia_turnos_mvc.Models;

public class ApplicationUser : IdentityUser
{
    public string? NombreCompleto { get; set; }

    // Solo se usa para usuarios con rol "Dueño".
    // Los usuarios con rol "Cliente" NO tienen barbería propia:
    // su relación con una barbería específica vive en cada registro de Cliente.
    public int? BarberiaId { get; set; }
    public Barberia? Barberia { get; set; }
}
