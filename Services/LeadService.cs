using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.ViewModels;

namespace ShiftingGuru.Services;

public class LeadService : ILeadService
{
    private readonly ApplicationDbContext _db;

    // How long two identical submissions are treated as the same request.
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(2);

    public LeadService(ApplicationDbContext db) => _db = db;

    public async Task<Lead> CreateLeadAsync(
        QuoteRequestViewModel model, Service service, CancellationToken ct = default)
    {
        var phone = Normalise(model.Phone) ?? "";

        // Double-click / refresh guard. If the same number sent the same
        // service in the last couple of minutes, return that lead instead of
        // creating a second one. Cheap: covered by the (Phone, CreatedAt) index.
        var cutoff = DateTime.UtcNow - DuplicateWindow;

        var existing = await _db.Leads
            .AsNoTracking()
            .Where(l => l.Phone == phone
                     && l.ServiceSlug == service.Slug
                     && l.CreatedAt >= cutoff)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null) return existing;

        var lead = new Lead
        {
            LeadNumber = await NextLeadNumberAsync(ct),
            ServiceSlug = service.Slug,
            ServiceName = service.Name,

            CustomerName = Normalise(model.CustomerName) ?? "",
            Phone = phone,
            Email = Normalise(model.Email),
            PreferredContactMethod = Normalise(model.PreferredContactMethod),
            AdditionalRequirements = Normalise(model.AdditionalRequirements),

            MovingDate = model.MovingDate.HasValue
                ? DateOnly.FromDateTime(model.MovingDate.Value)
                : null,

            // Server-controlled. Never taken from the form.
            Status = LeadStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        ApplyServiceFields(lead, model, service.Slug);

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        return lead;
    }

    /// <summary>
    /// Copies across only the fields that belong to the chosen service, so a
    /// car-transport lead never carries a stray PropertyType.
    /// </summary>
    private static void ApplyServiceFields(Lead lead, QuoteRequestViewModel model, string slug)
    {
        if (slug == "warehouse-storage")
        {
            lead.StorageLocation = Normalise(model.StorageLocation);
            lead.StorageType = Normalise(model.StorageType);
            lead.StorageSize = Normalise(model.StorageSize);
            lead.StorageDuration = Normalise(model.StorageDuration);
            return;
        }

        lead.MovingFrom = Normalise(model.MovingFrom);
        lead.MovingTo = Normalise(model.MovingTo);

        switch (slug)
        {
            case "home-shifting":
            case "packers-movers":
                lead.PropertyType = Normalise(model.PropertyType);
                lead.MoveSize = Normalise(model.MoveSize);
                break;

            case "office-shifting":
                lead.OfficeSize = Normalise(model.OfficeSize);
                lead.DeskCount = Normalise(model.DeskCount);
                break;

            case "car-transportation":
                lead.VehicleType = Normalise(model.VehicleType);
                lead.VehicleModel = Normalise(model.VehicleModel);
                lead.VehicleCondition = Normalise(model.VehicleCondition);
                break;

            case "bike-transportation":
                lead.VehicleType = Normalise(model.VehicleType);
                lead.VehicleModel = Normalise(model.VehicleModel);
                break;

            case "goods-transportation":
            case "truck-tempo":
                lead.GoodsType = Normalise(model.GoodsType);
                lead.LoadDetails = Normalise(model.LoadDetails);
                lead.VehicleRequirement = Normalise(model.VehicleRequirement);
                break;
        }
    }

    /// <summary>
    /// SG-yyyyMMdd-#####. The counter comes from a PostgreSQL sequence, which
    /// is atomic - two simultaneous submissions cannot produce the same number.
    /// A timestamp alone could, which is why this is not timestamp-only.
    /// </summary>
    private async Task<string> NextLeadNumberAsync(CancellationToken ct)
    {
        var next = await _db.Database
            .SqlQueryRaw<long>("SELECT nextval('lead_number_seq') AS \"Value\"")
            .SingleAsync(ct);

        return $"SG-{DateTime.UtcNow:yyyyMMdd}-{next:D5}";
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}