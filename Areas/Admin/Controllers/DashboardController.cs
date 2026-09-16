using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db) => _db = db;

    // GET /admin
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // One grouped query for every status count, rather than eight separate
        // CountAsync calls. PostgreSQL does the aggregation.
        var grouped = await _db.Leads
            .AsNoTracking()
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var counts = grouped.ToDictionary(x => x.Status, x => x.Count);

        var recent = await _db.Leads
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(8)
            .ToListAsync(ct);

        // Two EXISTS-based counts, computed by PostgreSQL.
        var assigned = await _db.Leads
            .AsNoTracking()
            .CountAsync(l => l.Assignments.Any(a => a.Status != AssignmentStatus.Cancelled), ct);

        var quoteCounts = await _db.Quotes
            .AsNoTracking()
            .GroupBy(q => q.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        return View(new AdminDashboardViewModel
        {
            AssignedLeads = assigned,
            QuoteCounts = quoteCounts,
            // Summing the grouped result avoids a second COUNT(*) round trip.
            TotalLeads = counts.Values.Sum(),
            CountsByStatus = counts,
            RecentLeads = recent
        });
    }
}