using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace OMS_Backend.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string firstName, string resetLink);
    }

    // Built-in System.Net.Mail — no third-party mail package needed
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string firstName, string resetLink)
        {
            var smtpSettings = _config.GetSection("SmtpSettings");

            using var message = new MailMessage
            {
                From = new MailAddress(smtpSettings["FromEmail"], smtpSettings["FromName"]),
                Subject = "Reset your OMS password",
                IsBodyHtml = true,
                Body = $@"
                    <div style='font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:auto'>
                        <h2 style='color:#1f2937'>Password reset request</h2>
                        <p>Hi {firstName},</p>
                        <p>We received a request to reset your Order Management System password.
                           This link is valid for 30 minutes.</p>
                        <p style='margin:24px 0'>
                            <a href='{resetLink}'
                               style='background:#1f2937;color:#fff;padding:10px 20px;
                                      border-radius:8px;text-decoration:none;display:inline-block'>
                                Reset Password
                            </a>
                        </p>
                        <p>If you didn't request this, you can safely ignore this email.</p>
                    </div>"
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(smtpSettings["Host"], int.Parse(smtpSettings["Port"]))
            {
                Credentials = new NetworkCredential(smtpSettings["Username"], smtpSettings["Password"]),
                EnableSsl = bool.Parse(smtpSettings["EnableSsl"] ?? "true"),
            };

            await client.SendMailAsync(message);
        }
    }
}