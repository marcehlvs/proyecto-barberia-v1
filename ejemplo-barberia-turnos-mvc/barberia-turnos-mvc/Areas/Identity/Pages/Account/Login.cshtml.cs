// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Services;

namespace barberia_turnos_mvc.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly ICurrentBarberiaService _currentBarberia;
        private readonly BarberiaDbContext _context;

        public LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger, ICurrentBarberiaService currentBarberia, BarberiaDbContext context)
        {
            _signInManager = signInManager;
            _logger = logger;
            _currentBarberia = currentBarberia;
            _context = context;
        }

        // Slug de la barbería desde la que se entró a este login, para retenerlo
        // en el link "Registrate" y en el propio POST.
        public string BarberiaSlug { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            BarberiaSlug = _currentBarberia.Barberia?.Slug;

            // Si no vino un returnUrl específico (ej: no es un redirect desde
            // una página protegida), el default es la home de ESTA barbería,
            // no la raíz del sitio. Esto se calcula ACÁ, antes de que
            // Login.cshtml lo vuelque en el <input asp-for="ReturnUrl" hidden />
            // del formulario: si lo dejáramos en "~/" acá, ese valor quedaría
            // "horneado" en el campo oculto y llegaría a OnPostAsync como si
            // fuera un valor explícito, saltándose cualquier default que
            // pongamos ahí.
            returnUrl ??= !string.IsNullOrEmpty(BarberiaSlug) ? Url.Content($"~/{BarberiaSlug}") : Url.Content("~/");

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            var slugActual = _currentBarberia.Barberia?.Slug;
            BarberiaSlug = slugActual;   // 👈 evita que el form/links pierdan el slug si hay que volver a mostrar la página
            var returnUrlGenerico = !string.IsNullOrEmpty(slugActual) ? Url.Content($"~/{slugActual}") : Url.Content("~/");
            returnUrl ??= returnUrlGenerico;

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                    // Si el returnUrl es el genérico (no vino de un link a una
                    // página protegida puntual), y es un Dueño, lo mandamos
                    // directo a SU panel en vez de a la home de la barbería.
                    if (returnUrl == returnUrlGenerico)
                    {
                        var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);
                        if (user != null && user.BarberiaId.HasValue && await _signInManager.UserManager.IsInRoleAsync(user, "Dueño"))
                        {
                            var barberiaDelDueno = await _context.Barberias
                                .AsNoTracking()
                                .FirstOrDefaultAsync(b => b.Id == user.BarberiaId.Value);

                            if (barberiaDelDueno != null)
                            {
                                return LocalRedirect($"/{barberiaDelDueno.Slug}/Dashboard");
                            }
                        }
                    }

                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}