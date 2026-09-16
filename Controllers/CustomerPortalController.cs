using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Customer;
using ShiftingGuru.ViewModels.Review;

namespace ShiftingGuru.Controllers;

/// <summary>
/// The customer's view of their own request.
///
/// Customers have no password. A cryptographically random link establishes a
/// cookie holding one claim: the lead id. Every action resolves the lead from
/// that cookie, never from the URL, so changing an id in the address bar can
/// never reach another customer's data.
/// </summary>
[Route("my-request")]
public class CustomerPortalController : Controller
{
    /// <summary>Separate from Identity's cookie - customers aren't Identity users.</summary>
    public const string Scheme = "CustomerPortal";
    public const string LeadIdClaim = "shiftingguru:lead";

    private readonly ApplicationDbContext _db;
    private readonly ICustomerAccessService _access;
    private readonly IQuoteSelectionService _selection;
    private readonly IReviewService _reviews;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CustomerPortalController> _logger;

    public CustomerPortalController(
        ApplicationDbContext db,
        ICustomerAccessService access,
        IQuoteSelectionService selection,
        IReviewService reviews,
        IWebHostEnvironment environment,
        ILogger<CustomerPortalController> logger)
    {
        _db = db;
        _access = access;
        _selection = selection;
        _reviews = reviews;
        _environment = environment;
        _logger = logger;
    }

    // GET /my-request/access/{token}
    [HttpGet("access/{token}")]
    public async Task<IActionResult> Access(string token, CancellationToken ct)
    {
        var leadId = await _access.ResolveLeadIdAsync(token, ct);

        if (leadId is null)
        {
            // Same page for expired, revoked and never-existed. Nothing about
            // whether a lead exists escapes.
            return View("AccessProblem");
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(LeadIdClaim, leadId.Value.ToString())
        }, Scheme);

        await HttpContext.SignInAsync(Scheme, new ClaimsPrincipal(identity));

