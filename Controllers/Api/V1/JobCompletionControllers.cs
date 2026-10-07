using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Services.Jobs;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>What the partner app sends when marking a job done.</summary>
public class MarkCompletedRequest
{
    [StringLength(500, ErrorMessage = "Keep the note under 500 characters.")]
    public string? Note { get; set; }
}

/// <summary>What the customer app sends when something went wrong.</summary>
public class ReportProblemRequest
{
    [Required(ErrorMessage = "Tell us what went wrong.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Write between 10 and 1000 characters.")]
    public string? Text { get; set; }
}

/// <summary>
/// NEW (mobile API): the partner marks a booked job as done.
/// Only for the partner the customer chose - CurrentVendor comes from the token.
/// </summary>
[Route("api/v1/partner/bookings/{leadId:int}/completion")]
public class PartnerCompletionController : ApiPartnerControllerBase
{
    private readonly IJobCompletionService _jobs;

    public PartnerCompletionController(IJobCompletionService jobs) => _jobs = jobs;

    // GET /api/v1/partner/bookings/42/completion
    [HttpGet("")]
    public async Task<IActionResult> Get(int leadId, CancellationToken ct)
    {
        var view = await _jobs.ForPartnerAsync(CurrentVendor.Id, leadId, ct);
        return view is null ? NotFound(new ApiError("That booking couldn't be found.", "notFound")) : Ok(view);
    }

    // POST /api/v1/partner/bookings/42/completion   { "note": "optional" }
    [HttpPost("")]
    public async Task<IActionResult> Mark(int leadId, [FromBody] MarkCompletedRequest request, CancellationToken ct)
    {
        var result = await _jobs.MarkCompletedAsync(CurrentVendor.Id, leadId, request.Note, ct);
        if (!result.Succeeded) return Conflict(new ApiError(result.Error!, "cannotMark"));

        return Ok(await _jobs.ForPartnerAsync(CurrentVendor.Id, leadId, ct));
    }
}

/// <summary>
/// NEW (mobile API): the customer answers "Did your move happen?".
/// Only on the customer's own bookings - CurrentCustomer comes from the token.
/// </summary>
[Route("api/v1/customer/bookings/{bookingId:int}/completion")]
public class CustomerCompletionController : ApiCustomerControllerBase
{
    private readonly IJobCompletionService _jobs;

    public CustomerCompletionController(IJobCompletionService jobs) => _jobs = jobs;

    // GET /api/v1/customer/bookings/42/completion
    [HttpGet("")]
    public async Task<IActionResult> Get(int bookingId, CancellationToken ct)
    {
        var view = await _jobs.ForCustomerAsync(CurrentCustomer.Id, bookingId, ct);
        return view is null ? NotFound(new ApiError("That booking couldn't be found.", "notFound")) : Ok(view);
    }

    // POST /api/v1/customer/bookings/42/completion/confirm
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(int bookingId, CancellationToken ct)
    {
        var result = await _jobs.ConfirmAsync(CurrentCustomer.Id, bookingId, ct);
        if (!result.Succeeded) return Conflict(new ApiError(result.Error!, "cannotConfirm"));

        return Ok(await _jobs.ForCustomerAsync(CurrentCustomer.Id, bookingId, ct));
    }

    // POST /api/v1/customer/bookings/42/completion/problem   { "text": "..." }
    [HttpPost("problem")]
    public async Task<IActionResult> Problem(int bookingId, [FromBody] ReportProblemRequest request, CancellationToken ct)
    {
        var result = await _jobs.ReportProblemAsync(CurrentCustomer.Id, bookingId, request.Text!, ct);
        if (!result.Succeeded) return Conflict(new ApiError(result.Error!, "cannotReport"));

        return Ok(await _jobs.ForCustomerAsync(CurrentCustomer.Id, bookingId, ct));
    }
}