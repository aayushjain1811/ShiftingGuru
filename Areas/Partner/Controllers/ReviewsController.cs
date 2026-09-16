using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Review;

namespace ShiftingGuru.Areas.Partner.Controllers;

/// <summary>
/// A vendor's own reviews. Scoped to CurrentVendor.Id, which comes from the
/// signed-in Identity user - a vendor can never see another's reviews.
/// </summary>
[Route("partner/reviews")]
public class ReviewsController : PartnerControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IReviewService _reviews;

    public ReviewsController(
        IPartnerService partners,
        UserManager<IdentityUser> users,
        ApplicationDbContext db,
        IReviewService reviews)
        : base(partners, users)
    {
        _db = db;
        _reviews = reviews;
    }

    // GET /partner/reviews
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "My reviews";

        var summary = await _reviews.GetVendorSummaryAsync(CurrentVendor.Id, ct);

        // Approved and pending only. A vendor doesn't need to see what was
        // rejected or hidden, and moderation notes never leave the admin area.
        var reviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.VendorId == CurrentVendor.Id
                     && (r.Status == ReviewStatus.Approved || r.Status == ReviewStatus.Pending))
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return View(new VendorReviewListViewModel
        {
            Summary = summary,
            Reviews = reviews,
            PendingCount = reviews.Count(r => r.Status == ReviewStatus.Pending)
        });
    }
}