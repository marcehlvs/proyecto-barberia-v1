// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using barberia_turnos_mvc.Services;

namespace barberia_turnos_mvc.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ICurrentBarberiaService _currentBarberia;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender, ICurrentBarberiaService currentBarberia)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _currentBarberia = currentBarberia;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        // Slug de la barbería, para retenerlo en el link "Volver a Ingresar".
        public string BarberiaSlug { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public void OnGet()
        {
            BarberiaSlug = _currentBarberia.Barberia?.Slug;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            BarberiaSlug = _currentBarberia.Barberia?.Slug;
            var nombreBarberia = _currentBarberia.Barberia?.Nombre ?? "tu barbería";

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToPage("./ForgotPasswordConfirmation", new { barberia = BarberiaSlug });
                }

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code, barberia = BarberiaSlug },
                    protocol: Request.Scheme);

                var htmlMessage = $@"
<div style='font-family: Arial, Helvetica, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f4f4f4; padding: 30px 0;'>
    <div style='background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 6px rgba(0,0,0,0.08);'>

        <div style='background-color: #1a1a1a; padding: 32px 24px; text-align: center;'>
            <h1 style='color: #ffffff; margin: 0; font-size: 24px; letter-spacing: 1px;'>
                💈 {nombreBarberia.ToUpper()}
            </h1>
        </div>

        <div style='padding: 32px 24px;'>
            <h2 style='color: #1a1a1a; margin-top: 0; font-size: 20px;'>
                Restablecer tu contraseña
            </h2>
            <p style='color: #444444; font-size: 15px; line-height: 1.6;'>
                Recibimos un pedido para restablecer la contraseña de tu cuenta en <strong>{nombreBarberia}</strong>. Si fuiste vos, hacé click en el botón de abajo.
            </p>

            <div style='text-align: center; margin: 32px 0;'>
                <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'
                   style='background-color: #c9a227; color: #1a1a1a; padding: 14px 32px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 15px; display: inline-block;'>
                    Restablecer contraseña
                </a>
            </div>

            <p style='color: #888888; font-size: 13px; line-height: 1.5;'>
                Si el botón no funciona, copiá y pegá este link en tu navegador:<br>
                <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style='color: #c9a227; word-break: break-all;'>{HtmlEncoder.Default.Encode(callbackUrl)}</a>
            </p>

            <hr style='border: none; border-top: 1px solid #eeeeee; margin: 28px 0;'>

            <p style='color: #aaaaaa; font-size: 12px; line-height: 1.5;'>
                Si no pediste este cambio, podés ignorar este mensaje con total tranquilidad — tu contraseña actual sigue siendo válida.
            </p>
        </div>

        <div style='background-color: #f0f0f0; padding: 16px 24px; text-align: center;'>
            <p style='color: #999999; font-size: 11px; margin: 0;'>
                © {DateTime.Now.Year} {nombreBarberia} · Todos los derechos reservados
            </p>
        </div>

    </div>
</div>";

                await _emailSender.SendEmailAsync(Input.Email, $"Restablecer tu contraseña - {nombreBarberia}", htmlMessage);

                return RedirectToPage("./ForgotPasswordConfirmation", new { barberia = BarberiaSlug });
            }

            return Page();
        }
    }
}
