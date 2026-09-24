using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace TimeTamer.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailSettings> options, ILogger<SmtpEmailSender> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (!_settings.IsConfigured) return false;

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(_settings.FromEmail, _settings.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                using var client = new SmtpClient(_settings.Host, _settings.Port)
                {
                    EnableSsl = _settings.EnableSsl
                };

                if (!string.IsNullOrWhiteSpace(_settings.Username))
                {
                    client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
                }

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send email to {Email}", toEmail);
                return false;
            }
        }
    }
}
