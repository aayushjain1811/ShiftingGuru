using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Api;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): the partner's own profile - business details, services
/// and cities, and which documents are on file.
///
/// Edits the same Vendor row as the website's /partner/profile page, with the
/// same rules: only the signed-in partner's own record, and Status,
/// VendorNumber, Email and the sign-in are never touched.
/// </summary>
[Route("api/v1/partner")]
public class PartnerProfileController : ApiPartnerControllerBase
{
    private const int MaxAreaLength = 60;
    private const int MaxAreasTextLength = 500;

    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly IPartnerProfileReader _profiles;
    private readonly ILogger<PartnerProfileController> _logger;

    public PartnerProfileController(
        ApplicationDbContext db,
        IServiceCatalog catalog,
        IPartnerProfileReader profiles,
        ILogger<PartnerProfileController> logger)
    {
        _db = db;
        _catalog = catalog;
        _profiles = profiles;
        _logger = logger;
    }

    // This controller writes, so it needs the tracked vendor row.
    protected override bool TracksVendor => true;

    // GET /api/v1/partner/profile
    [HttpGet("profile")]
    public IActionResult GetProfile() => Ok(PartnerBusinessDto.From(CurrentVendor));

    // PUT /api/v1/partner/profile
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateBusinessRequest request, CancellationToken ct)
    {
        var vendor = CurrentVendor;

        vendor.BusinessName = request.BusinessName!.Trim();
        vendor.ContactPerson = request.ContactPerson!.Trim();
        vendor.City = request.City!.Trim();
        vendor.Address = request.Address!.Trim();
        vendor.GstNumber = Normalise(request.GstNumber)?.ToUpperInvariant();
        vendor.YearsOfExperience = request.YearsOfExperience;
        vendor.AdditionalInformation = Normalise(request.AdditionalInformation);
        vendor.UpdatedAt = DateTime.UtcNow;

        // The home city is always one of the covered cities, so it shouldn't
        // also sit in the extra list.
        vendor.OperatingLocations = JoinAreas(SplitAreas(vendor.OperatingLocations), vendor.City);

        if (!await SaveAsync(ct))
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiError("Couldn't save those changes. Please try again.", "saveFailed"));
        }

        return Ok(new UpdatedBusinessResponse(
            PartnerBusinessDto.From(vendor),
            await _profiles.ReadAsync(vendor, ct)));
    }

    // PUT /api/v1/partner/services-areas
    [HttpPut("services-areas")]
    public async Task<IActionResult> UpdateServicesAreas([FromBody] UpdateServicesAreasRequest request, CancellationToken ct)
    {
        var vendor = CurrentVendor;

        // Only slugs that exist in the catalog survive - same as the website.
        var chosen = request.Services
            .Select(slug => _catalog.GetBySlug(slug))
            .Where(s => s is not null)
            .Select(s => s!)
            .DistinctBy(s => s.Slug)
            .ToList();

        if (chosen.Count == 0)
        {
            return BadRequest(new ApiError("Select at least one service you provide.", "noServices"));
        }

        var areas = request.Areas
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .ToList();

        if (areas.Any(a => a.Length > MaxAreaLength))
        {
            return BadRequest(new ApiError("One of those city names is too long.", "badArea"));
        }

        var joined = JoinAreas(areas, vendor.City);

        if (joined is { Length: > MaxAreasTextLength })
        {
            return BadRequest(new ApiError("That's too many cities. Remove a few and try again.", "tooManyAreas"));
        }

        // Replace the service rows with the new selection - the same way the
        // website's profile page does it.
        _db.VendorServices.RemoveRange(vendor.Services);
        foreach (var service in chosen)
        {
            vendor.Services.Add(new VendorService
            {
                VendorId = vendor.Id,
                ServiceSlug = service.Slug,
                ServiceName = service.Name
            });
        }

        vendor.OperatingLocations = joined;
        vendor.UpdatedAt = DateTime.UtcNow;

        if (!await SaveAsync(ct))
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiError("Couldn't save those changes. Please try again.", "saveFailed"));
        }

        return Ok(await _profiles.ReadAsync(vendor, ct));
    }

    // GET /api/v1/partner/documents
    // Which documents are on file. The files themselves are only ever shown
    // to the admin team, exactly as on the website.
    [HttpGet("documents")]
    public async Task<IActionResult> Documents(CancellationToken ct)
    {
        var onFile = await _db.VendorDocuments
            .AsNoTracking()
            .Where(d => d.VendorId == CurrentVendor.Id)
            .Select(d => new { d.Type, d.OriginalFileName, d.UploadedAt })
            .ToListAsync(ct);

        var result = Enum.GetValues<VendorDocumentType>()
            .Select(type =>
            {
                var doc = onFile.FirstOrDefault(d => d.Type == type);
                return new PartnerDocumentDto(
                    type.ToString(), Label(type), doc is not null, doc?.OriginalFileName, doc?.UploadedAt);
            })
            .ToList();

        return Ok(result);
    }

    // -----------------------------------------------------------------

    private async Task<bool> SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile update from the app failed for vendor {VendorId}", CurrentVendor.Id);
            return false;
        }
    }

    private static List<string> SplitAreas(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>"Pune, Mumbai" - without repeats and without the home city. Null when empty.</summary>
    private static string? JoinAreas(IEnumerable<string> areas, string homeCity)
    {
        var list = areas
            .Where(a => !string.Equals(a, homeCity.Trim(), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return list.Count == 0 ? null : string.Join(", ", list);
    }

    private static string Label(VendorDocumentType type) => type switch
    {
        VendorDocumentType.GstCertificate => "GST certificate",
        VendorDocumentType.PanCard => "PAN card",
        VendorDocumentType.AadhaarFront => "Aadhaar card (front)",
        VendorDocumentType.AadhaarBack => "Aadhaar card (back)",
        VendorDocumentType.OfficePhoto => "Office photo",
        _ => "Document"
    };

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}