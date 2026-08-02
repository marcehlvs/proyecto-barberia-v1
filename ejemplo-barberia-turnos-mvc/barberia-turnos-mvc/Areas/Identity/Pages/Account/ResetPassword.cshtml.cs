#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using barberia_turnos_mvc.Services;

namespace barberia_turnos_mvc.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICurrentBarberiaService _currentBarberia;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager, ICurrentBarberiaService currentBarberia)
        {
            _userManager = userManager;
            _currentBarberia = currentBarberia;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        // Slug de la barbería, para retenerlo en el link de vuelta a Ingresar
        // y en el redirect final a ResetPasswordConfirmation.
        public string BarberiaSlug { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar contraseña")]
            [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Code { get; set; }
        }

        public IActionResult OnGet(string code = null)
        {
            BarberiaSlug = _currentBarberia.Barberia?.Slug;

            if (code == null)
            {
                return BadRequest("Falta un código para restablecer la contraseña.");
            }

            Input = new InputModel
            {
                Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code))
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            BarberiaSlug = _currentBarberia.Barberia?.Slug;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // No revelamos que el usuario no existe.
                return RedirectToPage("./ResetPasswordConfirmation", new { barberia = BarberiaSlug });
            }

            var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                return RedirectToPage("./ResetPasswordConfirmation", new { barberia = BarberiaSlug });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}
