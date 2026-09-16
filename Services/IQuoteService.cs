using ShiftingGuru.Models;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Services;

public record QuoteResult(bool Succeeded, Quote? Quote, string? Error)
{
    public static QuoteResult Ok(Quote quote) => new(true, quote, null);
    public static QuoteResult Fail(string error) => new(false, null, error);
}

public interface IQuoteService
{
    /// <summary>
    /// Creates a quote, but only if this vendor holds a live assignment for
    /// this lead and has no active quote on it already.
    /// </summary>
    Task<QuoteResult> SubmitAsync(int leadId, int vendorId, SubmitQuoteViewModel model, CancellationToken ct = default);

    /// <summary>Updates a quote the vendor owns and is still allowed to change.</summary>
    Task<QuoteResult> UpdateAsync(int quoteId, int vendorId, SubmitQuoteViewModel model, CancellationToken ct = default);

    /// <summary>Admin status change. Returns false if the move isn't legal.</summary>
    Task<bool> ChangeStatusAsync(int quoteId, QuoteStatus status, CancellationToken ct = default);

    IReadOnlyList<QuoteStatus> AllowedTransitionsFrom(QuoteStatus current);
}