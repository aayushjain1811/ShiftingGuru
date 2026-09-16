using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

/// <summary>Shared paging shape for the three CMS lists.</summary>
public abstract class AdminCmsListViewModel
{
    public string? Search { get; set; }

    /// <summary>null = all, true = published, false = drafts.</summary>
    public bool? Published { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public IDictionary<string, string?> RouteValues(int page) => new Dictionary<string, string?>
    {
        ["search"] = Search,
        ["published"] = Published?.ToString().ToLowerInvariant(),
        ["page"] = page.ToString()
    };
}

// ---------------------------------------------------------------
// Locations
// ---------------------------------------------------------------
public class AdminLocationListViewModel : AdminCmsListViewModel
{
    public IReadOnlyList<Location> Locations { get; set; } = Array.Empty<Location>();
    public int DraftCount { get; set; }
}

public class AdminLocationFormViewModel
{
    /// <summary>Zero on create. The route id is authoritative on edit, not this.</summary>
    [BindNever, ValidateNever]
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(80)]
    [Display(Name = "Name")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Enter a slug.")]
    [StringLength(80)]
    [RegularExpression(@"^[a-z0-9]+(-[a-z0-9]+)*$",
        ErrorMessage = "Lowercase letters, numbers and hyphens only.")]
    [Display(Name = "URL slug")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Enter a city.")]
    [StringLength(80)]
    [Display(Name = "City")]
    public string? City { get; set; }

    [StringLength(80)]
    [Display(Name = "State")]
    public string? State { get; set; }

    [Required]
    [StringLength(60)]
    [Display(Name = "Country")]
    public string Country { get; set; } = "India";

    [Required(ErrorMessage = "Enter an H1 heading.")]
    [StringLength(160)]
    [Display(Name = "H1 heading")]
    public string? H1 { get; set; }

    [Required(ErrorMessage = "Enter a short description.")]
    [StringLength(300)]
    [Display(Name = "Short description")]
    public string? ShortDescription { get; set; }

    [Required(ErrorMessage = "Enter the page content.")]
    [MinLength(200, ErrorMessage = "Thin pages hurt more than they help. Write at least 200 characters.")]
    [Display(Name = "Page content")]
    public string? Content { get; set; }

    [Required(ErrorMessage = "Enter a meta title.")]
    [StringLength(160)]
    [Display(Name = "Meta title")]
    public string? MetaTitle { get; set; }

    [Required(ErrorMessage = "Enter a meta description.")]
    [StringLength(320, MinimumLength = 50,
        ErrorMessage = "Between 50 and 320 characters works best in search results.")]
    [Display(Name = "Meta description")]
    public string? MetaDescription { get; set; }

    [StringLength(160)]
    [Display(Name = "Open Graph title")]
    public string? OgTitle { get; set; }

    [StringLength(320)]
    [Display(Name = "Open Graph description")]
    public string? OgDescription { get; set; }

    [StringLength(400)]
    [Url(ErrorMessage = "Enter a full URL.")]
    [Display(Name = "Open Graph image URL")]
    public string? OgImage { get; set; }

    [Display(Name = "Published")]
    public bool IsPublished { get; set; }

    [BindNever, ValidateNever] public bool IsEdit => Id > 0;
}

// ---------------------------------------------------------------
// Routes
// ---------------------------------------------------------------
public class AdminRouteListViewModel : AdminCmsListViewModel
{
    public IReadOnlyList<MovingRoute> Routes { get; set; } = Array.Empty<MovingRoute>();
    public int DraftCount { get; set; }
}

public class AdminRouteFormViewModel
{
    [BindNever, ValidateNever]
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose an origin.")]
    [Display(Name = "From location")]
    public int FromLocationId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a destination.")]
    [Display(Name = "To location")]
    public int ToLocationId { get; set; }

    [Required(ErrorMessage = "Enter an H1 heading.")]
    [StringLength(160)]
    [Display(Name = "H1 heading")]
    public string? H1 { get; set; }

    [Required(ErrorMessage = "Enter a short description.")]
    [StringLength(300)]
    [Display(Name = "Short description")]
    public string? ShortDescription { get; set; }

    [Required(ErrorMessage = "Enter the page content.")]
    [MinLength(200, ErrorMessage = "Thin pages hurt more than they help. Write at least 200 characters.")]
    [Display(Name = "Page content")]
    public string? Content { get; set; }

    [Required(ErrorMessage = "Enter a meta title.")]
    [StringLength(160)]
    [Display(Name = "Meta title")]
    public string? MetaTitle { get; set; }

    [Required(ErrorMessage = "Enter a meta description.")]
    [StringLength(320, MinimumLength = 50,
        ErrorMessage = "Between 50 and 320 characters works best in search results.")]
    [Display(Name = "Meta description")]
    public string? MetaDescription { get; set; }

    [StringLength(160)]
    [Display(Name = "Open Graph title")]
    public string? OgTitle { get; set; }

    [StringLength(320)]
    [Display(Name = "Open Graph description")]
    public string? OgDescription { get; set; }

    [StringLength(400)]
    [Url(ErrorMessage = "Enter a full URL.")]
    [Display(Name = "Open Graph image URL")]
    public string? OgImage { get; set; }

    [Display(Name = "Published")]
    public bool IsPublished { get; set; }

    // ---- context ----
    [BindNever, ValidateNever] public IReadOnlyList<Location> Locations { get; set; } = Array.Empty<Location>();
    [BindNever, ValidateNever] public string? CurrentSlug { get; set; }
    [BindNever, ValidateNever] public bool IsEdit => Id > 0;
}

// ---------------------------------------------------------------
// FAQs
// ---------------------------------------------------------------
public class AdminFaqListViewModel : AdminCmsListViewModel
{
    public IReadOnlyList<Faq> Faqs { get; set; } = Array.Empty<Faq>();

    /// <summary>"location", "route", "service", or null for all.</summary>
    public string? Owner { get; set; }
}

public class AdminFaqFormViewModel
{
    [BindNever, ValidateNever]
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter a question.")]
    [StringLength(300)]
    [Display(Name = "Question")]
    public string? Question { get; set; }

    [Required(ErrorMessage = "Enter an answer.")]
    [StringLength(2000, MinimumLength = 20,
        ErrorMessage = "A useful answer is at least 20 characters.")]
    [Display(Name = "Answer")]
    public string? Answer { get; set; }

    /// <summary>"location", "route" or "service".</summary>
    [Required(ErrorMessage = "Choose what this FAQ belongs to.")]
    [Display(Name = "Attach to")]
    public string? OwnerType { get; set; }

    [Display(Name = "Location")]
    public int? LocationId { get; set; }

    [Display(Name = "Route")]
    public int? RouteId { get; set; }

    [Display(Name = "Service")]
    public string? ServiceSlug { get; set; }

    [Range(0, 999)]
    [Display(Name = "Display order")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Published")]
    public bool IsPublished { get; set; }

    [BindNever, ValidateNever] public IReadOnlyList<Location> Locations { get; set; } = Array.Empty<Location>();
    [BindNever, ValidateNever] public IReadOnlyList<MovingRoute> Routes { get; set; } = Array.Empty<MovingRoute>();
    [BindNever, ValidateNever] public IReadOnlyList<Service> Services { get; set; } = Array.Empty<Service>();
    [BindNever, ValidateNever] public bool IsEdit => Id > 0;
}

// ---------------------------------------------------------------
// Audit log
// ---------------------------------------------------------------
public class AdminAuditListViewModel : AdminCmsListViewModel
{
    public IReadOnlyList<AdminAuditLog> Entries { get; set; } = Array.Empty<AdminAuditLog>();
    public AuditAction? Action { get; set; }
    public string? EntityType { get; set; }
}