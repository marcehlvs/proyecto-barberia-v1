#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;

namespace barberia_turnos_mvc.Areas.Identity.Pages.Account
{
    // A diferencia de Register (que asume que la barbería ya existe),
    // esta página CREA una barbería nueva junto con su primer usuario Dueño.
    // No depende de ICurrentBarberiaService: no hay ninguna barbería todavía
    // en el momento en que alguien llega acá.
    [AllowAnonymous]
    public class RegisterDuenoModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly BarberiaDbContext _context;

        public RegisterDuenoModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender,
            BarberiaDbContext context)
        {
            _userManager = userManager;
            _userStore = userStore;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Ingresá el nombre de tu barbería.")]
            [Display(Name = "Nombre de la barbería")]
            [MaxLength(100)]
            public string NombreBarberia { get; set; }

            [Required(ErrorMessage = "Elegí un identificador para tu URL.")]
            [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Solo minúsculas, números y guiones (sin espacios ni acentos).")]
            [MaxLength(60)]
            [Display(Name = "Identificador en la URL")]
            public string Slug { get; set; }

            [Display(Name = "Dirección")]
            [MaxLength(150)]
            public string Direccion { get; set; }

            [Display(Name = "Teléfono")]
            [MaxLength(30)]
            public string Telefono { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Tu email (para ingresar al panel)")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar contraseña")]
            [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
            public string ConfirmPassword { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var slugNormalizado = Input.Slug?.Trim().ToLower();
            Input.Slug = slugNormalizado;

            var slugYaExiste = await _context.Barberias.AnyAsync(b => b.Slug == slugNormalizado);
            if (slugYaExiste)
            {
                ModelState.AddModelError("Input.Slug", "Ese identificador ya está en uso por otra barbería. Probá con otro.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // 1) Crear la barbería
            var barberia = new Barberia
            {
                Nombre = Input.NombreBarberia.Trim(),
                Slug = slugNormalizado,
                Direccion = Input.Direccion?.Trim() ?? "",
                Telefono = Input.Telefono?.Trim() ?? ""
                // PorcentajeSeña, MinutosEntreTurnos, HoraApertura, HoraCierre
                // quedan con los valores por defecto del modelo; el dueño los
                // ajusta después desde "Configuración".
            };
            _context.Barberias.Add(barberia);
            await _context.SaveChangesAsync();

            // 2) Crear el usuario Dueño, vinculado a esa barbería
            var user = CreateUser();
            user.BarberiaId = barberia.Id;
            user.NombreCompleto = Input.NombreBarberia.Trim();

            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await ((IUserEmailStore<ApplicationUser>)_userStore).SetEmailAsync(user, Input.Email, CancellationToken.None);

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (!result.Succeeded)
            {
                // Si falla la creación del usuario, no dejamos una barbería huérfana.
                _context.Barberias.Remove(barberia);
                await _context.SaveChangesAsync();

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // 3) Asegurar que exista el rol "Dueño" y asignárselo
            if (!await _roleManager.RoleExistsAsync("Dueño"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Dueño"));
            }
            await _userManager.AddToRoleAsync(user, "Dueño");

            // 4) Email de bienvenida (mismo estilo que el de clientes)
            var htmlMessage = $@"
<div style='font-family: Arial, Helvetica, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f4f4f4; padding: 30px 0;'>
    <div style='background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 6px rgba(0,0,0,0.08);'>
        <div style='background-color: #1a1a1a; padding: 32px 24px; text-align: center;'>
            <h1 style='color: #ffffff; margin: 0; font-size: 24px; letter-spacing: 1px;'>💈 {barberia.Nombre.ToUpper()}</h1>
        </div>
        <div style='padding: 32px 24px;'>
            <h2 style='color: #1a1a1a; margin-top: 0; font-size: 20px;'>¡Bienvenido a bordo!</h2>
            <p style='color: #444444; font-size: 15px; line-height: 1.6;'>
                Tu barbería ya está lista. Podés acceder a tu panel de administración,
                cargar tus servicios, y empezar a recibir turnos en:
            </p>
            <p style='text-align:center; margin: 24px 0;'>
                <a href='https://tudominio.com/{barberia.Slug}' style='color:#c9a227; font-weight:bold;'>tudominio.com/{barberia.Slug}</a>
            </p>
        </div>
        <div style='background-color: #f0f0f0; padding: 16px 24px; text-align: center;'>
            <p style='color: #999999; font-size: 11px; margin: 0;'>© {DateTime.Now.Year} {barberia.Nombre} · Todos los derechos reservados</p>
        </div>
    </div>
</div>";

            try
            {
                await _emailSender.SendEmailAsync(Input.Email, $"¡Bienvenido a {barberia.Nombre}!", htmlMessage);
            }
            catch
            {
                // No bloqueamos el alta si el email falla; el dueño ya puede entrar igual.
            }

            // 5) Firmar sesión directo (RequireConfirmedAccount está en false) y mandarlo a su panel
            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect($"/{barberia.Slug}/Dashboard");
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"No se pudo crear una instancia de '{nameof(ApplicationUser)}'.");
            }
        }
    }
}
