using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Helpers
{
    public class PaginatedList<T> : List<T>
    {
        public int PaginaActual { get; private set; }
        public int TotalPaginas { get; private set; }

        public PaginatedList(List<T> items, int totalItems, int paginaActual, int tamañoPagina)
        {
            PaginaActual = paginaActual;
            TotalPaginas = (int)Math.Ceiling(totalItems / (double)tamañoPagina);
            AddRange(items);
        }

        public bool TienePaginaAnterior => PaginaActual > 1;
        public bool TienePaginaSiguiente => PaginaActual < TotalPaginas;

        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int paginaActual, int tamañoPagina)
        {
            var totalItems = await source.CountAsync();
            var items = await source
                .Skip((paginaActual - 1) * tamañoPagina)
                .Take(tamañoPagina)
                .ToListAsync();

            return new PaginatedList<T>(items, totalItems, paginaActual, tamañoPagina);
        }
    }
}