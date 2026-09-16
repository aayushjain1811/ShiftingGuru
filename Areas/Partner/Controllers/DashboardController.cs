using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Areas.Partner.Controllers;

[Route("partner/dashboard")]
public class DashboardController : PartnerControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IReviewService _reviews;

    public DashboardController(
        IPartnerService partners,
        UserManager<IdentityUser> users,
        ApplicationDbContext db,
        IReviewService reviews)
        : base(partners, users)
    {
        _db = db;
        _reviews = reviews;
    }

    // GET /partner/dashboard
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Dashboard";

        // Completion is measured on the optional fields only - the required
        // ones were filled in at registration.
        var optional = new (string Label, bool Filled)[]
        {
            ("GST number", !string.IsNullOrWhiteSpace(CurrentVendor.GstNumber)),
            ("Operating locations", !string.IsNullOrWhiteSpace(CurrentVendor.OperatingLocations)),
            ("About your business", !string.IsNullOrWhiteSpace(CurrentVendor.AdditionalInformation)),
            ("Years of experience", CurrentVendor.YearsOfExperience > 0)
        };

        var filled = optional.Count(o => o.Filled);

        // One grouped query covers both counts.
        var byStatus = await _db.LeadAssignments
            .AsNoTracking()
            .Where(a => a.VendorId == CurrentVendor.Id && a.Status != AssignmentStatus.Cancelled)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var newLeads = byStatus.FirstOrDefault(x => x.Status == AssignmentStatus.Assigned)?.Count ?? 0;
        var viewedLeads = byStatus.FirstOrDefault(x => x.Status == AssignmentStatus.Viewed)?.Count ?? 0;

        var quoteCounts = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.VendorId == CurrentVendor.Id)
            .GroupBy(q => q.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var rating = await _reviews.GetVendorSummaryAsync(CurrentVendor.Id, ct);

        return View(new VendorDashboardViewModel
        {
            Vendor = CurrentVendor,
            AverageRating = rating.AverageRating,
            ReviewCount = rating.ApprovedCount,
            QuotesSubmitted = quoteCounts.Sum(x => x.Count),
            AcceptedQuotes = quoteCounts.FirstOrDefault(x => x.Status == QuoteStatus.Accepted)?.Count ?? 0,
            AssignedLeads = byStatus.Sum(x => x.Count),
            NewLeads = newLeads,
            ViewedLeads = viewedLeads,
            ProfileCompletion = (int)Math.Round(
                (filled + 6) / 10.0 * 100),   // 6 required fields always present
            MissingProfileFields = optional.Where(o => !o.Filled).Select(o => o.Label).ToList()
        });
    }
}