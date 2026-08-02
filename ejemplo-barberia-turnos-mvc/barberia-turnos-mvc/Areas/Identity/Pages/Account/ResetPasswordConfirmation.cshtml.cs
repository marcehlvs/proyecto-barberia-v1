#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using barberia_turnos_mvc.Services;

namespace barberia_turnos_mvc.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResetPasswordConfirmationModel : PageModel
    {
        private readonly ICurrentBarberiaService _currentBarberia;

        public ResetPasswordConfirmationModel(ICurrentBarberiaService currentBarberia)
        {
            _currentBarberia = currentBarberia;
        }

        public string BarberiaSlug { get; set; }

        public void OnGet()
        {
            BarberiaSlug = _currentBarberia.Barberia?.Slug;
        }
    }
}