        // Redirect so the token leaves the address bar immediately.
        return RedirectToAction(nameof(Index));
    }

    // GET /my-request
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        var quotes = await _selection.GetCustomerVisibleQuotesAsync(lead.Id, ct);

        CustomerQuoteCardViewModel? selected = null;
        if (lead.SelectedQuoteId is { } selectedId)
        {
            var match = quotes.FirstOrDefault(q => q.Id == selectedId);
            if (match is not null) selected = CustomerQuoteCardViewModel.From(match, true);
        }

        ViewData["Title"] = "Your moving request";

        return View(new CustomerRequestViewModel
        {
            Lead = lead,
            ServiceDetails = BuildServiceDetails(lead),
            QuoteCount = quotes.Count,
            SelectedQuote = selected
        });
    }

    // GET /my-request/quotes
    [HttpGet("quotes")]
    public async Task<IActionResult> Quotes(CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        var quotes = await _selection.GetCustomerVisibleQuotesAsync(lead.Id, ct);

        ViewData["Title"] = "Your quotes";

        return View(new CustomerQuoteListViewModel
        {
            Lead = lead,
            Quotes = quotes.Select(q => CustomerQuoteCardViewModel.From(q, lead.IsConverted)).ToList()
        });
    }

    // GET /my-request/quotes/42
    [HttpGet("quotes/{id:int}")]
    public async Task<IActionResult> QuoteDetails(int id, CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        var quote = await _selection.GetCustomerVisibleQuoteAsync(lead.Id, id, ct);
        if (quote is null) return View("QuoteNotFound");

        ViewData["Title"] = quote.QuoteNumber;

        return View(new CustomerQuoteDetailsViewModel
        {
            Lead = lead,
            Quote = CustomerQuoteCardViewModel.From(quote, lead.IsConverted)
        });
    }

    // GET /my-request/choose/42 - the confirmation screen, changes nothing.
    [HttpGet("choose/{quoteId:int}")]
    public async Task<IActionResult> Choose(int quoteId, CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        if (lead.IsConverted)
        {
            TempData["CustomerMessage"] = "You've already chosen a provider for this request.";
            return RedirectToAction(nameof(Index));
        }

        var quote = await _selection.GetCustomerVisibleQuoteAsync(lead.Id, quoteId, ct);
        if (quote is null) return View("QuoteNotFound");

        ViewData["Title"] = "Confirm your choice";

        return View(new CustomerVendorSelectionViewModel
        {
            Lead = lead,
            Quote = CustomerQuoteCardViewModel.From(quote, lead.IsConverted)
        });
    }

    // POST /my-request/choose/42 - the only action that changes anything.
    [HttpPost("choose/{quoteId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Choose(int quoteId, string confirm, CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        // leadId comes from the cookie; quoteId from the route is checked
        // against it inside the service.
        var result = await _selection.SelectAsync(lead.Id, quoteId, ct);

        if (!result.Succeeded)
        {
            TempData["CustomerError"] = result.Error;
            return RedirectToAction(nameof(Quotes));
        }

        TempData["SelectedQuoteId"] = result.Quote!.Id;
        return RedirectToAction(nameof(Confirmation));
    }

    // GET /my-request/confirmation
    [HttpGet("confirmation")]
    public async Task<IActionResult> Confirmation(CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        if (!lead.IsConverted || lead.SelectedQuoteId is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var quote = await _selection.GetCustomerVisibleQuoteAsync(lead.Id, lead.SelectedQuoteId.Value, ct);
        if (quote is null) return RedirectToAction(nameof(Index));

        ViewData["Title"] = "Provider selected";

        return View(new CustomerConfirmationViewModel
        {
            Lead = lead,
            Quote = CustomerQuoteCardViewModel.From(quote, true)
        });
    }

    // GET /my-request/review
    [HttpGet("review")]
    public async Task<IActionResult> Review(CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        var existing = await _reviews.GetForLeadAsync(lead.Id, ct);

        // Already reviewed: show it and its moderation state rather than a form.
        if (existing is not null)
        {
            ViewData["Title"] = "Your review";
            return View("ReviewStatus", new CustomerReviewViewModel
            {
                Review = existing,
                VendorName = lead.SelectedVendor?.BusinessName ?? "your provider"
            });
        }

        var eligibility = await _reviews.CheckEligibilityAsync(lead, ct);

        if (!eligibility.CanReview)
        {
            ViewData["Title"] = "Reviews";
            ViewData["ReviewBlockedReason"] = eligibility.Reason;
            return View("ReviewUnavailable");
        }

        ViewData["Title"] = "Leave a review";
        return View(BuildForm(new CreateReviewViewModel(), lead));
    }

    // POST /my-request/review
    [HttpPost("review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(CreateReviewViewModel model, CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        ViewData["Title"] = "Leave a review";

        var existing = await _reviews.GetForLeadAsync(lead.Id, ct);

        if (!ModelState.IsValid) return View(BuildForm(model, lead, existing is not null));

        // Create or edit. Both paths take the lead from the session, so a
        // tampered lead or vendor id in the form has nowhere to go.
        var result = existing is null
            ? await _reviews.CreateAsync(lead, model, ct)
            : await _reviews.UpdateAsync(lead.Id, model, ct);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(BuildForm(model, lead, existing is not null));
        }

        return RedirectToAction(nameof(ReviewSubmitted));
    }

    // GET /my-request/review/edit
    [HttpGet("review/edit")]
    public async Task<IActionResult> EditReview(CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        var existing = await _reviews.GetForLeadAsync(lead.Id, ct);
        if (existing is null) return RedirectToAction(nameof(Review));

        // Approved reviews are already public; editing one would bypass the
        // moderation that let it through.
        if (!existing.IsEditableByCustomer)
        {
            TempData["CustomerMessage"] = "This review has been moderated and can no longer be edited.";
            return RedirectToAction(nameof(Review));
        }

        ViewData["Title"] = "Edit your review";

        return View("Review", BuildForm(new CreateReviewViewModel
        {
            Rating = existing.Rating,
            Title = existing.Title,
            Comment = existing.Comment
        }, lead, isEdit: true));
    }

    // GET /my-request/review/submitted
    [HttpGet("review/submitted")]
    public async Task<IActionResult> ReviewSubmitted(CancellationToken ct)
    {
        var lead = await CurrentLeadAsync(ct);
        if (lead is null) return RedirectToAction(nameof(RequestAccess));

        ViewData["Title"] = "Thanks for your feedback";
        return View();
    }

    private CreateReviewViewModel BuildForm(
        CreateReviewViewModel model, Lead lead, bool isEdit = false)
    {
        model.VendorName = lead.SelectedVendor?.BusinessName ?? "your provider";
        model.LeadNumber = lead.LeadNumber;
        model.Route = lead.MovingFrom is null
            ? lead.StorageLocation ?? "-"
            : $"{lead.MovingFrom} to {lead.MovingTo}";
        model.IsEdit = isEdit;

        return model;
    }

    // GET /my-request/request-access
    [HttpGet("request-access")]
    public IActionResult RequestAccess()
    {
        ViewData["Title"] = "Access your request";
        return View(new RequestAccessViewModel());
    }

    // POST /my-request/request-access
    [HttpPost("request-access")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestAccess(RequestAccessViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Access your request";

        if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Phone))
        {
            ModelState.AddModelError(string.Empty, "Enter the email address or mobile number you used.");
            return View(model);
        }

        if (!ModelState.IsValid) return View(model);

        var link = await _access.IssueForContactAsync(model.Email, model.Phone, ct);

        // The same response either way. A match and a miss are indistinguishable,
        // so this form can't be used to discover who's a customer.
        model.Submitted = true;

        // Development only, and clearly labelled as such: there is no email or
        // SMS provider, so the link is shown rather than pretended to be sent.
        if (link is not null && _environment.IsDevelopment())
        {
            model.DevelopmentLink = link.Url;
        }

        return View(model);
    }

    // POST /my-request/sign-out
    [HttpPost("sign-out")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutCustomer()
    {
        await HttpContext.SignOutAsync(Scheme);
        return Redirect("/");
    }

    /// <summary>
    /// The lead for the current customer cookie. Everything in this controller
    /// goes through here - no action ever takes a lead id from the request.
    /// </summary>
    private async Task<Lead?> CurrentLeadAsync(CancellationToken ct)
    {
        var result = await HttpContext.AuthenticateAsync(Scheme);
        if (!result.Succeeded) return null;

        var claim = result.Principal?.FindFirst(LeadIdClaim)?.Value;
        if (!int.TryParse(claim, out var leadId)) return null;

        return await _db.Leads
            .AsNoTracking()
            .Include(l => l.SelectedVendor)
            .FirstOrDefaultAsync(l => l.Id == leadId, ct);
    }

    private static IReadOnlyList<(string, string)> BuildServiceDetails(Lead lead)
    {
        var candidates = new (string Label, string? Value)[]
        {
            ("Property type", lead.PropertyType),
            ("Approximate size", lead.MoveSize),
            ("Office size", lead.OfficeSize),
            ("Desks / employees", lead.DeskCount),
            ("Vehicle type", lead.VehicleType),
            ("Brand and model", lead.VehicleModel),
            ("Vehicle condition", lead.VehicleCondition),
            ("Type of goods", lead.GoodsType),
            ("Load details", lead.LoadDetails),
            ("Vehicle requirement", lead.VehicleRequirement),
            ("Storage type", lead.StorageType),
            ("Storage size", lead.StorageSize),
            ("Expected duration", lead.StorageDuration)
        };

        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .Select(c => (c.Label, c.Value!))
            .ToList();
    }
}