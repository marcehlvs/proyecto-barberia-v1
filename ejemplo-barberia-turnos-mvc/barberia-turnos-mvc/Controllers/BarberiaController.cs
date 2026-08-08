using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using barberia_turnos_mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace barberia_turnos_mvc.Controllers
{
    [Authorize(Roles = "Dueño")]
    public class BarberiaController : Controller
    {
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;
        private readonly IMercadoPagoTokenService _mpTokenService;

        public BarberiaController(BarberiaDbContext context, ICurrentBarberiaService currentBarberia, IMercadoPagoTokenService mpTokenService)
        {
            _context = context;
            _currentBarberia = currentBarberia;
            _mpTokenService = mpTokenService;
        }

        public async Task<IActionResult> Configuracion()
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;
            var barberia = await _context.Barberias.FirstOrDefaultAsync(b => b.Id == barberiaId);
            if (barberia == null) return NotFound();

            if (barberia.TieneMercadoPagoConectado)
            {
                ViewData["MpCuentaConectada"] = await _mpTokenService.ObtenerInfoCuentaConectadaAsync(barberia);
            }

            return View(barberia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Configuracion([Bind("Id,Nombre,Direccion,Telefono,PorcentajeSeña,MinutosEntreTurnos,HoraApertura,HoraCierre,Descripcion,FotosUrls")] Barberia form)
        {
            var barberiaId = _currentBarberia.GetRequerida().Id;

            // Que el Dueño solo pueda editar SU PROPIA barbería, nunca otra por ID a mano.
            if (form.Id != barberiaId)
            {
                return Forbid();
            }

            // El Slug no viene en el Bind (no se edita desde este form), así que
            // el model binder lo valida como vacío (falla el [Required]) antes de
            // que lleguemos acá, y ese error queda pegado en ModelState. Sin este
            // Remove, ModelState.IsValid da false siempre.
            ModelState.Remove(nameof(form.Slug));

            if (!ModelState.IsValid)
            {
                return View(form);
            }

            // Cargamos la entidad TRACKEADA y pisamos solo los campos del form,
            // en vez de _context.Update(form) (reemplazar la fila entera con el
            // objeto armado desde el Bind). Reemplazar la fila entera es lo que
            // mandó Direccion en null a la base: cualquier propiedad que no haya
            // llegado bien desde el POST se guarda tal cual quedó en memoria,
            // pisando lo que ya había. Cargar-y-mutar evita esa clase de bug
            // aunque algún campo puntual falle en bindear.
            var barberia = await _context.Barberias.FirstOrDefaultAsync(b => b.Id == barberiaId);
            if (barberia == null) return NotFound();

            barberia.Nombre = form.Nombre;
            barberia.Direccion = form.Direccion;
            barberia.Telefono = form.Telefono;
            barberia.PorcentajeSeña = form.PorcentajeSeña;
            barberia.MinutosEntreTurnos = form.MinutosEntreTurnos;
            barberia.HoraApertura = form.HoraApertura;
            barberia.HoraCierre = form.HoraCierre;
            barberia.Descripcion = form.Descripcion;
            barberia.FotosUrls = form.FotosUrls;
            // Slug: intencionalmente no se toca, no es parte de este form.

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Configuracion));
        }
    }
}