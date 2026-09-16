using ShiftingGuru.Models;
using ShiftingGuru.ViewModels.Review;

namespace ShiftingGuru.Services;

public record ReviewResult(bool Succeeded, Review? Review, string? Error)
{
    public static ReviewResult Ok(Review review) => new(true, review, null);
    public static ReviewResult Fail(string error) => new(false, null, error);
}

/// <summary>Why a customer can or can't review a lead.</summary>
public record ReviewEligibility(bool CanReview, string? Reason, Review? Existing);

public interface IReviewService
{
    /// <summary>Checks every rule in one place: status, selected vendor, duplicates.</summary>
    Task<ReviewEligibility> CheckEligibilityAsync(Lead lead, CancellationToken ct = default);

    Task<ReviewResult> CreateAsync(Lead lead, CreateReviewViewModel model, CancellationToken ct = default);

    /// <summary>Updates a pending review the customer owns.</summary>
    Task<ReviewResult> UpdateAsync(int leadId, CreateReviewViewModel model, CancellationToken ct = default);

    Task<Review?> GetForLeadAsync(int leadId, CancellationToken ct = default);

    /// <summary>Admin moderation. False when the move isn't legal.</summary>
    Task<bool> ModerateAsync(int reviewId, ReviewStatus status, string? note, CancellationToken ct = default);

    IReadOnlyList<ReviewStatus> AllowedTransitionsFrom(ReviewStatus current);

    /// <summary>Public rating, computed in the database from approved reviews.</summary>
    Task<VendorReviewSummaryViewModel> GetVendorSummaryAsync(int vendorId, CancellationToken ct = default);
}