using ShiftingGuru.Models;

namespace ShiftingGuru.Areas.Admin;

/// <summary>
/// Colour classes per status. Lives in a .cs file inside /Areas, which
/// Tailwind scans, so these class names survive the CSS build.
/// </summary>
public static class StatusBadge
{
    public static string Classes(LeadStatus status) => status switch
    {
        LeadStatus.New => "bg-jade-100 text-jade-600 border-jade-500/30",
        LeadStatus.Contacted => "bg-sky-50 text-sky-700 border-sky-200",
        LeadStatus.Assigned => "bg-indigo-50 text-indigo-700 border-indigo-200",
        LeadStatus.InProgress => "bg-amber-50 text-amber-700 border-amber-200",
        LeadStatus.Quoted => "bg-violet-50 text-violet-700 border-violet-200",
        LeadStatus.Converted => "bg-emerald-50 text-emerald-700 border-emerald-200",
        LeadStatus.Closed => "bg-ink-100 text-ink-600 border-ink-200",
        LeadStatus.Cancelled => "bg-red-50 text-red-700 border-red-200",
        _ => "bg-ink-100 text-ink-600 border-ink-200"
    };

    public static string Label(LeadStatus status) =>
        status == LeadStatus.InProgress ? "In progress" : status.ToString();
}

/// <summary>Colour classes for vendor application status.</summary>
public static class VendorBadge
{
    public static string Classes(VendorStatus status) => status switch
    {
        VendorStatus.Pending => "bg-amber-50 text-amber-700 border-amber-200",
        VendorStatus.Approved => "bg-emerald-50 text-emerald-700 border-emerald-200",
        VendorStatus.Rejected => "bg-red-50 text-red-700 border-red-200",
        VendorStatus.Suspended => "bg-ink-100 text-ink-600 border-ink-200",
        _ => "bg-ink-100 text-ink-600 border-ink-200"
    };
}

/// <summary>Colour classes for lead assignment status.</summary>
public static class AssignmentBadge
{
    public static string Classes(AssignmentStatus status) => status switch
    {
        AssignmentStatus.Assigned => "bg-sky-50 text-sky-700 border-sky-200",
        AssignmentStatus.Viewed => "bg-violet-50 text-violet-700 border-violet-200",
        AssignmentStatus.Declined => "bg-red-50 text-red-700 border-red-200",
        AssignmentStatus.Completed => "bg-emerald-50 text-emerald-700 border-emerald-200",
        AssignmentStatus.Expired => "bg-ink-100 text-ink-600 border-ink-200",
        AssignmentStatus.Cancelled => "bg-ink-100 text-ink-500 border-ink-200",
        _ => "bg-ink-100 text-ink-600 border-ink-200"
    };

    public static string MatchClasses(ShiftingGuru.ViewModels.Admin.MatchLevel level) => level switch
    {
        ShiftingGuru.ViewModels.Admin.MatchLevel.Match => "bg-emerald-50 text-emerald-700 border-emerald-200",
        ShiftingGuru.ViewModels.Admin.MatchLevel.Partial => "bg-amber-50 text-amber-700 border-amber-200",
        _ => "bg-ink-100 text-ink-500 border-ink-200"
    };

    public static string MatchLabel(ShiftingGuru.ViewModels.Admin.MatchLevel level) => level switch
    {
        ShiftingGuru.ViewModels.Admin.MatchLevel.Match => "Match",
        ShiftingGuru.ViewModels.Admin.MatchLevel.Partial => "Partial",
        _ => "No"
    };
}

/// <summary>Colour classes for quote status.</summary>
public static class QuoteBadge
{
    public static string Classes(QuoteStatus status) => status switch
    {
        QuoteStatus.Draft => "bg-ink-100 text-ink-600 border-ink-200",
        QuoteStatus.Submitted => "bg-sky-50 text-sky-700 border-sky-200",
        QuoteStatus.UnderReview => "bg-amber-50 text-amber-700 border-amber-200",
        QuoteStatus.Accepted => "bg-emerald-50 text-emerald-700 border-emerald-200",
        QuoteStatus.NotSelected => "bg-ink-100 text-ink-600 border-ink-200",
        QuoteStatus.Rejected => "bg-red-50 text-red-700 border-red-200",
        QuoteStatus.Expired => "bg-ink-100 text-ink-500 border-ink-200",
        QuoteStatus.Cancelled => "bg-ink-100 text-ink-500 border-ink-200",
        _ => "bg-ink-100 text-ink-600 border-ink-200"
    };

    public static string Label(QuoteStatus status) => status switch
    {
        QuoteStatus.UnderReview => "Under review",
        QuoteStatus.NotSelected => "Not selected",
        _ => status.ToString()
    };
}

/// <summary>Colour classes for notification delivery status.</summary>
public static class NotificationBadge
{
    public static string Classes(NotificationStatus status) => status switch
    {
        NotificationStatus.Sent => "bg-emerald-50 text-emerald-700 border-emerald-200",
        NotificationStatus.Pending => "bg-amber-50 text-amber-700 border-amber-200",
        NotificationStatus.Failed => "bg-red-50 text-red-700 border-red-200",
        _ => "bg-ink-100 text-ink-600 border-ink-200"
    };

    /// <summary>CustomerLeadCreated -> "Customer lead created".</summary>
    public static string Label(NotificationType type)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(
            type.ToString(), "(?<!^)([A-Z])", " $1");

        return char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();
    }
}

/// <summary>Colour classes for review moderation status.</summary>
public static class ReviewBadge
{
    public static string Classes(ReviewStatus status) => status switch
    {
        ReviewStatus.Pending => "bg-amber-50 text-amber-700 border-amber-200",
        ReviewStatus.Approved => "bg-emerald-50 text-emerald-700 border-emerald-200",
        ReviewStatus.Rejected => "bg-red-50 text-red-700 border-red-200",
        ReviewStatus.Hidden => "bg-ink-100 text-ink-600 border-ink-200",
        _ => "bg-ink-100 text-ink-600 border-ink-200"
    };

    /// <summary>"★★★★☆" for 4. Decorative - always pair with a text rating.</summary>
    public static string Stars(int rating) =>
        new string('\u2605', Math.Clamp(rating, 0, 5)) +
        new string('\u2606', Math.Clamp(5 - rating, 0, 5));
}