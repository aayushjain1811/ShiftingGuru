using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ShiftingGuru.Services.Email;

/// <summary>
/// Sends through Resend's HTTP API rather than SMTP.
///
/// Cloud Run blocks outbound port 25 and throttles other SMTP ports, so an
/// HTTPS call is both faster and more reliable there. This implements the
/// same IEmailService, so the notification service, template builder and
/// background sender are untouched - only the delivery mechanism changes.
///
/// Like the SMTP version, it never throws: a failure comes back as a result
/// so a business transaction is never rolled back because email was down.
/// </summary>
public class ResendEmailService : IEmailService
{
    private const string Endpoint = "https://api.resend.com/emails";

    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient http,
        IOptions<EmailOptions> options,
        ILogger<ResendEmailService> logger)
    {
        _http = http;
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

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            // A clear message beats a 401 from the provider: this one is
            // almost always a missing user secret rather than a code fault.
            _logger.LogError("Email is enabled but Email:ApiKey is not configured.");
            return EmailResult.Fail("Email:ApiKey is not configured.");
        }

        // Resend takes the sender as "Name <address@domain>".
        var from = string.IsNullOrWhiteSpace(_options.FromName)
            ? _options.FromEmail
            : $"{_options.FromName} <{_options.FromEmail}>";

        var payload = new Dictionary<string, object>
        {
            ["from"] = from,
            ["to"] = new[] { recipient },
            ["subject"] = subject,
            ["html"] = htmlBody
        };

        if (!string.IsNullOrWhiteSpace(textBody))
        {
            payload["text"] = textBody;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            // The key goes on the request, never in a log line.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Sent \"{Subject}\" to {Recipient}.", subject, recipient);
                return EmailResult.Ok();
            }

            // Resend explains rejections in the body: an unverified domain, a
            // malformed address, a rate limit. Worth keeping, so trim rather
            // than discard.
            var body = await response.Content.ReadAsStringAsync(ct);
            var reason = $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}";
            if (reason.Length > 400) reason = reason[..400];

            _logger.LogError(
                "Resend rejected \"{Subject}\" to {Recipient}. {Reason}", subject, recipient, reason);

            return EmailResult.Fail(reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send \"{Subject}\" to {Recipient}.", subject, recipient);

            var reason = ex.Message.Length > 400 ? ex.Message[..400] : ex.Message;
            return EmailResult.Fail(reason);
        }
    }
}