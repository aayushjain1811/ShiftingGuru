using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.ViewModels.Review;

namespace ShiftingGuru.Services;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        ApplicationDbContext db,
        INotificationService notifications,
        ILogger<ReviewService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>
    /// One place decides eligibility, so the view, the GET and the POST can
    /// never disagree about whether a review is allowed.
    /// </summary>
    public async Task<ReviewEligibility> CheckEligibilityAsync(Lead lead, CancellationToken ct = default)
    {
        var existing = await GetForLeadAsync(lead.Id, ct);

        if (existing is not null)
        {
            return new ReviewEligibility(false, "You've already reviewed this move.", existing);
        }

        if (lead.SelectedVendorId is null)
        {
            return new ReviewEligibility(false, "No provider has been selected for this request yet.", null);
        }

        // Converted means a vendor was chosen. The move happens afterwards, so
        // reviewing before Completed would be reviewing a service not yet given.
        if (lead.Status != LeadStatus.Completed)
        {
            return new ReviewEligibility(false,
                "You'll be able to leave a review once your move is marked complete.", null);
        }

        return new ReviewEligibility(true, null, null);
    }

    public async Task<ReviewResult> CreateAsync(
        Lead lead, CreateReviewViewModel model, CancellationToken ct = default)
    {
        var eligibility = await CheckEligibilityAsync(lead, ct);
        if (!eligibility.CanReview) return ReviewResult.Fail(eligibility.Reason!);

        if (model.Rating is < 1 or > 5)
        {
            return ReviewResult.Fail("Choose a rating between 1 and 5 stars.");
        }

        var review = new Review
        {
            LeadId = lead.Id,

            // From the lead, never from the form. A tampered vendor id has
            // nowhere to land.
            VendorId = lead.SelectedVendorId!.Value,

            Rating = model.Rating,
            Title = Normalise(model.Title),
            Comment = model.Comment!.Trim(),
            CustomerNameSnapshot = Review.ToDisplayName(lead.CustomerName),

            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Reviews.Add(review);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // The unique index on LeadId caught a double submit.
            _logger.LogWarning(ex, "Duplicate review rejected for lead {LeadId}", lead.Id);
            return ReviewResult.Fail("You've already reviewed this move.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Review creation failed for lead {LeadId}", lead.Id);
            return ReviewResult.Fail("Couldn't save your review. Please try again.");
        }

        var vendor = await _db.Vendors.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == review.VendorId, ct);

        if (vendor is not null)
        {
            await _notifications.ReviewSubmittedAsync(review, lead, vendor, ct);
        }

        return ReviewResult.Ok(review);
    }

    public async Task<ReviewResult> UpdateAsync(
        int leadId, CreateReviewViewModel model, CancellationToken ct = default)
    {
        // Scoped by lead, and the lead came from the customer session, so this
        // can only ever touch the caller's own review.
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.LeadId == leadId, ct);

        if (review is null) return ReviewResult.Fail("That review couldn't be found.");

        if (!review.IsEditableByCustomer)
        {
            return ReviewResult.Fail("This review has already been moderated and can't be edited.");
        }

        if (model.Rating is < 1 or > 5)
        {
            return ReviewResult.Fail("Choose a rating between 1 and 5 stars.");
        }

        review.Rating = model.Rating;
        review.Title = Normalise(model.Title);
        review.Comment = model.Comment!.Trim();
        review.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
            return ReviewResult.Ok(review);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Review update failed for lead {LeadId}", leadId);
            return ReviewResult.Fail("Couldn't save your changes. Please try again.");
        }
    }

    public Task<Review?> GetForLeadAsync(int leadId, CancellationToken ct = default) =>
        _db.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.LeadId == leadId, ct);

    public async Task<bool> ModerateAsync(
        int reviewId, ReviewStatus status, string? note, CancellationToken ct = default)
    {
        var review = await _db.Reviews
            .Include(r => r.Vendor)
            .Include(r => r.Lead)
            .FirstOrDefaultAsync(r => r.Id == reviewId, ct);

        if (review is null) return false;

        if (!Enum.IsDefined(status) || !AllowedTransitionsFrom(review.Status).Contains(status))
        {
            return false;
        }

        var wasPublished = review.Status == ReviewStatus.Approved;

        review.Status = status;
        review.ModerationNote = Normalise(note);
        review.UpdatedAt = DateTime.UtcNow;

        if (status == ReviewStatus.Approved)
        {
            review.PublishedAt ??= DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Review moderation failed for review {ReviewId}", reviewId);
            return false;
        }

        // Only tell the vendor the first time it goes live.
        if (status == ReviewStatus.Approved && !wasPublished && review.Vendor is not null)
        {
            await _notifications.ReviewPublishedAsync(review, review.Vendor, ct);
        }

        return true;
    }

    /// <summary>
    /// Rejected and hidden are terminal for publication but reversible by an
    /// admin - a hidden review can go back up, a rejected one reconsidered.
    /// </summary>
    public IReadOnlyList<ReviewStatus> AllowedTransitionsFrom(ReviewStatus current) => current switch
    {
        ReviewStatus.Pending => new[] { ReviewStatus.Approved, ReviewStatus.Rejected },
        ReviewStatus.Approved => new[] { ReviewStatus.Hidden, ReviewStatus.Rejected },
        ReviewStatus.Hidden => new[] { ReviewStatus.Approved, ReviewStatus.Rejected },
        ReviewStatus.Rejected => new[] { ReviewStatus.Approved },
        _ => Array.Empty<ReviewStatus>()
    };

    public async Task<VendorReviewSummaryViewModel> GetVendorSummaryAsync(
        int vendorId, CancellationToken ct = default)
    {
        // Grouped in PostgreSQL - the rows themselves never come back, only
        // the counts. Approved only: pending, rejected and hidden don't count.
        var grouped = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.VendorId == vendorId && r.Status == ReviewStatus.Approved)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = grouped.Sum(g => g.Count);

        return new VendorReviewSummaryViewModel
        {
            ApprovedCount = total,

            // Null rather than 0.0 when there's nothing to average - the view
            // shows "No reviews yet" instead of inventing a score.
            AverageRating = total == 0
                ? null
                : Math.Round(grouped.Sum(g => (double)g.Rating * g.Count) / total, 1),

            Breakdown = grouped.ToDictionary(g => g.Rating, g => g.Count)
        };
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}