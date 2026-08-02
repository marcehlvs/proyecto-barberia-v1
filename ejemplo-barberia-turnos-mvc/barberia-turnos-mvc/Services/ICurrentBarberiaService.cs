using barberia_turnos_mvc.Models;

namespace barberia_turnos_mvc.Services
{
    // Resuelve, para el request actual, a qué Barberia pertenece
    // según el segmento {barberiaSlug} de la URL (ej: /elcorte/Turno/Index).
    // Se registra como Scoped: una instancia por request, calculada una sola vez.
    public interface ICurrentBarberiaService
    {
        // Null si la URL no tiene slug, o si el slug no coincide con ninguna barbería.
        Barberia? Barberia { get; }

        // Lanza si Barberia es null. Usar cuando la acción REQUIERE una barbería válida.
        Barberia GetRequerida();
    }
}
