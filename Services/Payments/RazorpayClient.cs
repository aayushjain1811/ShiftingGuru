using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ShiftingGuru.Services.Payments;

public record RazorpayPaymentInfo(string Id, string OrderId, long AmountPaise, string Currency, string Status);

public interface IRazorpayClient
{
    string? KeyId { get; }

    /// <summary>Creates an order on Razorpay's side and returns its id (order_...).</summary>
    Task<string> CreateOrderAsync(long amountPaise, string receipt, string email, CancellationToken ct = default);

    /// <summary>Asks Razorpay directly about a payment. Null if Razorpay doesn't know it.</summary>
    Task<RazorpayPaymentInfo?> GetPaymentAsync(string paymentId, CancellationToken ct = default);

    /// <summary>Checks the signature the payment window returns to the browser.</summary>
    bool IsValidCheckoutSignature(string orderId, string paymentId, string signature);

    /// <summary>Checks the signature on a webhook call from Razorpay.</summary>
    bool IsValidWebhookSignature(string body, string signature);
}

/// <summary>
/// Talks to Razorpay's REST API directly over HTTPS - the same approach as the
/// Resend email service, with no extra SDK. The secret key is only ever used
/// here on the server, never sent to the browser or written to a log.
/// </summary>
public class RazorpayClient : IRazorpayClient
{
    private readonly HttpClient _http;
    private readonly RazorpayOptions _options;

    public RazorpayClient(HttpClient http, IOptions<RazorpayOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public string? KeyId => _options.KeyId;

    public async Task<string> CreateOrderAsync(long amountPaise, string receipt, string email, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object>
        {
            ["amount"] = amountPaise,
            ["currency"] = "INR",
            ["receipt"] = receipt,
            ["notes"] = new Dictionary<string, string>
            {
                ["purpose"] = "Partner registration fee",
                ["email"] = email
            }
        };

        using var request = NewRequest(HttpMethod.Post, "orders");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Razorpay order creation failed: {(int)response.StatusCode} {Trim(body)}");
        }

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Razorpay returned an order without an id.");
    }

    public async Task<RazorpayPaymentInfo?> GetPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        using var request = NewRequest(HttpMethod.Get, $"payments/{Uri.EscapeDataString(paymentId)}");
        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode) return null;

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;

        return new RazorpayPaymentInfo(
            root.GetProperty("id").GetString() ?? "",
            root.TryGetProperty("order_id", out var order) ? order.GetString() ?? "" : "",
            root.GetProperty("amount").GetInt64(),
            root.GetProperty("currency").GetString() ?? "",
            root.GetProperty("status").GetString() ?? "");
    }

    public bool IsValidCheckoutSignature(string orderId, string paymentId, string signature) =>
        !string.IsNullOrWhiteSpace(_options.KeySecret)
        && SameHex(Hmac(_options.KeySecret, $"{orderId}|{paymentId}"), signature);

    public bool IsValidWebhookSignature(string body, string signature) =>
        !string.IsNullOrWhiteSpace(_options.WebhookSecret)
        && SameHex(Hmac(_options.WebhookSecret, body), signature);

    // -----------------------------------------------------------------

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("Razorpay:KeyId and Razorpay:KeySecret are not configured.");
        }

        var request = new HttpRequestMessage(method, "https://api.razorpay.com/v1/" + path);
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.KeyId}:{_options.KeySecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private static string Hmac(string secret, string message)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Constant-time comparison, so timing reveals nothing about the right answer.
    private static bool SameHex(string expected, string? actual) =>
        !string.IsNullOrEmpty(actual)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(actual.Trim().ToLowerInvariant()));

    private static string Trim(string value) => value.Length > 300 ? value[..300] : value;
}