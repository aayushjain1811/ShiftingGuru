using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Review;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): the customer's review of the partner they chose.
///
/// Uses the website's own ReviewService, so the rules are the same everywhere:
/// - only after an admin marks the move Completed
/// - one review per move, always about the partner the customer chose
/// - every review waits for the team's approval before it's public
/// - it can be edited only while it's still waiting
/// </summary>
[Route("api/v1/customer/bookings/{bookingId:int}/review")]
public class CustomerReviewsController : ApiCustomerControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IReviewService _reviews;

    public CustomerReviewsController(ApplicationDbContext db, IReviewService reviews)
    {
        _db = db;
        _reviews = reviews;
    }

    // GET /api/v1/customer/bookings/42/review
    [HttpGet("")]
    public async Task<IActionResult> Get(int bookingId, CancellationToken ct)
    {
        var lead = await OwnBookingAsync(bookingId, ct);
        if (lead is null) return NotFound(new ApiError("That booking couldn't be found.", "notFound"));

        return Ok(await StateAsync(lead, ct));
    }

    // POST /api/v1/customer/bookings/42/review
    // Creates the review, or edits it while it's still waiting for approval.
    [HttpPost("")]
    public async Task<IActionResult> Submit(int bookingId, [FromBody] SubmitReviewRequest request, CancellationToken ct)
    {
        var lead = await OwnBookingAsync(bookingId, ct);
        if (lead is null) return NotFound(new ApiError("That booking couldn't be found.", "notFound"));

        var model = new CreateReviewViewModel
        {
            Rating = request.Rating,
            Title = request.Title,
            Comment = request.Comment
        };

        var existing = await _reviews.GetForLeadAsync(lead.Id, ct);

        // The lead comes from the signed-in customer, so a tampered booking or
        // partner id has nowhere to go - the same protection as the website.
        var result = existing is null
            ? await _reviews.CreateAsync(lead, model, ct)
            : await _reviews.UpdateAsync(lead.Id, model, ct);

        if (!result.Succeeded)
        {
            return BadRequest(new ApiError(result.Error ?? "Couldn't save your review. Please try again.", "reviewFailed"));
        }

        return Ok(await StateAsync(lead, ct));
    }

    // -----------------------------------------------------------------

    /// <summary>The customer's booking, with the chosen partner - only if it's theirs.</summary>
    private Task<Lead?> OwnBookingAsync(int leadId, CancellationToken ct) =>
        _db.Leads
            .AsNoTracking()
            .Include(l => l.SelectedVendor)
            .FirstOrDefaultAsync(l => l.Id == leadId
                                   && l.CustomerId == CurrentCustomer.Id
                                   && l.SelectedVendorId != null, ct);

    private async Task<CustomerReviewStateDto> StateAsync(Lead lead, CancellationToken ct)
    {
        // One place decides eligibility - the website's own check.
        var eligibility = await _reviews.CheckEligibilityAsync(lead, ct);
        var review = eligibility.Existing ?? await _reviews.GetForLeadAsync(lead.Id, ct);

        return new CustomerReviewStateDto(
            eligibility.CanReview,
            review is null ? eligibility.Reason : null,
            review is null ? null : CustomerReviewDto.From(review),
            lead.SelectedVendor?.BusinessName ?? "your partner",
            lead.LeadNumber);
    }
}