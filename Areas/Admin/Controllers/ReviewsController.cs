using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Review;

namespace ShiftingGuru.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin/reviews")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class ReviewsController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _db;
    private readonly IReviewService _reviews;
    private readonly IAuditService _audit;

    public ReviewsController(ApplicationDbContext db, IReviewService reviews, IAuditService audit)
    {
        _db = db;
        _reviews = reviews;
        _audit = audit;
    }

    // GET /admin/reviews
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, ReviewStatus? status, int? rating, int? vendorId,
        DateOnly? from, DateOnly? to, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Reviews";
        if (page < 1) page = 1;

        var query = _db.Reviews
            .AsNoTracking()
            .Include(r => r.Vendor)
            .Include(r => r.Lead)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(r =>
                (r.Title != null && EF.Functions.ILike(r.Title, term)) ||
                EF.Functions.ILike(r.Comment, term) ||
                EF.Functions.ILike(r.Vendor!.BusinessName, term) ||
                EF.Functions.ILike(r.Lead!.LeadNumber, term));
        }

        if (status.HasValue && Enum.IsDefined(status.Value))
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (rating is >= 1 and <= 5)
        {
            query = query.Where(r => r.Rating == rating.Value);
        }
        else
        {
            rating = null;
        }

        if (vendorId.HasValue) query = query.Where(r => r.VendorId == vendorId.Value);

        if (from.HasValue)
        {
            var fromUtc = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt < toUtc);
        }

        var total = await query.CountAsync(ct);

        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        var pending = await _db.Reviews
            .AsNoTracking()
            .CountAsync(r => r.Status == ReviewStatus.Pending, ct);

        var vendors = await _db.Vendors
            .AsNoTracking()
            .Where(v => v.Reviews.Any())
            .OrderBy(v => v.BusinessName)
            .Select(v => new { v.Id, v.BusinessName })
            .ToListAsync(ct);

        return View(new AdminReviewListViewModel
        {
            Reviews = reviews,
            Search = search,
            Status = status,
            Rating = rating,
            VendorId = vendorId,
            FromDate = from,
            ToDate = to,
            Page = page,
            PageSize = PageSize,
            TotalCount = total,
            PendingCount = pending,
            Vendors = vendors.Select(v => (v.Id, v.BusinessName)).ToList()
        });
    }

    // GET /admin/reviews/42
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var review = await _db.Reviews
            .AsNoTracking()
            .Include(r => r.Vendor)
            .Include(r => r.Lead)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (review?.Lead is null || review.Vendor is null) return View("NotFound");

        ViewData["Title"] = $"Review of {review.Vendor.BusinessName}";

        return View(new AdminReviewDetailsViewModel
        {
            Review = review,
            Lead = review.Lead,
            Vendor = review.Vendor,
            AllowedTransitions = _reviews.AllowedTransitionsFrom(review.Status)
        });
    }

    // POST /admin/reviews/42/moderate
    [HttpPost("{id:int}/moderate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Moderate(
        int id, ReviewStatus status, string? note, CancellationToken ct)
    {
        var changed = await _reviews.ModerateAsync(id, status, note, ct);

        TempData[changed ? "AdminMessage" : "AdminError"] = changed
            ? $"Review marked {status}."
            : "That moderation step isn't allowed from the review's current state.";

        if (changed)
        {
            await _audit.RecordAsync(
                status == ReviewStatus.Approved ? AuditAction.Approved : AuditAction.StatusChanged,
                nameof(Review), id, $"Review moderated to {status}", ct);
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}