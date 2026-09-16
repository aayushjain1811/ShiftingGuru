using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Areas.Partner.Controllers;

/// <summary>
/// A vendor's own quotes. Every query is scoped to CurrentVendor.Id, which
/// comes from the signed-in Identity user - never from the route or the form.
/// </summary>
[Route("partner/quotes")]
public class QuotesController : PartnerControllerBase
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _db;
    private readonly IQuoteService _quotes;

    public QuotesController(
        IPartnerService partners,
        UserManager<IdentityUser> users,
        ApplicationDbContext db,
        IQuoteService quotes)
        : base(partners, users)
    {
        _db = db;
        _quotes = quotes;
    }

    // GET /partner/quotes
    [HttpGet("")]
    public async Task<IActionResult> Index(QuoteStatus? status, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "My quotes";
        if (page < 1) page = 1;

        var query = _db.Quotes.AsNoTracking().Where(q => q.VendorId == CurrentVendor.Id);

        if (status.HasValue && Enum.IsDefined(status.Value))
        {
            query = query.Where(q => q.Status == status.Value);
        }

        var total = await query.CountAsync(ct);

        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var rows = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(q => new PartnerQuoteRow
            {
                QuoteId = q.Id,
                QuoteNumber = q.QuoteNumber,
                LeadNumber = q.Lead!.LeadNumber,
                ServiceName = q.Lead.ServiceName,
                MovingFrom = q.Lead.MovingFrom,
                MovingTo = q.Lead.MovingTo,
                StorageLocation = q.Lead.StorageLocation,
                TotalAmount = q.TotalAmount,
                Status = q.Status,
                CreatedAt = q.CreatedAt
            })
            .ToListAsync(ct);

        return View(new PartnerQuoteListViewModel
        {
            Quotes = rows,
            Status = status,
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        });
    }

    // GET /partner/quotes/42
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var quote = await LoadOwnedAsync(id, ct);
        if (quote?.Lead is null) return View("NotFound");

        ViewData["Title"] = quote.QuoteNumber;

        return View(new PartnerQuoteDetailsViewModel { Quote = quote, Lead = quote.Lead });
    }

    // GET /partner/quotes/42/edit
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var quote = await LoadOwnedAsync(id, ct);
        if (quote?.Lead is null) return View("NotFound");

        if (!quote.IsEditableByVendor)
        {
            TempData["PartnerMessage"] = $"A {quote.Status} quote can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewData["Title"] = "Edit quote";

        return View(new EditQuoteViewModel
        {
            BasePrice = quote.BasePrice,
            PackingCharges = quote.PackingCharges,
            TransportationCharges = quote.TransportationCharges,
            LoadingUnloadingCharges = quote.LoadingUnloadingCharges,
            AdditionalCharges = quote.AdditionalCharges,
            EstimatedPickupDate = quote.EstimatedPickupDate,
            EstimatedDeliveryDate = quote.EstimatedDeliveryDate,
            EstimatedDeliveryDays = quote.EstimatedDeliveryDays,
            VendorNotes = quote.VendorNotes,
            Lead = quote.Lead,
            QuoteNumber = quote.QuoteNumber,
            Status = quote.Status
        });
    }

    // POST /partner/quotes/42/edit
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditQuoteViewModel model, CancellationToken ct)
    {
        var quote = await LoadOwnedAsync(id, ct);
        if (quote?.Lead is null) return View("NotFound");

        model.Lead = quote.Lead;
        model.QuoteNumber = quote.QuoteNumber;
        model.Status = quote.Status;

        if (!ModelState.IsValid) return View(model);

        // Vendor id passed explicitly from the authenticated context.
        var result = await _quotes.UpdateAsync(id, CurrentVendor.Id, model, ct);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["PartnerMessage"] = "Your quote has been updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Loads a quote only if this vendor owns it. The ownership filter is part
    /// of the query, so another vendor's quote is indistinguishable from one
    /// that doesn't exist.
    /// </summary>
    private Task<Quote?> LoadOwnedAsync(int id, CancellationToken ct) =>
        _db.Quotes
            .AsNoTracking()
            .Include(q => q.Lead)
            .FirstOrDefaultAsync(q => q.Id == id && q.VendorId == CurrentVendor.Id, ct);
}