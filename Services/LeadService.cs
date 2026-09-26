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
        // CHANGED: stored as the plain 10 digits, so "+91 98765 43210" and
        // "9876543210" are the same customer.
        var phone = MobileDigits(model.Phone) ?? Normalise(model.Phone) ?? "";

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
            AdditionalRequirements = Normalise(model.AdditionalRequirements),

            MovingDate = model.MovingDate.HasValue
                ? DateOnly.FromDateTime(model.MovingDate.Value)
                : null,

            // Server-controlled. Never taken from the form.
            Status = LeadStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        // CHANGED: the short form has one "from" box. For storage it means
        // "where do you need storage", which is kept in StorageLocation as before.
        if (service.Slug == QuoteRequestViewModel.StorageSlug)
        {
            lead.StorageLocation = Normalise(model.MovingFrom);
        }
        else
        {
            lead.MovingFrom = Normalise(model.MovingFrom);
            lead.MovingTo = Normalise(model.MovingTo);
        }

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        return lead;
    }

    /// <summary>
    /// SG-yyyyMMdd-#####. The counter comes from a PostgreSQL sequence, which
    /// is atomic - two simultaneous submissions cannot produce the same number.
    /// </summary>
    private async Task<string> NextLeadNumberAsync(CancellationToken ct)
    {
        var next = await _db.Database
            .SqlQueryRaw<long>("SELECT nextval('lead_number_seq') AS \"Value\"")
            .SingleAsync(ct);

        return $"SG-{DateTime.UtcNow:yyyyMMdd}-{next:D5}";
    }

    /// <summary>Last 10 digits of an Indian mobile number, or null if it isn't one.</summary>
    public static string? MobileDigits(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.Length > 10) digits = digits[^10..];
        return digits.Length == 10 && digits[0] >= '6' ? digits : null;
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}