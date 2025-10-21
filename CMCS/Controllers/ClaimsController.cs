using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers
{
    [Authorize]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClaimsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ===== IC: My Claims =====
        [Authorize(Roles = "IC")]
        public async Task<IActionResult> My()
        {
            var uid = _userManager.GetUserId(User)!;
            var list = await _db.MonthlyClaims
                .Where(c => c.IcUserId == uid)
                .OrderByDescending(c => c.SubmittedAt)
                .ThenByDescending(c => c.Id)
                .ToListAsync();

            return View(list);
        }

       // ===== IC: Create (GET) =====
[Authorize(Roles = "IC")]
public IActionResult New()
{
    // Get current logged-in IC user
    var userId = _userManager.GetUserId(User)!;
    var user = _userManager.FindByIdAsync(userId).GetAwaiter().GetResult();

    // Build the view model with rate assigned by CO
    var vm = new MonthlyClaim
    {
        IcUserId = userId,
        MonthKey = $"{DateTime.UtcNow:yyyy-MM}",
        Hours = 0,
        Rate = user?.HourlyRate ?? 0m,   // read-only, pre-assigned by CO
        Status = ClaimStatus.Draft
    };

    return View("Create", vm);
}


// ===== IC: Create (POST Save Draft) =====
[Authorize(Roles = "IC")]
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SaveDraft(MonthlyClaim model, [FromForm] string? entriesJson)
{
    // Basic model validation for MonthKey etc.
    if (string.IsNullOrWhiteSpace(model.MonthKey) || !System.Text.RegularExpressions.Regex.IsMatch(model.MonthKey, @"^\d{4}-\d{2}$"))
        ModelState.AddModelError(nameof(model.MonthKey), "Month must be in YYYY-MM format.");

    // Parse entries and recompute total hours on server
    decimal totalHours = 0m;
    try
    {
        if (!string.IsNullOrWhiteSpace(entriesJson))
        {
            var entries = System.Text.Json.JsonSerializer.Deserialize<List<WorkEntryDto>>(entriesJson) ?? new();
            // Optional: validate entries belong to MonthKey
            var parts = model.MonthKey.Split('-');
            int year = int.Parse(parts[0]), month = int.Parse(parts[1]);

            foreach (var e in entries)
            {
                if (!DateOnly.TryParse(e.date, out var d))
                    continue;
                if (d.Year != year || d.Month != month)
                    continue;

                if (!TimeOnly.TryParse(e.start, out var s)) continue;
                if (!TimeOnly.TryParse(e.end, out var t)) continue;

                var span = t.ToTimeSpan() - s.ToTimeSpan();
                if (span.TotalMinutes > 0)
                    totalHours += (decimal)(span.TotalHours);
            }
        }
    }
    catch
    {
        ModelState.AddModelError(string.Empty, "Invalid time entries submitted.");
    }

    if (!ModelState.IsValid)
        return View("Create", model);

    // Enforce IC ownership and CO-assigned rate
    var userId = _userManager.GetUserId(User)!;
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null)
    {
        ModelState.AddModelError(string.Empty, "User not found.");
        return View("Create", model);
    }

    model.IcUserId = userId;
    model.Rate = user.HourlyRate;    // always enforce rate
    model.Hours = decimal.Round(totalHours, 2); // from server-side calculation
    model.Status = ClaimStatus.Draft;

    _db.MonthlyClaims.Add(model);
    await _db.SaveChangesAsync();

    TempData["OK"] = "Draft saved with time entries.";
    return RedirectToAction(nameof(My));
}

// DTO for deserializing entriesJson
private sealed class WorkEntryDto
{
    public string date { get; set; } = "";
    public string start { get; set; } = "";
    public string end { get; set; } = "";
}

        // ===== IC: Submit =====
        [Authorize(Roles = "IC")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id)
        {
            var uid = _userManager.GetUserId(User)!;
            var claim = await _db.MonthlyClaims
                .FirstOrDefaultAsync(c => c.Id == id && c.IcUserId == uid);

            if (claim == null)
                return NotFound();

            if (claim.Status != ClaimStatus.Draft)
            {
                TempData["Error"] = "Only draft claims can be submitted.";
                return RedirectToAction(nameof(My));
            }

            claim.Status = ClaimStatus.Pending;
            claim.SubmittedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["OK"] = "Claim submitted for review.";
            return RedirectToAction(nameof(My));
        }

        // ===== MR: Review Queue (Pending) =====
        [Authorize(Roles = "MR")]
        public async Task<IActionResult> ReviewQueue()
        {
            var list = await _db.MonthlyClaims
                .Include(c => c.IcUser)
                .Where(c => c.Status == ClaimStatus.Pending)
                .OrderBy(c => c.SubmittedAt)
                .ThenBy(c => c.Id)
                .ToListAsync();

            return View(list);
        }

        // ===== MR: Approve =====
        [Authorize(Roles = "MR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string? remark)
        {
            var claim = await _db.MonthlyClaims.FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null) return NotFound();
            if (claim.Status != ClaimStatus.Pending)
            {
                TempData["Error"] = "Only pending claims can be approved.";
                return RedirectToAction(nameof(ReviewQueue));
            }

            claim.Status = ClaimStatus.Approved;
            claim.ManagerRemark = string.IsNullOrWhiteSpace(remark) ? claim.ManagerRemark : remark.Trim();
            await _db.SaveChangesAsync();
            TempData["OK"] = "Claim approved.";
            return RedirectToAction(nameof(ReviewQueue));
        }

        // ===== MR: Reject =====
        [Authorize(Roles = "MR")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? remark)
        {
            var claim = await _db.MonthlyClaims.FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null) return NotFound();
            if (claim.Status != ClaimStatus.Pending)
            {
                TempData["Error"] = "Only pending claims can be rejected.";
                return RedirectToAction(nameof(ReviewQueue));
            }

            claim.Status = ClaimStatus.Rejected;
            claim.ManagerRemark = string.IsNullOrWhiteSpace(remark)
                ? "Rejected"
                : remark.Trim();

            await _db.SaveChangesAsync();
            TempData["OK"] = "Claim rejected.";
            return RedirectToAction(nameof(ReviewQueue));
        }

        // ===== CO: Overview =====
        [Authorize(Roles = "CO")]
        public async Task<IActionResult> All()
        {
            var list = await _db.MonthlyClaims
                .Include(c => c.IcUser)
                .OrderByDescending(c => c.SubmittedAt)
                .ThenByDescending(c => c.Id)
                .ToListAsync();

            return View(list);
        }
    }
}
