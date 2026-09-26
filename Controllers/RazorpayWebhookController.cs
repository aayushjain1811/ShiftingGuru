using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Services.Payments;

namespace ShiftingGuru.Controllers;

/// <summary>
/// Razorpay calls this directly when a payment is captured. It's the safety
/// net for a partner whose browser closes, or whose internet drops, right
/// after paying: the payment is still recorded and they aren't charged again.
///
/// Anyone on the internet can call this URL, so nothing is trusted until the
/// signature (made with the webhook secret only we and Razorpay know) matches.
/// </summary>
[Route("webhooks/razorpay")]
[IgnoreAntiforgeryToken]
public class RazorpayWebhookController : Controller
{
    private readonly IRazorpayClient _razorpay;
    private readonly IRegistrationPaymentService _payments;
    private readonly ILogger<RazorpayWebhookController> _logger;

    public RazorpayWebhookController(
        IRazorpayClient razorpay, IRegistrationPaymentService payments,
        ILogger<RazorpayWebhookController> logger)
    {
        _razorpay = razorpay;
        _payments = payments;
        _logger = logger;
    }

    // POST /webhooks/razorpay
    [HttpPost("")]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["X-Razorpay-Signature"].ToString();

        if (!_razorpay.IsValidWebhookSignature(body, signature))
        {
            _logger.LogWarning("Rejected a Razorpay webhook with an invalid signature.");
            return Unauthorized();
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var eventName = root.GetProperty("event").GetString();

            if (eventName is "payment.captured" or "order.paid")
            {
                var payment = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");

                var orderId = payment.TryGetProperty("order_id", out var order) ? order.GetString() : null;
                var paymentId = payment.GetProperty("id").GetString();
                var amount = payment.GetProperty("amount").GetInt64();

                if (!string.IsNullOrEmpty(orderId) && !string.IsNullOrEmpty(paymentId))
                {
                    await _payments.MarkPaidFromWebhookAsync(orderId, paymentId, amount, ct);
                }
            }
        }
        catch (Exception ex)
        {
            // A 500 makes Razorpay retry later, which is what we want.
            _logger.LogError(ex, "Couldn't process a Razorpay webhook.");
            return StatusCode(500);
        }

        return Ok();
    }
}