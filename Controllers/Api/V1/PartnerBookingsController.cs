using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): the partner's bookings and earnings.
///
/// A booking is a lead where the customer chose this partner
/// (Lead.SelectedVendorId), set by the website's QuoteSelectionService.
/// The website has two booking states, and the app's tabs follow them:
///
///   Upcoming  - Converted, moving date today or later (or not set)
///   Active    - Converted, moving date has passed, not yet marked complete
///   Completed - an admin marked the move Completed
/// </summary>
[Route("api/v1/partner")]
public class PartnerBookingsController : ApiPartnerControllerBase
{
    private const int MaxListSize = 100;
    private const int ChartMonths = 12;

    private static readonly CultureInfo India = new("en-IN");

    private readonly ApplicationDbContext _db;

    public PartnerBookingsController(ApplicationDbContext db) => _db = db;

    // GET /api/v1/partner/bookings?tab=upcoming|active|completed
    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings([FromQuery] string? tab, CancellationToken ct)
    {
        var selected = (tab ?? "upcoming").Trim().ToLowerInvariant();

        if (selected is not ("upcoming" or "active" or "completed"))
        {
            return BadRequest(new ApiError("Unknown tab. Use upcoming, active or completed.", "badTab"));
        }

        var vendorId = CurrentVendor.Id;
        var today = IndiaTime.Today;

        var query = _db.Leads
            .AsNoTracking()
            .Where(l => l.SelectedVendorId == vendorId && l.SelectedQuoteId != null);

        query = selected switch
        {
            // Soonest move first; undated ones after the dated ones.
            "upcoming" => query
                .Where(l => l.Status == LeadStatus.Converted && (l.MovingDate == null || l.MovingDate >= today))
                .OrderBy(l => l.MovingDate == null)
                .ThenBy(l => l.MovingDate),

            "active" => query
                .Where(l => l.Status == LeadStatus.Converted && l.MovingDate < today)
                .OrderByDescending(l => l.MovingDate),

            _ => query   // "completed"
                .Where(l => l.Status == LeadStatus.Completed)
                .OrderByDescending(l => l.UpdatedAt)
        };

        var rows = await query
            .Take(MaxListSize)
            .Select(l => new
            {
                l.Id,
                l.LeadNumber,
                QuoteId = l.SelectedQuote!.Id,
                l.SelectedQuote.QuoteNumber,
                l.SelectedQuote.TotalAmount,
                l.ServiceSlug,
                l.ServiceName,
                l.MovingFrom,
                l.MovingTo,
                l.StorageLocation,
                l.MovingDate,
                l.Status,
                l.CustomerName,
                l.ConvertedAt,
                l.CreatedAt
            })
            .ToListAsync(ct);

        // The customer chose this partner, so their name is theirs to see.
        var result = rows.Select(r => new PartnerBookingDto(
                r.Id, r.LeadNumber, r.QuoteId, r.QuoteNumber,
                r.ServiceSlug, r.ServiceName, r.MovingFrom, r.MovingTo, r.StorageLocation, r.MovingDate,
                r.TotalAmount,
                r.Status == LeadStatus.Completed ? "completed" : "confirmed",
                r.CustomerName,
                r.ConvertedAt ?? r.CreatedAt))
            .ToList();

        return Ok(result);
    }

    // GET /api/v1/partner/earnings
    [HttpGet("earnings")]
    public async Task<IActionResult> Earnings(CancellationToken ct)
    {
        var vendorId = CurrentVendor.Id;

        // Every job this partner won: the chosen quote's total and when it was chosen.
        // Bookings an admin later cancelled don't count.
        var won = await _db.Leads
            .AsNoTracking()
            .Where(l => l.SelectedVendorId == vendorId
                     && l.SelectedQuoteId != null
                     && l.ConvertedAt != null
                     && l.Status != LeadStatus.Cancelled)
            .Select(l => new
            {
                ConvertedAt = l.ConvertedAt!.Value,
                Amount = l.SelectedQuote!.TotalAmount,
                l.Status
            })
            .ToListAsync(ct);

        var nowIndia = IndiaTime.Now;
        var thisMonthStart = new DateTime(nowIndia.Year, nowIndia.Month, 1);

        // Twelve months, oldest first, ending with this month - one bar each,
        // including months with nothing in them.
        var months = Enumerable.Range(0, ChartMonths)
            .Select(i => thisMonthStart.AddMonths(i - (ChartMonths - 1)))
            .Select(start => new EarningsMonthDto(
                start.ToString("MMM", India),
                start.Year,
                won.Where(w =>
                    {
                        var at = IndiaTime.FromUtc(w.ConvertedAt);
                        return at.Year == start.Year && at.Month == start.Month;
                    })
                   .Sum(w => w.Amount)))
            .ToList();

        return Ok(new PartnerEarningsDto(
            TotalBooked: won.Sum(w => w.Amount),
            ThisMonth: won.Where(w => IndiaTime.FromUtc(w.ConvertedAt) >= thisMonthStart).Sum(w => w.Amount),
            UpcomingValue: won.Where(w => w.Status == LeadStatus.Converted).Sum(w => w.Amount),
            CompletedJobs: won.Count(w => w.Status == LeadStatus.Completed),
            TotalBookings: won.Count,
            Months: months));
    }
}