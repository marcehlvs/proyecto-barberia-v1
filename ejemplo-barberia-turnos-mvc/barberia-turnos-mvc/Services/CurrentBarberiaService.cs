using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;

namespace barberia_turnos_mvc.Services
{
    public class CurrentBarberiaService : ICurrentBarberiaService
    {
        public Barberia? Barberia { get; private set; }

        // El middleware (CurrentBarberiaMiddleware) es quien llama a este método,
        // una sola vez por request, apenas se resuelve la ruta.
        public void Establecer(Barberia? barberia)
        {
            Barberia = barberia;
        }

        public Barberia GetRequerida()
        {
            if (Barberia is null)
            {
                throw new InvalidOperationException(
                    "Se requiere una barbería en el contexto de este request, pero no se pudo resolver desde la URL. " +
                    "Verificá que la ruta incluya un {barberiaSlug} válido.");
            }

            return Barberia;
        }
    }
}
