using barberia_turnos_mvc.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.ViewComponents
{
    public class FooterInfoViewComponent : ViewComponent
    {
        private readonly BarberiaDbContext _context;

        public FooterInfoViewComponent(BarberiaDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var barberia = await _context.Barberias.FirstOrDefaultAsync();
            return View(barberia);
        }
    }
}