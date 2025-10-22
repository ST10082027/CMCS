using System.Text.Json;
using System.Text.RegularExpressions;
using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// NEW usings for file uploads
using Microsoft.AspNetCore.Http;
using System.IO;

namespace CMCS.Controllers
{
    [Authorize]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ClaimsController> _logger;
        private readonly IWebHostEnvironment _env; // NEW

        public ClaimsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<ClaimsController> logger,
            IWebHostEnvironment env) // NEW
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _env = env; // NEW
        }

        // ===== IC: My Claims =====
        [Authorize(Roles = "IC")]
        public async Task<IActionResult> My()
        {
            var uid = _userManager.GetUserId(User)!;
            var list = await _db.MonthlyClaims
                .Include(c => c.Documents) // NEW
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
            var userId = _userManager.GetUserId(User)!;
            var user = _userManager.FindByIdAsync(userId).GetAwaiter().GetResult();

            var vm = new MonthlyClaim
            {
                IcUserId = userId,
                MonthKey = $"{DateTime.UtcNow:yyyy-MM}",
                Hours = 0,
                Rate = user?.HourlyRate ?? 0m, // CO-assigned, read-only in UI
                Status = ClaimStatus.Draft
            };

            _logger.LogInformation("GET /Claims/New -> Create with MonthKey {MonthKey}, Rate {Rate}", vm.MonthKey, vm.Rate);
            return View("Create", vm);
        }

        // --- POST: /Claims/Summary ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Summary(MonthlyClaim model, [FromForm] string? entriesJson)
        {
            _logger.LogInformation("POST /Claims/Summary hit. Initial ModelState valid? {Valid}", ModelState.IsValid);

            // (1) Server-authoritative fields
            var userId = _userManager.GetUserId(User)!;
            model.IcUserId = userId;

            var user = _userManager.FindByIdAsync(userId).GetAwaiter().GetResult();
            if (user != null)
            {
                model.Rate = user.HourlyRate; // never trust posted value
            }

            // (2) Recompute Hours from entriesJson; cap to 1 decimal
            try
            {
                var hours = ComputeTotalHours(model.MonthKey ?? string.Empty, entriesJson);
                model.Hours = Math.Round(hours, 1, MidpointRounding.AwayFromZero);
                _logger.LogInformation("Recomputed Hours from entriesJson (rounded 1dp): {Hours}", model.Hours);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recompute Hours from entriesJson.");
                ModelState.AddModelError(nameof(model.Hours), "Could not compute total hours from entries.");
            }

            // (3) Validate MonthKey format
            if (!string.IsNullOrWhiteSpace(model.MonthKey) && !Regex.IsMatch(model.MonthKey, @"^\d{4}-\d{2}$"))
            {
                ModelState.AddModelError(nameof(model.MonthKey), "Month must be in YYYY-MM format.");
            }

            // (4) Remove client-bound errors for fields we override
            ModelState.Remove(nameof(model.IcUserId));
            ModelState.Remove(nameof(model.Hours));

            // (5) Re-validate normalized model
            if (!TryValidateModel(model))
            {
                var errors = ModelState.Where(kvp => kvp.Value != null && kvp.Value.Errors.Count > 0)
                    .ToDictionary(k => k.Key, v => v.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                _logger.LogWarning("ModelState invalid after normalization on POST /Claims/Summary -> {Errors}", JsonSerializer.Serialize(errors));
                return View("Create", model);
            }

            // (6) PRG via TempData
            var json = JsonSerializer.Serialize(model);
            TempData["ClaimSummaryJson"] = json;
            _logger.LogInformation("Stored TempData[ClaimSummaryJson] length {Len}. Redirecting to GET /Claims/Summary.", json.Length);

            return RedirectToAction(nameof(Summary)); // GET
        }

        // --- GET: /Claims/Summary ---
        [HttpGet]
        public IActionResult Summary()
        {
            _logger.LogInformation("GET /Claims/Summary hit.");

            if (TempData["ClaimSummaryJson"] is string json && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var model = JsonSerializer.Deserialize<MonthlyClaim>(json);
                    if (model == null)
                    {
                        _logger.LogWarning("Failed to deserialize ClaimSummaryJson (null). Redirecting to New.");
                        return RedirectToAction(nameof(New));
                    }

                    _logger.LogInformation("Deserialized model successfully. Rendering Summary view.");
                    return View("Summary", model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing ClaimSummaryJson. Redirecting to New.");
                    return RedirectToAction(nameof(New));
                }
            }

            _logger.LogWarning("No TempData for ClaimSummaryJson (refresh or deep link). Redirecting to New.");
            return RedirectToAction(nameof(New));
        }

        // --- POST: /Claims/Finish ---
        // Persists the claim and redirects to /Claims/My
        // NEW: supports optional file uploads (SupportingFiles)
        [Authorize(Roles = "IC")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finish([FromForm] string summaryJson, List<IFormFile>? SupportingFiles) // CHANGED
        {
            _logger.LogInformation("POST /Claims/Finish hit. summaryJson length: {Len}", summaryJson?.Length ?? 0);

            if (string.IsNullOrWhiteSpace(summaryJson))
            {
                _logger.LogWarning("summaryJson missing. Redirecting to New.");
                TempData["warn"] = "Session expired. Please recreate your claim.";
                return RedirectToAction(nameof(New));
            }

            MonthlyClaim? incoming;
            try
            {
                incoming = JsonSerializer.Deserialize<MonthlyClaim>(summaryJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize summaryJson in Finish.");
                TempData["warn"] = "Could not read your claim details. Please try again.";
                return RedirectToAction(nameof(New));
            }

            if (incoming == null)
            {
                TempData["warn"] = "Claim details were empty. Please try again.";
                return RedirectToAction(nameof(New));
            }

            var userId = _userManager.GetUserId(User)!;
            var user = await _userManager.FindByIdAsync(userId);

            // Check if a claim for this month already exists (single submission per month)
            var monthKey = incoming.MonthKey;
            if (!string.IsNullOrWhiteSpace(monthKey))
            {
                var exists = await _db.MonthlyClaims
                    .AnyAsync(c => c.IcUserId == userId && c.MonthKey == monthKey);

                if (exists)
                {
                    _logger.LogInformation("Duplicate month submission blocked. User {UserId} MonthKey {MonthKey}", userId, monthKey);
                    TempData["warn"] = $"You have already submitted this month's claim ({monthKey}).";
                    return RedirectToAction(nameof(My));
                }
            }

            // Defensive: cap hours to 1 dp again
            var hoursRounded = Math.Round(incoming.Hours, 1, MidpointRounding.AwayFromZero);

            var claim = new MonthlyClaim
            {
                IcUserId = userId,
                MonthKey = incoming.MonthKey,
                Hours = hoursRounded,
                Rate = user?.HourlyRate ?? incoming.Rate,
                Notes = incoming.Notes,
                Status = ClaimStatus.Pending,
                SubmittedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _db.MonthlyClaims.Add(claim);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Claim {Id} persisted for user {UserId} with Hours={Hours}, Rate={Rate}, MonthKey={MonthKey}",
                claim.Id, userId, claim.Hours, claim.Rate, claim.MonthKey);

            // === Handle optional supporting files ===
            if (SupportingFiles is { Count: > 0 })
            {
                var claimId = claim.Id;
                var uploadsRoot = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "claims", claimId.ToString());
                Directory.CreateDirectory(uploadsRoot);

                // Basic allow-list (extend as needed)
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "application/pdf",
                    "image/png", "image/jpeg",
                    "application/msword",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "application/vnd.ms-excel",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "text/plain", "text/csv"
                };
                const long maxBytes = 20L * 1024 * 1024; // 20 MB per file

                foreach (var file in SupportingFiles)
                {
                    if (file == null || file.Length == 0) continue;
                    if (file.Length > maxBytes)
                    {
                        _logger.LogWarning("Skipped {File} > 20MB", file.FileName);
                        continue;
                    }
                    if (!string.IsNullOrEmpty(file.ContentType) && !allowed.Contains(file.ContentType))
                    {
                        _logger.LogWarning("Skipped {File} unsupported content-type {CT}", file.FileName, file.ContentType);
                        continue;
                    }

                    var ext = Path.GetExtension(file.FileName);
                    var stored = $"{Guid.NewGuid():N}{ext}";
                    var fullPath = Path.Combine(uploadsRoot, stored);
                    await using (var stream = System.IO.File.Create(fullPath))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var doc = new Document
                    {
                        Id = Guid.NewGuid(),
                        MonthlyClaimId = claimId,
                        FileName = file.FileName,
                        ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                        FileSize = (int)file.Length,
                        StoragePath = Path.Combine("uploads", "claims", claimId.ToString(), stored),
                        UploadedAt = DateTime.UtcNow
                    };
                    _db.Documents.Add(doc);
                }
                await _db.SaveChangesAsync();
            }

            TempData["ok"] = "Your claim has been submitted for review.";
            return RedirectToAction(nameof(My));
        }

        // ===== IC: Confirm Submit (legacy) =====
        [Authorize(Roles = "IC")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSubmit(int id)
        {
            var claim = await _db.MonthlyClaims.FindAsync(id);
            if (claim == null)
                return NotFound();

            // Finalize the claim
            claim.Status = ClaimStatus.Pending;
            claim.SubmittedAt = DateTime.UtcNow;

            _db.Update(claim);
            await _db.SaveChangesAsync();

            TempData["ok"] = "Your claim has been submitted successfully.";
            return RedirectToAction(nameof(My));
        }

        // ===== IC: Review (GET) =====
        [Authorize(Roles = "IC")]
        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var uid = _userManager.GetUserId(User)!;

            var claim = await _db.MonthlyClaims
                .Include(c => c.IcUser)
                .FirstOrDefaultAsync(c => c.Id == id && c.IcUserId == uid);

            if (claim == null)
                return NotFound();

            return View("Review", claim);
        }

        // ===== IC: Submit (POST) =====
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

            if (claim.Status != ClaimStatus.Draft && claim.Status != ClaimStatus.Rejected)
            {
                TempData["warn"] = "Only Draft or Rejected claims can be submitted.";
                return RedirectToAction(nameof(Review), new { id });
            }

            claim.Status = ClaimStatus.Pending;
            claim.SubmittedAt = DateTime.UtcNow;
            claim.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["ok"] = "Your claim was submitted for manager review.";
            return RedirectToAction(nameof(My));
        }

        // ===== MR: Review Queue (Pending) =====
        [Authorize(Roles = "MR")]
        public async Task<IActionResult> ReviewQueue()
        {
            var list = await _db.MonthlyClaims
                .Include(c => c.IcUser)
                .Include(c => c.Documents) // NEW
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
            if (!string.IsNullOrWhiteSpace(remark))
                claim.ManagerRemark = remark.Trim();

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
            claim.ManagerRemark = string.IsNullOrWhiteSpace(remark) ? "Rejected" : remark.Trim();

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
                .Include(c => c.Documents) // NEW
                .OrderByDescending(c => c.SubmittedAt)
                .ThenByDescending(c => c.Id)
                .ToListAsync();

            return View(list);
        }

        // ===== Downloads =====
        // IC, MR, and CO can download stored documents
        [Authorize(Roles = "IC,MR,CO")]
        [HttpGet("/Claims/DownloadDocument/{id:guid}")]
        public async Task<IActionResult> DownloadDocument(Guid id)
        {
            var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();

            var fullPath = Path.Combine(_env.WebRootPath ?? "wwwroot",
                doc.StoragePath.Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            var stream = System.IO.File.OpenRead(fullPath);
            return File(stream, doc.ContentType, doc.FileName);
        }

        // ===== Helpers =====
        private static decimal ComputeTotalHours(string monthKey, string? entriesJson)
        {
            if (string.IsNullOrWhiteSpace(entriesJson) || string.IsNullOrWhiteSpace(monthKey))
                return 0m;

            var entries = JsonSerializer.Deserialize<List<WorkEntryDto>>(entriesJson) ?? new();
            var parts = monthKey.Split('-');
            if (parts.Length != 2) return 0m;

            if (!int.TryParse(parts[0], out var year)) return 0m;
            if (!int.TryParse(parts[1], out var month)) return 0m;

            decimal totalHours = 0m;

            foreach (var e in entries)
            {
                if (!DateOnly.TryParse(e.date, out var d)) continue;
                if (d.Year != year || d.Month != month) continue;

                if (!TimeOnly.TryParse(e.start, out var s)) continue;
                if (!TimeOnly.TryParse(e.end, out var t)) continue;

                var span = t.ToTimeSpan() - s.ToTimeSpan();
                if (span.TotalMinutes > 0)
                    totalHours += (decimal)span.TotalHours;
            }

            // cap to 1 dp
            return Math.Round(totalHours, 1, MidpointRounding.AwayFromZero);
        }

        private sealed class WorkEntryDto
        {
            public string date { get; set; } = "";
            public string start { get; set; } = "";
            public string end { get; set; } = "";
        }
    }
}
