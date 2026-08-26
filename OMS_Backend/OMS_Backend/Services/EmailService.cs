using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace OMS_Backend.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(
            string toEmail,
            string firstName,
            string resetLink);

        Task SendAccountActivationEmailAsync(
            string toEmail,
            string firstName);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        private SmtpClient CreateSmtpClient()
        {
            var smtpSettings = _config.GetSection("SmtpSettings");

            return new SmtpClient(
                smtpSettings["Host"],
                int.Parse(smtpSettings["Port"] ?? "587"))
            {
                Credentials = new NetworkCredential(
                    smtpSettings["Username"],
                    smtpSettings["Password"]),

                EnableSsl = bool.Parse(
                    smtpSettings["EnableSsl"] ?? "true")
            };
        }

        public async Task SendPasswordResetEmailAsync(
            string toEmail,
            string firstName,
            string resetLink)
        {
            var smtpSettings = _config.GetSection("SmtpSettings");

            using var message = new MailMessage
            {
                From = new MailAddress(
                    smtpSettings["FromEmail"],
                    smtpSettings["FromName"]),

                Subject = "Reset your OMS password",

                IsBodyHtml = true,

                Body = $@"
                    <div style='font-family:Segoe UI,Arial,sans-serif;
                                max-width:480px;
                                margin:auto;
                                color:#374151'>

                        <h2 style='color:#1f2937'>
                            Password reset request
                        </h2>

                        <p>Hi {WebUtility.HtmlEncode(firstName)},</p>

                        <p>
                            We received a request to reset your
                            Order Management System password.
                            This link is valid for 30 minutes.
                        </p>

                        <p style='margin:24px 0'>
                            <a href='{WebUtility.HtmlEncode(resetLink)}'
                               style='background:#1f2937;
                                      color:#fff;
                                      padding:10px 20px;
                                      border-radius:8px;
                                      text-decoration:none;
                                      display:inline-block'>
                                Reset Password
                            </a>
                        </p>

                        <p>
                            If you didn't request this,
                            you can safely ignore this email.
                        </p>

                        <p style='margin-top:30px'>
                            Regards,<br/>
                            <strong>Areena Design OMS</strong>
                        </p>
                    </div>"
            };

            message.To.Add(toEmail);

            using var client = CreateSmtpClient();

            await client.SendMailAsync(message);
        }

        public async Task SendAccountActivationEmailAsync(
            string toEmail,
            string firstName)
        {
            var smtpSettings = _config.GetSection("SmtpSettings");

            using var message = new MailMessage
            {
                From = new MailAddress(
                    smtpSettings["FromEmail"],
                    smtpSettings["FromName"]),

                Subject = "Your OMS account has been activated",

                IsBodyHtml = true,

                Body = $@"
                    <div style='font-family:Segoe UI,Arial,sans-serif;
                                max-width:520px;
                                margin:auto;
                                color:#374151'>

                        <div style='
                            background:#10b981;
                            color:white;
                            padding:18px 24px;
                            border-radius:12px 12px 0 0;'>

                            <h2 style='margin:0'>
                                Account Activated
                            </h2>
                        </div>

                        <div style='
                            border:1px solid #e5e7eb;
                            border-top:none;
                            padding:28px 24px;
                            border-radius:0 0 12px 12px;'>

                            <p>
                                Hi <strong>
                                    {WebUtility.HtmlEncode(firstName)}
                                </strong>,
                            </p>

                            <p>
                                Your account for the
                                <strong>Order Management System</strong>
                                has been successfully activated by an administrator.
                            </p>

                            <p>
                                You can now log in and access the system
                                using your registered email address.
                            </p>

                            <p style='margin:28px 0'>
                                <a href='http://localhost:4200/login'
                                   style='background:#1f2937;
                                          color:#ffffff;
                                          padding:11px 22px;
                                          border-radius:8px;
                                          text-decoration:none;
                                          display:inline-block;
                                          font-weight:600'>
                                    Login to OMS
                                </a>
                            </p>

                            <p style='font-size:13px;color:#6b7280'>
                                If you did not expect this change,
                                please contact your system administrator.
                            </p>

                            <p style='margin-top:28px'>
                                Regards,<br/>
                                <strong>Areena Design OMS</strong>
                            </p>
                        </div>
                    </div>"
            };

            message.To.Add(toEmail);

            using var client = CreateSmtpClient();

            await client.SendMailAsync(message);
        }
    }
}