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

    /// <summary>
    /// NEW (mobile app): saves a request made in the customer app. Same lead
    /// number, same double-tap guard and same starting status as the website
    /// form - plus the service details the app asks for, and the link to the
    /// customer's account. Contact details come from the account, never the app.
    /// </summary>
    Task<Lead> CreateAppLeadAsync(AppLeadInput input, Service service, CancellationToken ct = default);
}

/// <summary>
/// NEW (mobile app): a request from the customer app, already checked and
/// trimmed to the Leads column sizes by the API.
/// </summary>
public class AppLeadInput
{
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = "";
    public string Phone { get; init; } = "";
    public string? Email { get; init; }

    public string? MovingFrom { get; init; }
    public string? MovingTo { get; init; }
    public string? StorageLocation { get; init; }
    public DateOnly? MovingDate { get; init; }

    public string? PropertyType { get; init; }
    public string? MoveSize { get; init; }
    public string? OfficeSize { get; init; }
    public string? DeskCount { get; init; }
    public string? VehicleType { get; init; }
    public string? VehicleModel { get; init; }
    public string? VehicleCondition { get; init; }
    public string? GoodsType { get; init; }
    public string? LoadDetails { get; init; }
    public string? VehicleRequirement { get; init; }
    public string? StorageType { get; init; }
    public string? StorageSize { get; init; }
    public string? StorageDuration { get; init; }

    public string? AdditionalRequirements { get; init; }
}