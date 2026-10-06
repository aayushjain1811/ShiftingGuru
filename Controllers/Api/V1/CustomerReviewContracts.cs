using System.ComponentModel.DataAnnotations;
using ShiftingGuru.Models;

namespace ShiftingGuru.Controllers.Api.V1;

// NEW (mobile API): customer reviews of the partner they chose.

/// <summary>What the app sends to leave or edit a review.</summary>
public class SubmitReviewRequest
{
    [Range(1, 5, ErrorMessage = "Choose a rating between 1 and 5 stars.")]
    public int Rating { get; set; }

    [StringLength(100, ErrorMessage = "Keep the title under 100 characters.")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Tell other customers how the move went.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Write between 10 and 1000 characters.")]
    public string? Comment { get; set; }
}

/// <summary>
/// The customer's own review. CHANGED: whether it's published, waiting or
/// rejected is never sent to the customer, and neither is the team's note.
/// </summary>
public record CustomerReviewDto(
    int Rating,
    string? Title,
    string Comment,
    bool Editable,
    DateTime CreatedAt)
{
    public static CustomerReviewDto From(Review review) => new(
        review.Rating,
        review.Title,
        review.Comment,
        review.IsEditableByCustomer,
        review.CreatedAt);
}

/// <summary>Everything the review screen needs: can they review, and what they already wrote.</summary>
public record CustomerReviewStateDto(
    bool CanReview,
    string? Reason,
    CustomerReviewDto? Review,
    string VendorName,
    string RequestNumber);