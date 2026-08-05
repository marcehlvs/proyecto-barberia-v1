// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using barberia_turnos_mvc.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Services;

namespace barberia_turnos_mvc.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly BarberiaDbContext _context;
        private readonly ICurrentBarberiaService _currentBarberia;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            BarberiaDbContext context,
            ICurrentBarberiaService currentBarberia)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
            _currentBarberia = currentBarberia;
        }

        // Slug de la barbería para la que se está registrando este cliente.
        // Viaja por query string (?barberia=elcorte) y se re-envía en el POST
        // vía asp-route-barberia en el <form> de Register.cshtml.
        public string BarberiaSlug { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }


        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            BarberiaSlug = _currentBarberia.Barberia?.Slug;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            var barberia = _currentBarberia.Barberia;
            if (barberia is null)
            {
                ModelState.AddModelError(string.Empty,
                    "No pudimos identificar la barbería para este registro. Volvé a intentar desde el link de tu barbería.");
                return Page();
            }

            // Sin un returnUrl específico (ej: no venías de un link protegido),
            // el destino por defecto es la home de ESTA barbería, no la raíz
            // del sitio ("/"). La raíz redirige a la PRIMERA barbería de la
            // base (ver Program.cs), lo que terminaría mandando a un cliente
            // recién registrado en "barberia-test" a ver el home de "elcorte".
            returnUrl ??= Url.Content($"~/{barberia.Slug}");

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    // Asignar rol Cliente automáticamente
                    await _userManager.AddToRoleAsync(user, "Cliente");

                    // Crear el registro de Cliente vinculado a este usuario Y a esta barbería
                    var cliente = new Cliente
                    {
                        Nombre = Input.Email!.Split('@')[0], // provisorio, lo ajusta después en su perfil
                        Apellido = "",
                        Telefono = "",
                        ApplicationUserId = user.Id,
                        BarberiaId = barberia.Id
                    };
                    _context.Clientes.Add(cliente);
                    await _context.SaveChangesAsync();

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    var htmlMessage = $@"
<div style='font-family: Arial, Helvetica, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f4f4f4; padding: 30px 0;'>
    <div style='background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 6px rgba(0,0,0,0.08);'>

        <div style='background-color: #1a1a1a; padding: 32px 24px; text-align: center;'>
            <h1 style='color: #ffffff; margin: 0; font-size: 24px; letter-spacing: 1px;'>
                💈 {barberia.Nombre.ToUpper()}
            </h1>
        </div>

        <div style='padding: 32px 24px;'>
            <h2 style='color: #1a1a1a; margin-top: 0; font-size: 20px;'>
                ¡Bienvenido!
            </h2>
            <p style='color: #444444; font-size: 15px; line-height: 1.6;'>
                Gracias por registrarte en <strong>{barberia.Nombre}</strong>. Para empezar a reservar tus turnos, primero necesitamos que confirmes tu dirección de email.
            </p>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'
                   style='background-color: #c9a227; color: #1a1a1a; padding: 14px 32px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 15px; display: inline-block;'>
                    Confirmar mi cuenta
                </a>
            </div>

            <p style='color: #888888; font-size: 13px; line-height: 1.5;'>
                Si el botón no funciona, copiá y pegá este link en tu navegador:<br>
                <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style='color: #c9a227; word-break: break-all;'>{HtmlEncoder.Default.Encode(callbackUrl)}</a>
            </p>

            <hr style='border: none; border-top: 1px solid #eeeeee; margin: 28px 0;'>

            <p style='color: #aaaaaa; font-size: 12px; line-height: 1.5;'>
                Si no creaste esta cuenta, podés ignorar este mensaje con total tranquilidad.
            </p>
        </div>

        <div style='background-color: #f0f0f0; padding: 16px 24px; text-align: center;'>
            <p style='color: #999999; font-size: 11px; margin: 0;'>
                © {DateTime.Now.Year} {barberia.Nombre} · Todos los derechos reservados
            </p>
        </div>

    </div>
</div>";
                    try
                    {
                        await _emailSender.SendEmailAsync(Input.Email, $"Confirmá tu cuenta - {barberia.Nombre}", htmlMessage);
                    }
                    catch
                    {

                    }
                    
                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}