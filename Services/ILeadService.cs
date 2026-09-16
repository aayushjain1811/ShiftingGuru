using ShiftingGuru.Models;
using ShiftingGuru.ViewModels;

namespace ShiftingGuru.Services;

public interface ILeadService
{
    /// <summary>
    /// Maps a validated quote request onto a Lead and saves it.
    /// Returns the saved Lead, including its generated LeadNumber.
    /// Throws if the database is unavailable - the caller decides what the
    /// customer sees.
    /// </summary>
    Task<Lead> CreateLeadAsync(QuoteRequestViewModel model, Service service, CancellationToken ct = default);
}