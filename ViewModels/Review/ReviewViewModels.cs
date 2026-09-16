using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Review;

/// <summary>
/// The customer review form. No LeadId and no VendorId: the lead comes from
/// the customer session, the vendor from that lead's selected vendor.
/// </summary>
public class CreateReviewViewModel
{
    [Range(1, 5, ErrorMessage = "Choose a rating between 1 and 5 stars.")]
    [Display(Name = "Your rating")]
    public int Rating { get; set; }

    [StringLength(120, ErrorMessage = "Keep the title under 120 characters.")]
    [Display(Name = "Review title (optional)")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Tell us a little about your experience.")]
    [StringLength(1000, MinimumLength = 10,
        ErrorMessage = "Your review should be between 10 and 1000 characters.")]
    [Display(Name = "Your review")]
    public string? Comment { get; set; }

    // ---- read-only context ----
    [BindNever, ValidateNever] public string VendorName { get; set; } = "";
    [BindNever, ValidateNever] public string LeadNumber { get; set; } = "";
    [BindNever, ValidateNever] public string Route { get; set; } = "";
    [BindNever, ValidateNever] public bool IsEdit { get; set; }
}

/// <summary>The customer's own review, with its moderation state.</summary>
public class CustomerReviewViewModel
{
    public Models.Review Review { get; set; } = new();
    public string VendorName { get; set; } = "";

    /// <summary>What the customer is told. Never the internal status name.</summary>
    public string StatusLabel => Review.Status switch
    {
        ReviewStatus.Pending => "Awaiting moderation",
        ReviewStatus.Approved => "Published",
        _ => "Not published"
    };
}

/// <summary>Aggregate rating for one vendor, from approved reviews only.</summary>
public class VendorReviewSummaryViewModel
{
    public int ApprovedCount { get; set; }
    public double? AverageRating { get; set; }

    /// <summary>Star value (1-5) to how many approved reviews gave it.</summary>
    public IReadOnlyDictionary<int, int> Breakdown { get; set; } = new Dictionary<int, int>();

    public bool HasReviews => ApprovedCount > 0;

    public int CountFor(int stars) => Breakdown.TryGetValue(stars, out var c) ? c : 0;

    public int PercentFor(int stars) =>
        ApprovedCount == 0 ? 0 : (int)Math.Round(CountFor(stars) * 100.0 / ApprovedCount);

    /// <summary>"4.8", or null when there's nothing to average.</summary>
    public string? DisplayRating => AverageRating?.ToString("0.0");
}

public class VendorReviewListViewModel
{
    public VendorReviewSummaryViewModel Summary { get; set; } = new();

    /// <summary>Approved reviews, plus this vendor's own pending ones.</summary>
    public IReadOnlyList<Models.Review> Reviews { get; set; } = Array.Empty<Models.Review>();

    public int PendingCount { get; set; }
}

public class AdminReviewListViewModel
{
    public IReadOnlyList<Models.Review> Reviews { get; set; } = Array.Empty<Models.Review>();

    public string? Search { get; set; }
    public ReviewStatus? Status { get; set; }
    public int? Rating { get; set; }
    public int? VendorId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public int PendingCount { get; set; }
    public IReadOnlyList<(int Id, string Name)> Vendors { get; set; } = Array.Empty<(int, string)>();

    public IDictionary<string, string?> RouteValues(int page) => new Dictionary<string, string?>
    {
        ["search"] = Search,
        ["status"] = Status?.ToString(),
        ["rating"] = Rating?.ToString(),
        ["vendorId"] = VendorId?.ToString(),
        ["from"] = FromDate?.ToString("yyyy-MM-dd"),
        ["to"] = ToDate?.ToString("yyyy-MM-dd"),
        ["page"] = page.ToString()
    };
}

public class AdminReviewDetailsViewModel
{
    public Models.Review Review { get; set; } = new();
    public Lead Lead { get; set; } = new();
    public Vendor Vendor { get; set; } = new();

    /// <summary>True when the reviewed vendor really is the lead's selected one.</summary>
    public bool MatchesSelectedVendor => Lead.SelectedVendorId == Review.VendorId;

    public IReadOnlyList<ReviewStatus> AllowedTransitions { get; set; } = Array.Empty<ReviewStatus>();
}