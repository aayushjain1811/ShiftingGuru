using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;

namespace ShiftingGuru.Areas.Admin.Controllers;

/// <summary>
/// NEW: every partner registration fee payment. Read-only: refunds are made
/// in the Razorpay dashboard. The important filter is "Paid, no application":
/// people who paid but never finished submitting, who may need a call or a refund.
/// </summary>
[Area("Admin")]
[Route("admin/registration-payments")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class RegistrationPaymentsController : Controller
{
    private const int PageSize = 50;

    private readonly ApplicationDbContext _db;

    public RegistrationPaymentsController(ApplicationDbContext db) => _db = db;

    // GET /admin/registration-payments?show=unused
    [HttpGet("")]
    public async Task<IActionResult> Index(string? show, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Registration fees";
        if (page < 1) page = 1;

        var query = _db.RegistrationPayments.AsNoTracking().Include(p => p.Vendor).AsQueryable();

        query = show switch
        {
            "paid" => query.Where(p => p.Status == RegistrationPaymentStatus.Paid),
            "unused" => query.Where(p => p.Status == RegistrationPaymentStatus.Paid && p.VendorId == null),
            "all" => query,
            _ => query.Where(p => p.Status != RegistrationPaymentStatus.Created)   // default: hide abandoned windows
        };

        ViewData["Show"] = show ?? "";
        ViewData["Page"] = page;
        ViewData["UnusedCount"] = await _db.RegistrationPayments.AsNoTracking()
            .CountAsync(p => p.Status == RegistrationPaymentStatus.Paid && p.VendorId == null, ct);

        var payments = await query
            .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize + 1)   // one extra tells us whether there's a next page
            .ToListAsync(ct);

        ViewData["HasNext"] = payments.Count > PageSize;

        return View(payments.Take(PageSize).ToList());
    }
}