using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.Services.Storage;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin/vendors")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class VendorsController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly IPartnerService _partners;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IDocumentStorage _storage;
    private readonly ILogger<VendorsController> _logger;

    public VendorsController(
        ApplicationDbContext db,
        IServiceCatalog catalog,
        IPartnerService partners,
        INotificationService notifications,
        IAuditService audit,
        IDocumentStorage storage,
        ILogger<VendorsController> logger)
    {
        _db = db;
        _catalog = catalog;
        _partners = partners;
        _notifications = notifications;
        _audit = audit;
        _storage = storage;
        _logger = logger;
    }

    // GET /admin/vendors
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, VendorStatus? status, string? service,
        int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Vendors";
        if (page < 1) page = 1;

        var query = _db.Vendors.AsNoTracking().Include(v => v.Services).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(v =>
                EF.Functions.ILike(v.VendorNumber, term) ||
                EF.Functions.ILike(v.BusinessName, term) ||
                EF.Functions.ILike(v.ContactPerson, term) ||
                EF.Functions.ILike(v.Phone, term) ||
                EF.Functions.ILike(v.Email, term) ||
                EF.Functions.ILike(v.City, term));
        }

        if (status.HasValue && Enum.IsDefined(status.Value))
        {
            query = query.Where(v => v.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(service) && _catalog.GetBySlug(service) is { } matched)
        {
            // Translated to EXISTS in SQL - no vendor rows loaded to filter.
            query = query.Where(v => v.Services.Any(s => s.ServiceSlug == matched.Slug));
        }
        else
        {
            service = null;
        }

        var total = await query.CountAsync(ct);

        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var vendors = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        return View(new AdminVendorListViewModel
        {
            Vendors = vendors,
            Search = search,
            Status = status,
            ServiceSlug = service,
            Page = page,
            PageSize = PageSize,
            TotalCount = total,
            Services = _catalog.GetAll()
        });
    }

    // GET /admin/vendors/42
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var vendor = await _db.Vendors
            .AsNoTracking()
            .Include(v => v.Services)
            .Include(v => v.Documents)   // NEW
            .AsSplitQuery()              // two collections: avoids one big joined result
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (vendor is null) return View("NotFound");

        ViewData["Title"] = vendor.BusinessName;

        return View(new AdminVendorDetailsViewModel
        {
            Vendor = vendor,
            AllowedTransitions = _partners.AllowedTransitionsFrom(vendor.Status)
        });
    }

    // NEW
    // GET /admin/vendors/42/documents/7
    // The ONLY way a partner document is ever shown. Admin-only (class-level
    // [Authorize]), and the document must belong to that vendor - asking for
    // vendor 42's document 7 when it belongs to vendor 43 is a 404.
    [HttpGet("{id:int}/documents/{documentId:int}")]
    public async Task<IActionResult> Document(int id, int documentId, CancellationToken ct)
    {
        var document = await _db.VendorDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.VendorId == id, ct);

        if (document is null) return NotFound();

        var content = await _storage.OpenReadAsync(document.StoragePath, ct);
        if (content is null)
        {
            _logger.LogError("Document {DocumentId} for vendor {VendorId} is missing from storage.", documentId, id);
            return NotFound();
        }

        // ID documents: never cached by the browser or anything in between,
        // shown inline (not downloaded), and never "guessed" as another type.
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentDisposition =
            new ContentDispositionHeaderValue("inline") { FileNameStar = document.OriginalFileName }.ToString();

        return File(content, document.ContentType);
    }

    // POST /admin/vendors/42/status
    [HttpPost("{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, VendorStatus status, CancellationToken ct)
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vendor is null) return View("NotFound");

        // Two checks: the value must be a real enum member, and the move must
        // be legal from where the vendor currently is. Approving an already
        // approved vendor, or any move the UI doesn't offer, is rejected here
        // rather than trusted because it arrived in a POST.
        if (!Enum.IsDefined(status) ||
            !_partners.AllowedTransitionsFrom(vendor.Status).Contains(status))
        {
            TempData["AdminError"] = $"Can't move a {vendor.Status} vendor to {status}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        vendor.Status = status;
        vendor.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);

            // After the save. Approved and rejected are announced; suspension
            // is a conversation, not an automated email.
            await _notifications.PartnerStatusChangedAsync(vendor, ct);

            await _audit.RecordAsync(
                status switch
                {
                    VendorStatus.Approved => AuditAction.Approved,
                    VendorStatus.Rejected => AuditAction.Rejected,
                    VendorStatus.Suspended => AuditAction.Suspended,
                    _ => AuditAction.StatusChanged
                },
                nameof(Vendor), vendor.Id, $"{status} vendor {vendor.BusinessName}", ct);

            TempData["AdminMessage"] = $"{vendor.BusinessName} is now {status}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update status for vendor {VendorId}", id);
            TempData["AdminError"] = "Couldn't save that change. Please try again.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}