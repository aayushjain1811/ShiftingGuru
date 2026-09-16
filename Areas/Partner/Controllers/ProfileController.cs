using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Areas.Partner.Controllers;

[Route("partner/profile")]
public class ProfileController : PartnerControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IPartnerService partners,
        UserManager<IdentityUser> users,
        ApplicationDbContext db,
        IServiceCatalog catalog,
        ILogger<ProfileController> logger)
        : base(partners, users)
    {
        _db = db;
        _catalog = catalog;
        _logger = logger;
    }

    // This controller writes, so it needs the tracked entity.
    protected override bool TracksVendor => true;

    // GET /partner/profile
    [HttpGet("")]
    public IActionResult Index()
    {
        var vendor = CurrentVendor;

        return View(Prepare(new VendorProfileViewModel
        {
            BusinessName = vendor.BusinessName,
            ContactPerson = vendor.ContactPerson,
            Phone = vendor.Phone,
            City = vendor.City,
            Address = vendor.Address,
            GstNumber = vendor.GstNumber,
            YearsOfExperience = vendor.YearsOfExperience,
            OperatingLocations = vendor.OperatingLocations,
            AdditionalInformation = vendor.AdditionalInformation,
            ServicesOffered = vendor.Services.Select(s => s.ServiceSlug).ToList()
        }));
    }

    // POST /partner/profile
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(VendorProfileViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(Prepare(model));

        var chosen = model.ServicesOffered
            .Select(slug => _catalog.GetBySlug(slug))
            .Where(s => s is not null)
            .Select(s => s!)
            .DistinctBy(s => s.Slug)
            .ToList();

        if (chosen.Count == 0)
        {
            ModelState.AddModelError(nameof(model.ServicesOffered),
                "Select at least one service you provide.");
            return View(Prepare(model));
        }

        // CurrentVendor came from the signed-in user, so this can only ever
        // write to the caller's own record. Status, VendorNumber, Email and
        // IdentityUserId are simply not touched.
        var vendor = CurrentVendor;

        vendor.BusinessName = model.BusinessName!.Trim();
        vendor.ContactPerson = model.ContactPerson!.Trim();
        vendor.Phone = model.Phone!.Trim();
        vendor.City = model.City!.Trim();
        vendor.Address = model.Address!.Trim();
        vendor.GstNumber = Normalise(model.GstNumber);
        vendor.YearsOfExperience = model.YearsOfExperience;
        vendor.OperatingLocations = Normalise(model.OperatingLocations);
        vendor.AdditionalInformation = Normalise(model.AdditionalInformation);
        vendor.UpdatedAt = DateTime.UtcNow;

        // Replace the service rows with the new selection.
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

        try
        {
            await _db.SaveChangesAsync(ct);
            TempData["PartnerMessage"] = "Your profile has been updated.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile update failed for vendor {VendorId}", vendor.Id);
            ModelState.AddModelError(string.Empty,
                "Couldn't save those changes. Please try again.");
            return View(Prepare(model));
        }

        return RedirectToAction(nameof(Index));
    }

    private VendorProfileViewModel Prepare(VendorProfileViewModel model)
    {
        ViewData["Title"] = "My profile";

        model.VendorNumber = CurrentVendor.VendorNumber;
        model.Email = CurrentVendor.Email;
        model.Status = CurrentVendor.Status;
        model.AvailableServices = _catalog.GetAll();

        return model;
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}