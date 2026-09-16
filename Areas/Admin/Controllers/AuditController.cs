using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

/// <summary>
/// Read-only by design. An audit trail an admin can edit isn't an audit trail,
/// so there is no create, update or delete action here at all.
/// </summary>
[Area("Admin")]
[Route("admin/audit")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class AuditController : Controller
{
    private const int PageSize = 50;

    private readonly ApplicationDbContext _db;

    public AuditController(ApplicationDbContext db) => _db = db;

    // GET /admin/audit
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, AuditAction? action, string? entityType,
        int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Audit log";
        if (page < 1) page = 1;

        var query = _db.AdminAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(a =>
                EF.Functions.ILike(a.Description, term) ||
                EF.Functions.ILike(a.AdminEmail, term));
        }

        if (action.HasValue && Enum.IsDefined(action.Value))
        {
            query = query.Where(a => a.Action == action.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var entries = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        return View(new AdminAuditListViewModel
        {
            Entries = entries,
            Search = search,
            Action = action,
            EntityType = entityType,
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        });
    }
}