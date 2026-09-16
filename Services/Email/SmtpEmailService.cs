using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ShiftingGuru.Services.Email;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailResult> SendAsync(
        string recipient, string subject, string htmlBody, string? textBody = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return EmailResult.Fail("No recipient address.");
        }

        // Development default. The subject and recipient are logged so you can
        // see what would have gone out; the body is not, because it can carry
        // access links and customer details.
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Email disabled. Would have sent \"{Subject}\" to {Recipient}.", subject, recipient);
            return EmailResult.Ok();
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(recipient);

            // Plain-text alternative for clients that won't render HTML.
            if (!string.IsNullOrWhiteSpace(textBody))
            {
                message.AlternateViews.Add(
                    AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));
                message.AlternateViews.Add(
                    AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html"));
            }

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            await client.SendMailAsync(message, ct);

            _logger.LogInformation("Sent \"{Subject}\" to {Recipient}.", subject, recipient);
            return EmailResult.Ok();
        }
        catch (Exception ex)
        {
            // ex.Message can mention the host but never the password - the
            // credential object isn't part of the message text.
            _logger.LogError(ex, "Failed to send \"{Subject}\" to {Recipient}.", subject, recipient);

            var reason = ex.Message.Length > 400 ? ex.Message[..400] : ex.Message;
            return EmailResult.Fail(reason);
        }
    }
}