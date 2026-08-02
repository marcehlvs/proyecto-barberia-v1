using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace barberia_turnos_mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;

        public HomeController(BarberiaDbContext context, ICurrentBarberiaService currentBarberia)
        {
            _context = context;
            _currentBarberia = currentBarberia;
        }

        public async Task<IActionResult> Index()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;

            var barberia = await _context.Barberias
                .Include(b => b.Servicios)
                .FirstOrDefaultAsync(b => b.Id == barberiaId);

            return View(barberia);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // Rutas fijas (sin slug) con atributo explícito: estas páginas no
        // dependen de ninguna barbería, así que no pueden colgar de la
        // ruta convencional "{barberiaSlug}/{controller}/{action}".
        // Usar [Route] en vez de una MapControllerRoute genérica evita que
        // esta ruta compita con la generación de links normales de
        // Home/Index (que sí necesitan el slug en el path).
        [Route("Home/Error/{statusCode?}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            if (statusCode.HasValue)
            {
                ViewData["StatusCode"] = statusCode.Value;
            }
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Route("Home/AccesoDenegado")]
        public IActionResult AccesoDenegado()
        {
            return View();
        }

    }
}
