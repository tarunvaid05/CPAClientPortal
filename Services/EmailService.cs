using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public interface IEmailSender
{
    Task SendInviteEmail(string toEmail, string link);
    Task SendEmailAsync(string email, string subject, string htmlMessage);
    Task SendPasswordResetEmailAsync(string email, string resetLink);
}

public class EmailService : IEmailSender
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendInviteEmail(string toEmail, string link)
    {
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
            <p>Please click the link below to set up your password and access your account:</p>
            <p><a href='{link}' style='padding:10px 15px; background-color:#dc3545; color:white; text-decoration:none; border-radius:5px;'>Set Your Password</a></p>
            <p>If you can't click the link, copy and paste this URL into your browser:</p>
            <p>{link}</p>
            <p>This link will expire in 24 hours for security reasons.</p>
            <p>Thank you for choosing Jyoti Iyer CPA for your accounting needs!</p>";

        await SendEmailAsync(toEmail, subject, message);

        Console.WriteLine($"[Email Sent] To: {toEmail} | Link: {link}");
    }

    public async Task SendPasswordResetEmailAsync(string email, string resetLink)
    {
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

        Console.WriteLine($"[Password Reset Email Sent] To: {email} | Link: {resetLink}");
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

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("[Email Warning] SMTP settings are not fully configured. Set Email:SmtpServer, Email:SmtpUsername, Email:SmtpPassword.");
                return;
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

                    await smtp.SendMailAsync(message);
                }
            }

            Console.WriteLine($"[Email Sent] To: {email} | Subject: {subject} via SMTP {host}:{port}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email Error] {ex.Message}");
            throw;
        }
    }
}

