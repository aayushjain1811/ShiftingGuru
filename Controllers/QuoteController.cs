using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShiftingGuru.Services.Email;
using ShiftingGuru.Data;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.ViewModels;

namespace ShiftingGuru.Controllers;

[Route("quote")]
public class QuoteController : Controller
{
    private readonly IServiceCatalog _catalog;
    private readonly ILeadService _leads;
    private readonly ICustomerAccessService _access;
    private readonly INotificationService _notifications;
    private readonly AppOptions _app;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<QuoteController> _logger;

    public QuoteController(
        IServiceCatalog catalog,
        ILeadService leads,
        ICustomerAccessService access,
        INotificationService notifications,
        IOptions<AppOptions> app,
        IWebHostEnvironment environment,
        ILogger<QuoteController> logger)
    {
        _catalog = catalog;
        _leads = leads;
        _access = access;
        _notifications = notifications;
        _app = app.Value;
        _environment = environment;
        _logger = logger;
    }

    // GET /quote
    // GET /quote?service=home-shifting&from=Gurgaon&to=Pune
    [HttpGet("")]
    public IActionResult Index(string? service, string? from, string? to, DateTime? date)
    {
        var model = new QuoteRequestViewModel
        {
            MovingFrom = from,
            MovingTo = to,
            MovingDate = date
        };

        // Never trust the slug from the URL. An unknown value simply means
        // nothing is pre-selected - no exception, no error message.
        var matched = service is null ? null : _catalog.GetBySlug(service);
        if (matched is not null)
        {
            model.ServiceSlug = matched.Slug;
            model.ServiceName = matched.Name;
        }

        return View(Prepare(model));
    }

    // POST /quote
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(QuoteRequestViewModel model, CancellationToken ct)
    {
        var service = model.ServiceSlug is null ? null : _catalog.GetBySlug(model.ServiceSlug);
        if (service is null)
        {
            ModelState.AddModelError(nameof(model.ServiceSlug), "Please choose a service.");
        }
        else
        {
            model.ServiceName = service.Name;
        }

        if (!ModelState.IsValid)
        {
            return View(Prepare(model));
        }

        try
        {
            var lead = await _leads.CreateLeadAsync(model, service!, ct);

            // Only the reference and service name cross to the success page.
            // No name, phone or email in TempData or the URL.
            TempData["LeadNumber"] = lead.LeadNumber;
            TempData["SubmittedService"] = lead.ServiceName;

            // Passwordless portal access. The raw token exists only in this
            // link - it is never stored or logged.
            var link = await _access.IssueAsync(lead.Id, ct);

            // Sent after the lead is committed, never before: no email about a
            // lead that failed to save. Notification failures don't reach here.
            await _notifications.LeadCreatedAsync(lead, _app.Url(link.Url), ct);

            // No delivery provider in development, so the link is shown on the
            // success page rather than pretended sent.
            if (_environment.IsDevelopment())
            {
                TempData["AccessLink"] = link.Url;
            }

            return RedirectToAction(nameof(Success));
        }
        catch (Exception ex)
        {
            // Logged without customer details - no name, phone or email.
            _logger.LogError(ex,
                "Failed to save lead for service {ServiceSlug}", model.ServiceSlug);

            ModelState.AddModelError(string.Empty,
                "Sorry, we couldn't submit your request just now. Please try again in a moment.");

            return View(Prepare(model));
        }
    }

    // GET /quote/success
    [HttpGet("success")]
    public IActionResult Success()
    {
        var leadNumber = TempData["LeadNumber"] as string;

        // Reaching this page without submitting anything sends you back to
        // the form rather than showing a bare confirmation.
        if (string.IsNullOrEmpty(leadNumber))
        {
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Request Received";
        ViewData["MetaDescription"] = "Your moving requirement has been submitted to ShiftingGuru.";
        ViewData["Canonical"] = "https://www.shiftingguru.com/quote/success";

        ViewData["LeadNumber"] = leadNumber;
        ViewData["SubmittedService"] = TempData["SubmittedService"] as string;
        ViewData["AccessLink"] = TempData["AccessLink"] as string;
        ViewData["AccessLink"] = TempData["AccessLink"] as string;

        return View();
    }

    private QuoteRequestViewModel Prepare(QuoteRequestViewModel model)
    {
        model.Services = _catalog.GetAll();

        ViewData["Title"] = "Get Free Quotes";
        ViewData["MetaDescription"] =
            "Tell ShiftingGuru what you're moving and compare options from verified "
            + "moving and logistics professionals across India.";
        ViewData["Canonical"] = "https://www.shiftingguru.com/quote";

        return model;
    }
}