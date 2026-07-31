using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace barberia_turnos_mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly BarberiaDbContext _context;

        public HomeController(BarberiaDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            var barberia = _context.Barberias
            .Include(b => b.Servicios)
            .FirstOrDefault();

            return View(barberia);
        }

        public IActionResult Privacy()
        {
            return View();
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            if (statusCode.HasValue)
            {
                ViewData["StatusCode"] = statusCode.Value;
            }
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult AccesoDenegado()
        {
            return View();
        }

    }
}
