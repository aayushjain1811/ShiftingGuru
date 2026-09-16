using ShiftingGuru.Models;

namespace ShiftingGuru.Services;

public record SelectionResult(bool Succeeded, Quote? Quote, string? Error)
{
    public static SelectionResult Ok(Quote quote) => new(true, quote, null);
    public static SelectionResult Fail(string error) => new(false, null, error);
}

public interface IQuoteSelectionService
{
    /// <summary>Quotes this lead's customer is allowed to see.</summary>
    Task<IReadOnlyList<Quote>> GetCustomerVisibleQuotesAsync(int leadId, CancellationToken ct = default);

    /// <summary>One customer-visible quote, or null if it isn't theirs.</summary>
    Task<Quote?> GetCustomerVisibleQuoteAsync(int leadId, int quoteId, CancellationToken ct = default);

    /// <summary>
    /// Accepts one quote, converts the lead and marks the rest NotSelected,
    /// atomically. Safe to call concurrently: exactly one caller can win.
    /// </summary>
    Task<SelectionResult> SelectAsync(int leadId, int quoteId, CancellationToken ct = default);
}