using Microsoft.AspNetCore.Identity.UI.Services;
using MailKit.Net.Smtp;
using MimeKit;

namespace barberia_turnos_mvc.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(MailboxAddress.Parse(_config["Email:RemitenteEmail"]));
            mensaje.To.Add(MailboxAddress.Parse(email));
            mensaje.Subject = subject;
            mensaje.Body = new TextPart("html") { Text = htmlMessage };

            using var cliente = new SmtpClient();
            await cliente.ConnectAsync(
                _config["Email:SmtpHost"],
                int.Parse(_config["Email:SmtpPort"]!),
                MailKit.Security.SecureSocketOptions.StartTls);

            await cliente.AuthenticateAsync(_config["Email:SmtpUser"], _config["Email:SmtpPassword"]);
            await cliente.SendAsync(mensaje);
            await cliente.DisconnectAsync(true);
        }
    }
}