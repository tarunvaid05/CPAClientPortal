using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
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

        // Log for debugging
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

        // Log for debugging
        Console.WriteLine($"[Password Reset Email Sent] To: {email} | Link: {resetLink}");
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        try
        {
            // Skeleton implementation of sending email through SendGrid.
            var apiKey = _configuration["SendGrid:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("[Email Warning] SendGrid API key is not set in configuration.");
                return;
            }

            var client = new SendGridClient(apiKey);
            // Update this "from" address as necessary
            var fromEmail = _configuration["SendGrid:FromEmail"] ?? "no-reply@jyotiyiercpa.com";
            var fromName = _configuration["SendGrid:FromName"] ?? "Jyoti Iyer CPA Client Portal";
            var from = new EmailAddress(fromEmail, fromName);
            var to = new EmailAddress(email);
            // For simplicity, we're leaving the plain text version empty.
            var msg = MailHelper.CreateSingleEmail(from, to, subject, "", htmlMessage);
            var response = await client.SendEmailAsync(msg);

            Console.WriteLine($"[Email Sent] To: {email} | Subject: {subject} | Status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Email Error] {ex.Message}");
            throw;
        }
    }
}