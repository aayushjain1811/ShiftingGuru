using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): the partner app's home screen.
///
/// Uses the same counts as the website's partner dashboard
/// (Areas/Partner/Controllers/DashboardController), plus bookings and this
/// month's booked value, which the app shows and the website doesn't.
/// Every query is filtered by CurrentVendor.Id.
/// </summary>
[Route("api/v1/partner")]
public class PartnerDashboardController : ApiPartnerControllerBase
{
    private const int RecentLeadCount = 5;

    // India has no daylight saving, so a fixed offset is exact.
    private static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);

    private readonly ApplicationDbContext _db;
    private readonly IReviewService _reviews;

    public PartnerDashboardController(ApplicationDbContext db, IReviewService reviews)
    {
        _db = db;
        _reviews = reviews;
    }

    // GET /api/v1/partner/dashboard
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var vendorId = CurrentVendor.Id;

        // ---- Leads: one grouped query, like the website ----
        var byStatus = await _db.LeadAssignments
            .AsNoTracking()
            .Where(a => a.VendorId == vendorId && a.Status != AssignmentStatus.Cancelled)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var newLeads = byStatus.FirstOrDefault(x => x.Status == AssignmentStatus.Assigned)?.Count ?? 0;
        var viewedLeads = byStatus.FirstOrDefault(x => x.Status == AssignmentStatus.Viewed)?.Count ?? 0;

        // ---- Quotes ----
        var quoteCounts = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.VendorId == vendorId)
            .GroupBy(q => q.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // ---- Bookings: leads where the customer chose this partner ----
        var activeBookings = await _db.Leads.AsNoTracking()
            .CountAsync(l => l.SelectedVendorId == vendorId && l.Status == LeadStatus.Converted, ct);

        var completedJobs = await _db.Leads.AsNoTracking()
            .CountAsync(l => l.SelectedVendorId == vendorId && l.Status == LeadStatus.Completed, ct);

        // ---- Booked value this month (India time) ----
        // The total of accepted quotes on leads converted since the 1st.
        // This is the value of work won, not money paid out - ShiftingGuru
        // doesn't handle the payment between customer and partner.
        var nowIndia = DateTime.UtcNow + IndiaOffset;
        var monthStartUtc = DateTime.SpecifyKind(
            new DateTime(nowIndia.Year, nowIndia.Month, 1) - IndiaOffset, DateTimeKind.Utc);

        var bookedValue = await _db.Quotes.AsNoTracking()
            .Where(q => q.VendorId == vendorId
                     && q.Status == QuoteStatus.Accepted
                     && q.Lead!.ConvertedAt >= monthStartUtc)
            .SumAsync(q => (decimal?)q.TotalAmount, ct) ?? 0m;

        // ---- Rating: approved reviews only, same as the website ----
        var rating = await _reviews.GetVendorSummaryAsync(vendorId, ct);

        // ---- Recent open leads, without customer contact details ----
        var recentRows = await _db.LeadAssignments
            .AsNoTracking()
            .Where(a => a.VendorId == vendorId
                     && (a.Status == AssignmentStatus.Assigned || a.Status == AssignmentStatus.Viewed))
            .OrderByDescending(a => a.AssignedAt)
            .Take(RecentLeadCount)
            .Select(a => new
            {
                a.LeadId,
                a.Lead!.LeadNumber,
                a.Lead.ServiceSlug,
                a.Lead.ServiceName,
                a.Lead.MovingFrom,
                a.Lead.MovingTo,
                a.Lead.StorageLocation,
                a.Lead.MovingDate,
                a.Status,
                a.AssignedAt,

                // Service details only - for the card's one-line summary.
                a.Lead.PropertyType,
                a.Lead.MoveSize,
                a.Lead.OfficeSize,
                a.Lead.DeskCount,
                a.Lead.VehicleType,
                a.Lead.VehicleModel,
                a.Lead.VehicleCondition,
                a.Lead.GoodsType,
                a.Lead.LoadDetails,
                a.Lead.VehicleRequirement,
                a.Lead.StorageType,
                a.Lead.StorageSize,
                a.Lead.StorageDuration,

                HasActiveQuote = _db.Quotes.Any(q =>
                    q.LeadId == a.LeadId
                    && q.VendorId == vendorId
                    && Quote.ActiveStatuses.Contains(q.Status))
            })
            .ToListAsync(ct);

        var recentLeads = recentRows
            .Select(r => new PartnerLeadSummaryDto(
                r.LeadId, r.LeadNumber, r.ServiceSlug, r.ServiceName,
                r.MovingFrom, r.MovingTo, r.StorageLocation, r.MovingDate,
                PartnerLeadStatus.For(r.Status, r.HasActiveQuote),
                r.AssignedAt,
                PartnerLeadSummary.Build(r.ServiceName,
                    r.PropertyType, r.MoveSize, r.OfficeSize, r.DeskCount,
                    r.VehicleType, r.VehicleModel, r.VehicleCondition,
                    r.GoodsType, r.LoadDetails, r.VehicleRequirement,
                    r.StorageType, r.StorageSize, r.StorageDuration)))
            .ToList();

        // ---- Profile completion: same rule as the website dashboard ----
        var optional = new (string Label, bool Filled)[]
        {
            ("GST number", !string.IsNullOrWhiteSpace(CurrentVendor.GstNumber)),
            ("Operating locations", !string.IsNullOrWhiteSpace(CurrentVendor.OperatingLocations)),
            ("About your business", !string.IsNullOrWhiteSpace(CurrentVendor.AdditionalInformation)),
            ("Years of experience", CurrentVendor.YearsOfExperience > 0)
        };

        var filled = optional.Count(o => o.Filled);
        var completion = (int)Math.Round((filled + 6) / 10.0 * 100);   // 6 required fields always present

        var stats = new PartnerStatsDto(
            NewLeads: newLeads,
            OpenLeads: newLeads + viewedLeads,
            PendingQuotes: quoteCounts
                .Where(x => x.Status is QuoteStatus.Submitted or QuoteStatus.UnderReview)
                .Sum(x => x.Count),
            QuotesSubmitted: quoteCounts.Sum(x => x.Count),
            AcceptedQuotes: quoteCounts.FirstOrDefault(x => x.Status == QuoteStatus.Accepted)?.Count ?? 0,
            ActiveBookings: activeBookings,
            CompletedJobs: completedJobs,
            BookedValueThisMonth: bookedValue,
            Rating: rating.AverageRating,
            ReviewCount: rating.ApprovedCount);

        return Ok(new PartnerDashboardDto(
            stats,
            recentLeads,
            completion,
            optional.Where(o => !o.Filled).Select(o => o.Label).ToList()));
    }
}