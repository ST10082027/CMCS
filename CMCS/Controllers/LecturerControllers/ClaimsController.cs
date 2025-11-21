using System.Text.Json;
using System.Text.RegularExpressions;
using CMCS.Data;
using CMCS.Infrastructure;
using CMCS.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace CMCS.Controllers.LecturerControllers
{
    // Require login for everything in this controller; per-action roles below
    [SessionAuthorize]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ClaimsController> _logger;
        private readonly IWebHostEnvironment _env;

        public ClaimsController(ApplicationDbContext db,
                                ILogger<ClaimsController> logger,
                                IWebHostEnvironment env)
        {
            _db = db;
            _logger = logger;
            _env = env;
        }


        // ===== Lecturer: My Claims =====
        [SessionAuthorize("Lecturer")]
        [HttpGet]
        public async Task<IActionResult> My()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var list = await _db.MonthlyClaims
                .Include(c => c.Documents)
                .Where(c => c.IcUserId == userId)
                .OrderByDescending(c => c.SubmittedAt)
                .ThenByDescending(c => c.Id)
                .ToListAsync();

            _logger.LogInformation("Lecturer {UserId} viewed My Claims list with {Count} records", userId, list.Count);

            // FIXED PATH
            return View("~/Views/LecturerViews/MyClaims.cshtml", list);
        }

        // ===== Lecturer: New Claim (GET) =====
        [SessionAuthorize("Lecturer")]
        [HttpGet]
        public IActionResult New()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            var vm = new MonthlyClaim
            {
                IcUserId = userId,
                MonthKey = $"{DateTime.UtcNow:yyyy-MM}",
                Hours = 0,
                Rate = user.HourlyRate, // HR-assigned, read-only in UI
                Status = ClaimStatus.Draft
            };

            _logger.LogInformation("GET /Claims/New -> Create with MonthKey {MonthKey}, Rate {Rate}", vm.MonthKey, vm.Rate);

            // FIXED PATH
            return View("~/Views/LecturerViews/CreateClaim.cshtml", vm);
        }

        // ===== Lecturer: New Claim (POST from Create) - go to Summary =====
        [SessionAuthorize("Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Summary(MonthlyClaim model, string entriesJson)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            // Load authoritative user & rate
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                model.Rate = user.HourlyRate; // server-authoritative
            }

            // Recompute Hours from entriesJson; cap to 1 decimal
            try
            {
                var hours = ComputeTotalHours(model.MonthKey ?? string.Empty, entriesJson);
                model.Hours = Math.Round(hours, 1, MidpointRounding.AwayFromZero);
                _logger.LogInformation("Recomputed Hours from entriesJson (rounded 1dp): {Hours}", model.Hours);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recompute Hours from entriesJson in Summary.");
                ModelState.AddModelError(string.Empty, "We could not read your time entries. Please try again.");
            }

            // Validate MonthKey format
            if (!string.IsNullOrWhiteSpace(model.MonthKey) &&
                !Regex.IsMatch(model.MonthKey, @"^\d{4}-\d{2}$"))
            {
                ModelState.AddModelError(nameof(model.MonthKey), "Month must be in YYYY-MM format.");
            }

            // Remove client-bound errors for overridden fields
            ModelState.Remove(nameof(model.IcUserId));
            ModelState.Remove(nameof(model.Hours));
            ModelState.Remove(nameof(model.Rate));

            model.IcUserId = userId;
            model.Status = ClaimStatus.Draft;

            if (!TryValidateModel(model))
            {
                _logger.LogWarning("Summary model validation failed. Returning to Create view.");
                // FIXED PATH
                return View("~/Views/LecturerViews/CreateClaim.cshtml", model);
            }

            // Pass entriesJson forward via ViewData so the Summary view can post it again
            ViewData["EntriesJson"] = entriesJson ?? "[]";

            // FIXED PATH
            return View("~/Views/LecturerViews/ClaimSummary.cshtml", model);
        }

        // ===== Lecturer: Final submit from Summary (POST /Claims/Finish) =====
        [SessionAuthorize("Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finish(string summaryJson, List<IFormFile> SupportingFiles)
        {
            var userId = HttpContext.Session.GetString("UserId");
            _logger.LogInformation(
        "Finish called for user {UserId} with summaryJson length {Len}",
        userId, summaryJson?.Length ?? 0);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(summaryJson))
            {
                TempData["error"] = "We could not read your claim summary. Please try again.";
                return RedirectToAction("New");
            }

            MonthlyClaim incoming;
            try
            {
                incoming = JsonSerializer.Deserialize<MonthlyClaim>(summaryJson) ?? new MonthlyClaim();
            }
            catch
            {
                TempData["error"] = "We could not read your claim summary. Please try again.";
                return RedirectToAction("New");
            }

            // Reset ID to ensure new claim
            incoming.Id = 0;
            incoming.IcUserId = userId;

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            incoming.Rate = user?.HourlyRate ?? 0;

            // Duplicate check for same month
            if (!string.IsNullOrWhiteSpace(incoming.MonthKey))
            {
                var duplicate = await _db.MonthlyClaims.AnyAsync(c =>
                    c.IcUserId == userId &&
                    c.MonthKey == incoming.MonthKey &&
                    (c.Status == ClaimStatus.Pending ||
                     c.Status == ClaimStatus.VerifiedByCoordinator ||
                     c.Status == ClaimStatus.ApprovedByManager ||
                     c.Status == ClaimStatus.FinalisedByHR));

                if (duplicate)
                {
                    TempData["error"] = "You already have a claim submitted for this month.";
                    return RedirectToAction(nameof(My));
                }
            }

            // Set pipeline tracking fields
            incoming.Status = ClaimStatus.Pending;
            incoming.SubmittedAt = DateTime.UtcNow;
            incoming.CreatedAt = DateTime.UtcNow;
            incoming.UpdatedAt = DateTime.UtcNow;

            if (!TryValidateModel(incoming))
            {
                TempData["error"] = "There was a problem with your claim. Please review it.";
                return RedirectToAction("New");
            }

            _db.MonthlyClaims.Add(incoming);
            await _db.SaveChangesAsync();

            // ========= Handle Attachments =========
            if (SupportingFiles != null && SupportingFiles.Count > 0)
            {
                var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt"
        };

                const long maxFileSize = 20 * 1024 * 1024; // 20MB

                var uploadPath = Path.Combine(_env.WebRootPath, "uploads", "claims", incoming.Id.ToString());
                Directory.CreateDirectory(uploadPath);

                foreach (var file in SupportingFiles)
                {
                    if (file.Length == 0 || file.Length > maxFileSize)
                        continue;

                    var ext = Path.GetExtension(file.FileName);
                    if (!allowedExts.Contains(ext))
                        continue;

                    var storedName = $"{Guid.NewGuid():N}{ext}";
                    var storedPath = Path.Combine(uploadPath, storedName);

                    using (var stream = System.IO.File.Create(storedPath))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var doc = new Document
                    {
                        Id = Guid.NewGuid(),
                        MonthlyClaimId = incoming.Id,
                        FileName = Path.GetFileName(file.FileName),
                        ContentType = file.ContentType,
                        FileSize = (int)file.Length,
                        StoragePath = $"uploads/claims/{incoming.Id}/{storedName}",
                        UploadedAt = DateTime.UtcNow,
                        UploadedByUserId = userId
                    };

                    _db.Documents.Add(doc);
                }

                await _db.SaveChangesAsync();
            }

            TempData["ok"] = "Your claim has been submitted for review.";
            return RedirectToAction("Index", "LecturerDashboard");
        }

        // ===== Lecturer: Review Draft Claim (GET) =====
        [SessionAuthorize("Lecturer")]
        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var claim = await _db.MonthlyClaims
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id && c.IcUserId == userId);

            if (claim == null)
            {
                TempData["warn"] = "Claim not found or you do not have permission to view it.";
                return RedirectToAction(nameof(My));
            }

            // FIXED PATH
            return View("~/Views/LecturerViews/ReviewClaim.cshtml", claim);
        }

        // ===== Lecturer: Confirm & Submit existing draft claim (POST) =====
        [SessionAuthorize("Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSubmit(int id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            var claim = await _db.MonthlyClaims
                .FirstOrDefaultAsync(c => c.Id == id && c.IcUserId == userId);

            if (claim == null)
            {
                return NotFound();
            }

            // Only Draft or Rejected can be (re)submitted
            if (claim.Status != ClaimStatus.Draft &&
                claim.Status != ClaimStatus.Rejected)
            {
                TempData["warn"] = "Only draft or rejected claims can be submitted.";
                return RedirectToAction(nameof(Review), new { id });
            }

            // Make sure we are using the HR-assigned rate
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                claim.Rate = user.HourlyRate;
            }

            claim.Status = ClaimStatus.Pending;
            claim.SubmittedAt = DateTime.UtcNow;
            claim.UpdatedAt = DateTime.UtcNow;

            if (!TryValidateModel(claim))
            {
                TempData["error"] = "There was a problem with your claim. Please review it.";
                return RedirectToAction(nameof(Review), new { id });
            }

            await _db.SaveChangesAsync();

            TempData["ok"] = "Your claim has been submitted for review.";
            return RedirectToAction(nameof(My));
        }

        // ===== Lecturer: Edit existing draft/rejected claim (GET) =====
        [SessionAuthorize("Lecturer")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            var claim = await _db.MonthlyClaims
                .FirstOrDefaultAsync(c => c.Id == id && c.IcUserId == userId);

            if (claim == null)
            {
                return NotFound();
            }

            // Only allow editing draft or rejected claims
            if (claim.Status != ClaimStatus.Draft &&
                claim.Status != ClaimStatus.Rejected)
            {
                TempData["warn"] = "Only draft or rejected claims can be edited.";
                return RedirectToAction(nameof(Review), new { id });
            }

            // Ensure MonthKey is set so the month wheel has something to show
            if (string.IsNullOrWhiteSpace(claim.MonthKey))
            {
                claim.MonthKey = DateTime.UtcNow.ToString("yyyy-MM");
            }

            return View("~/Views/LecturerViews/EditClaim.cshtml", claim);
        }


        // ===== Lecturer: Edit existing draft/rejected claim (POST) =====
        [SessionAuthorize("Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MonthlyClaim model, string entriesJson)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            // Load the persisted claim for this user
            var claim = await _db.MonthlyClaims
                .FirstOrDefaultAsync(c => c.Id == model.Id && c.IcUserId == userId);

            if (claim == null)
            {
                return NotFound();
            }

            // Only allow editing draft or rejected claims
            if (claim.Status != ClaimStatus.Draft &&
                claim.Status != ClaimStatus.Rejected)
            {
                TempData["warn"] = "Only draft or rejected claims can be edited.";
                return RedirectToAction(nameof(Review), new { id = claim.Id });
            }

            // Load authoritative user & rate
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                model.Rate = user.HourlyRate; // server-authoritative
            }

            // Recompute Hours from entriesJson; cap to 1 decimal
            if (!string.IsNullOrWhiteSpace(entriesJson))
            {
                try
                {
                    var hours = ComputeTotalHours(model.MonthKey ?? string.Empty, entriesJson);
                    model.Hours = Math.Round(hours, 1, MidpointRounding.AwayFromZero);
                    _logger.LogInformation(
                        "Recomputed Hours from entriesJson in Edit for claim {Id}: {Hours}",
                        claim.Id, model.Hours);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to recompute Hours from entriesJson in Edit for claim {Id}. Raw entriesJson: {Json}",
                        claim.Id, entriesJson);
                    ModelState.AddModelError(string.Empty,
                        "We could not read your time entries. Please try again.");
                }
            }

            // Validate MonthKey format (YYYY-MM)
            if (!string.IsNullOrWhiteSpace(model.MonthKey) &&
                !Regex.IsMatch(model.MonthKey, @"^\d{4}-\d{2}$"))
            {
                ModelState.AddModelError(nameof(model.MonthKey), "Month must be in YYYY-MM format.");
            }

            // Remove client-bound errors for overridden fields
            ModelState.Remove(nameof(model.IcUserId));
            ModelState.Remove(nameof(model.Hours));
            ModelState.Remove(nameof(model.Rate));

            // Preserve ownership and status from the existing claim
            model.IcUserId = userId;
            model.Status = claim.Status; // keep Draft/Rejected as-is

            // Validation over the updated model state
            if (!TryValidateModel(model))
            {
                _logger.LogWarning("Edit model validation failed. Returning to Edit view.");
                return View("~/Views/LecturerViews/EditClaim.cshtml", model);
            }

            // Copy validated fields back onto the tracked entity
            claim.MonthKey = model.MonthKey ?? claim.MonthKey;
            claim.Hours = model.Hours;
            claim.Rate = model.Rate;
            claim.Notes = model.Notes;
            claim.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["ok"] = "Your draft claim has been updated.";
            return RedirectToAction(nameof(Review), new { id = claim.Id });
        }


        // ===== Utility: Compute total hours from entriesJson =====
        private decimal ComputeTotalHours(string monthKey, string entriesJson)
        {
            if (string.IsNullOrWhiteSpace(entriesJson))
            {
                return 0m;
            }

            List<WorkEntryDto>? entries;
            try
            {
                entries = JsonSerializer.Deserialize<List<WorkEntryDto>>(entriesJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize entriesJson in ComputeTotalHours. Raw: {Raw}", entriesJson);
                throw;
            }

            if (entries == null || entries.Count == 0)
            {
                return 0m;
            }

            decimal totalHours = 0m;

            foreach (var e in entries)
            {
                if (!DateTime.TryParse(e.date, out var date))
                {
                    _logger.LogWarning("Skipping work entry with invalid date: {Date}", e.date);
                    continue;
                }

                if (!TimeSpan.TryParse(e.start, out var start) ||
                    !TimeSpan.TryParse(e.end, out var end))
                {
                    _logger.LogWarning("Skipping work entry with invalid time: {Start} - {End}", e.start, e.end);
                    continue;
                }

                if (end <= start)
                {
                    _logger.LogWarning("Skipping work entry with non-positive duration: {Start} - {End}", e.start, e.end);
                    continue;
                }

                var duration = end - start;
                var hours = (decimal)duration.TotalHours;

                if (hours < 0 || hours > 24)
                {
                    _logger.LogWarning("Skipping work entry with unreasonable duration: {Hours} hours", hours);
                    continue;
                }

                totalHours += hours;
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