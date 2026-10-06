using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Controllers.Api.V1;
using ShiftingGuru.Data;
using ShiftingGuru.Models;

namespace ShiftingGuru.Services.Api;

/// <summary>
/// NEW (mobile API): builds the partner profile the app shows after sign-in
/// and on /me. One place, so both endpoints always return the same thing.
/// </summary>
public interface IPartnerProfileReader
{
    Task<PartnerProfileDto> ReadAsync(Vendor vendor, CancellationToken ct = default);
}

public class PartnerProfileReader : IPartnerProfileReader
{
    private readonly ApplicationDbContext _db;
    private readonly IReviewService _reviews;

    public PartnerProfileReader(ApplicationDbContext db, IReviewService reviews)
    {
        _db = db;
        _reviews = reviews;
    }

    public async Task<PartnerProfileDto> ReadAsync(Vendor vendor, CancellationToken ct = default)
    {
        // Same rating the website shows: approved reviews only.
        var rating = await _reviews.GetVendorSummaryAsync(vendor.Id, ct);

        // A job counts once an admin has marked the move complete.
        var completedJobs = await _db.Leads
            .AsNoTracking()
            .CountAsync(l => l.SelectedVendorId == vendor.Id && l.Status == LeadStatus.Completed, ct);

        return PartnerProfileDto.From(vendor, rating.AverageRating, rating.ApprovedCount, completedJobs);
    }
}