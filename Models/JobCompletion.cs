namespace ShiftingGuru.Models;

/// <summary>
/// NEW: how a booked job gets marked as done.
///
///   1. The partner taps "Mark job completed" in the app (after the move date).
///   2. The customer is asked "Did your move happen?":
///        - Yes                -> the job is completed
///        - There's a problem  -> the team is emailed with what they wrote,
///                                and the job stays open until an admin resolves it
///        - no answer in 3 days -> completed automatically
///
/// One row per booking (the lead), created when the partner marks it.
/// Deleted together with the lead.
/// </summary>
public class JobCompletion
{
    /// <summary>The booking (lead). Also the key - one row per booking.</summary>
    public int LeadId { get; set; }
    public Lead? Lead { get; set; }

    /// <summary>The partner who marked it - always the one the customer chose.</summary>
    public int VendorId { get; set; }

    public DateTime PartnerMarkedAt { get; set; }

    /// <summary>Optional note from the partner, e.g. "Delivered, customer signed".</summary>
    public string? PartnerNote { get; set; }

    public JobCustomerAnswer CustomerAnswer { get; set; } = JobCustomerAnswer.Waiting;
    public DateTime? CustomerAnsweredAt { get; set; }

    /// <summary>What the customer wrote when they reported a problem.</summary>
    public string? ProblemText { get; set; }

    /// <summary>Set when an admin has dealt with a reported problem.</summary>
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNote { get; set; }
}

public enum JobCustomerAnswer
{
    /// <summary>Asked, no answer yet.</summary>
    Waiting,

    /// <summary>The customer said yes.</summary>
    Confirmed,

    /// <summary>No answer within 3 days - completed automatically.</summary>
    AutoConfirmed,

    /// <summary>The customer reported a problem.</summary>
    Problem
}