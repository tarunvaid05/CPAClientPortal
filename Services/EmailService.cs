using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace JyotiIyerCPA.Services
{
    public interface IEmailSender
    {
        Task SendInviteEmail(string toEmail, string link);
        Task SendEmailAsync(string email, string subject, string htmlMessage);
        Task SendPasswordResetEmailAsync(string email, string resetLink);
    }

    public class EmailService : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly IHostEnvironment _env;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IHostEnvironment env)
        {
            _configuration = configuration;
            _logger = logger;
            _env = env;
        }

        public async Task SendInviteEmail(string toEmail, string link)
        {
            var opId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("[Email Invite:{OpId}] Preparing invite email. To={To} Env={Env}", opId, toEmail, _env.EnvironmentName);
            if (_env.IsDevelopment())
            {
                _logger.LogDebug("[Email Invite:{OpId}] Invite link: {Link}", opId, link);
            }
            string subject = "Welcome to Jyoti Iyer CPA Client Portal";
            string message = $@"
                <h2>Welcome to Jyoti Iyer CPA Client Portal</h2>
                <p>You've been invited to join our client portal. This secure platform allows you to:</p>
                <ul>
                    <li>Upload tax documents securely</li>
                    <li>Download completed tax returns</li>
                    <li>Communicate directly with our team</li>
                    <li>And much more!</li>
                </ul>
                <p>Please click the link below to set up your account and password:</p>
                <p><a href='{link}' style='padding:10px 15px; background-color:#dc3545; color:white; text-decoration:none; border-radius:5px;'>Set Up Your Account</a></p>
                <p>If you can't click the link, copy and paste this URL into your browser:</p>
                <p>{link}</p>
                <p>This link will expire in 24 hours for security reasons.</p>
                <p>Thank you for choosing Jyoti Iyer CPA for your accounting needs!</p>";

            await SendEmailAsync(toEmail, subject, message);
            _logger.LogInformation("[Email Invite:{OpId}] Invite email queued via SMTP. To={To}", opId, toEmail);
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetLink)
        {
            var opId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("[Email Reset:{OpId}] Preparing password reset email. To={To} Env={Env}", opId, email, _env.EnvironmentName);
            if (_env.IsDevelopment())
            {
                _logger.LogDebug("[Email Reset:{OpId}] Reset link: {Link}", opId, resetLink);
            }
            string subject = "Reset Your Password - Jyoti Iyer CPA Client Portal";
            string message = $@"
                <h2>Reset Your Password</h2>
                <p>We received a request to reset your password for the Jyoti Iyer CPA Client Portal.</p>
                <p>Please click the link below to reset your password:</p>
                <p><a href='{resetLink}' style='padding:10px 15px; background-color:#dc3545; color:white; text-decoration:none; border-radius:5px;'>Reset Password</a></p>
                <p>If you can't click the link, copy and paste this URL into your browser:</p>
                <p>{resetLink}</p>
                <p>This link will expire in 24 hours for security reasons.</p>
                <p>If you did not request a password reset, please ignore this email or contact us if you have concerns.</p>
                <p>Thank you,<br>Jyoti Iyer CPA</p>";

            await SendEmailAsync(email, subject, message);
            _logger.LogInformation("[Email Reset:{OpId}] Password reset email queued via SMTP. To={To}", opId, email);
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            try
            {
                var emailSection = _configuration.GetSection("Email");
                var host = emailSection["SmtpServer"];
                var username = emailSection["SmtpUsername"];
                var password = emailSection["SmtpPassword"];
                var port = emailSection.GetValue<int?>("SmtpPort") ?? 587;
                var enableSsl = emailSection.GetValue<bool?>("EnableSsl") ?? true;
                var fromEmail = emailSection["FromEmail"] ?? username;
                var fromName = emailSection["FromName"] ?? "Jyoti Iyer CPA";

                _logger.LogInformation("[SMTP] Host={Host} Port={Port} SSL={SSL} From={From} User={User}", host, port, enableSsl, fromEmail, Mask(username));
                if ((!string.IsNullOrEmpty(host) && host.Contains("example.com", StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(username) && username.Contains("your-smtp-username", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("[SMTP] Placeholder SMTP settings detected. Update appsettings or user-secrets with real credentials.");
                }

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    var msg = "SMTP settings are not fully configured. Set Email:SmtpServer, Email:SmtpUsername, Email:SmtpPassword.";
                    _logger.LogError("[SMTP] {Message}", msg);
                    throw new InvalidOperationException(msg);
                }

                using (var smtp = new SmtpClient(host, port))
                {
                    smtp.EnableSsl = enableSsl;
                    smtp.Credentials = new NetworkCredential(username, password);

                    using (var message = new MailMessage())
                    {
                        message.From = new MailAddress(fromEmail, fromName);
                        message.To.Add(new MailAddress(email));
                        message.Subject = subject;
                        message.Body = htmlMessage ?? string.Empty;
                        message.IsBodyHtml = true;

                        _logger.LogInformation("[SMTP] Sending email to {To} with subject '{Subject}'", email, subject);
                        await smtp.SendMailAsync(message);
                    }
                }

                _logger.LogInformation("[SMTP] Email sent to {To}. Subject='{Subject}'. Server={Host}:{Port}", email, subject, host, port);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "[SMTP] SmtpException while sending email: StatusCode={StatusCode} Message={Message}", ex.StatusCode, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SMTP] Unexpected exception while sending email: {Message}", ex.Message);
                throw;
            }
        }

        private static string Mask(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var at = input.IndexOf('@');
            if (at > 2)
            {
                return new string('*', at - 2) + input.Substring(at - 2);
            }
            return "***";
        }
    }
}
