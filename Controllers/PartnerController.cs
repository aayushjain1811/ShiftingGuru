using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Controllers;

/// <summary>
/// The public-facing partner pages. Signed-in partner functionality lives in
/// the Partner area; this is just the front door.
/// </summary>
public class PartnerController : Controller
{
    private readonly IServiceCatalog _catalog;
    private readonly IPartnerService _partners;
    private readonly ILogger<PartnerController> _logger;

    public PartnerController(
        IServiceCatalog catalog, IPartnerService partners, ILogger<PartnerController> logger)
    {
        _catalog = catalog;
        _partners = partners;
        _logger = logger;
    }

    // GET /join-as-partner
    [HttpGet("/join-as-partner")]
    public IActionResult Join() => View(Prepare(new PartnerRegistrationViewModel()));

    // POST /join-as-partner
    [HttpPost("/join-as-partner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(PartnerRegistrationViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(Prepare(model));

        var result = await _partners.RegisterAsync(model, ct);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
            return View(Prepare(model));
        }

        // Only the business name crosses over - no contact details in TempData.
        TempData["PartnerBusinessName"] = result.Vendor!.BusinessName;
        TempData["PartnerVendorNumber"] = result.Vendor.VendorNumber;

        return RedirectToAction(nameof(Success));
    }

    // GET /join-as-partner/success
    [HttpGet("/join-as-partner/success")]
    public IActionResult Success()
    {
        var vendorNumber = TempData["PartnerVendorNumber"] as string;
        if (string.IsNullOrEmpty(vendorNumber)) return RedirectToAction(nameof(Join));

        ViewData["Title"] = "Application received";
        ViewData["VendorNumber"] = vendorNumber;
        ViewData["BusinessName"] = TempData["PartnerBusinessName"] as string;

        return View();
    }

    private PartnerRegistrationViewModel Prepare(PartnerRegistrationViewModel model)
    {
        model.AvailableServices = _catalog.GetAll();

        ViewData["Title"] = "Join as a Partner";
        ViewData["MetaDescription"] =
            "Register your moving or logistics business with ShiftingGuru and receive "
            + "relevant customer enquiries from across India.";
        ViewData["Canonical"] = "https://www.shiftingguru.com/join-as-partner";

        return model;
    }
}