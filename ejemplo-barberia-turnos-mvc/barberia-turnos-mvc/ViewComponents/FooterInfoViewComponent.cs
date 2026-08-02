using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.ViewComponents
{
    public class FooterInfoViewComponent : ViewComponent
    {
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;

        public FooterInfoViewComponent(BarberiaDbContext context, ICurrentBarberiaService currentBarberia)
        {
            _context = context;
            _currentBarberia = currentBarberia;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var barberiaActual = _currentBarberia.Barberia;
            if (barberiaActual == null)
            {
                return View(null as barberia_turnos_mvc.Models.Barberia);
            }

            var barberia = await _context.Barberias
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == barberiaActual.Id);

            return View(barberia);
        }
    }
}
