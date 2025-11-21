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

namespace CMCS.Controllers
{
    // ============================
    // Controller: ClaimsController
    // Requires session-based authentication
    // ============================
    [SessionAuthorize]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ClaimsController> _logger;
        private readonly IWebHostEnvironment _env;

        // -------------------------------------
        // Constructor / DI setup
        // -------------------------------------
        public ClaimsController(
            ApplicationDbContext db,
            ILogger<ClaimsController> logger,
            IWebHostEnvironment env)
        {
            _db = db;
            _logger = logger;
            _env = env;
        }

        // =====================================================================
        // LECTURER ACTIONS
        // =====================================================================

        // -----------------------------------------------------------
        // Lecturer: View all their own claims
        // GET /Claims/My
        // -----------------------------------------------------------
        [SessionAuthorize("Lecturer")]
        [HttpGet]
        public async Task<IActionResult> My()
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var list = await _db.MonthlyClaims
                .Include(c => c.Documents)
                .Where(c => c.IcUserId == userId)
                .OrderByDescending(c => c.SubmittedAt)
                .ThenByDescending(c => c.Id)
                .ToListAsync();

            _logger.LogInformation(
                "Lecturer {UserId} viewed My Claims list with {Count} records",
                userId, list.Count);

            return View("~/Views/LecturerViews/MyClaims.cshtml", list);
        }

        // -----------------------------------------------------------
        // Lecturer: Begin a new claim (Form)
        // GET /Claims/New
        // -----------------------------------------------------------
        [SessionAuthorize("Lecturer")]
        [HttpGet]
        public IActionResult New()
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var user = _db.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            // Prefill defaults
            var vm = new MonthlyClaim
            {
                IcUserId = userId,
                MonthKey = $"{DateTime.UtcNow:yyyy-MM}",
                Hours = 0,
                Rate = user.HourlyRate,
                Status = ClaimStatus.Draft
            };

            _logger.LogInformation("GET /Claims/New -> MonthKey {MonthKey}, Rate {Rate}",
                vm.MonthKey, vm.Rate);

            return View("~/Views/LecturerViews/CreateClaim.cshtml", vm);
        }

        // -----------------------------------------------------------
        // Lecturer: Submit Create view -> Summary page
        // POST /Claims/Summary
        // -----------------------------------------------------------
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

            // Always load authoritative HR rate
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
                model.Rate = user.HourlyRate;

            // Compute total hours from JSON entries
            try
            {
                var totalHours = ComputeTotalHours(model.MonthKey ?? string.Empty, entriesJson);
                model.Hours = Math.Round(totalHours, 1, MidpointRounding.AwayFromZero);

                _logger.LogInformation(
                    "Recomputed hours from entriesJson: {Hours}",
                    model.Hours);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compute hours from entriesJson");
                ModelState.AddModelError(string.Empty, "Unable to read your time entries. Please try again.");
            }

            // Validate MonthKey format
            if (!string.IsNullOrWhiteSpace(model.MonthKey) &&
                !Regex.IsMatch(model.MonthKey, @"^\d{4}-\d{2}$"))
            {
                ModelState.AddModelError(nameof(model.MonthKey),
                    "Month must be in YYYY-MM format.");
            }

            // Remove client-bound fields (server authoritative)
            ModelState.Remove(nameof(model.IcUserId));
            ModelState.Remove(nameof(model.Hours));
            ModelState.Remove(nameof(model.Rate));

            // Assign validated server values
            model.IcUserId = userId;
            model.Status = ClaimStatus.Draft;

            if (!TryValidateModel(model))
            {
                _logger.LogWarning("Summary validation failed - returning to Create view");
                return View("~/Views/LecturerViews/CreateClaim.cshtml", model);
            }

            // Pass entriesJson forward to the Summary View
            ViewData["EntriesJson"] = entriesJson ?? "[]";

            return View("~/Views/LecturerViews/ClaimSummary.cshtml", model);
        }

        // -----------------------------------------------------------
        // Lecturer: Final Submit from Summary
        // POST /Claims/Finish
        // -----------------------------------------------------------
        [SessionAuthorize("Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finish(string summaryJson, List<IFormFile> SupportingFiles)
        {
            var userId = HttpContext.Session.GetString("UserId");

            _logger.LogInformation("Finish called for {UserId}, json length {Len}",
                userId, summaryJson?.Length ?? 0);

            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(summaryJson))
            {
                TempData["error"] = "We could not read your claim summary.";
                return RedirectToAction("New");
            }

            MonthlyClaim incoming;
            try
            {
                incoming = JsonSerializer.Deserialize<MonthlyClaim>(summaryJson)
                           ?? new MonthlyClaim();
            }
            catch
            {
                TempData["error"] = "We could not read your claim summary.";
                return RedirectToAction("New");
            }

            // Ensure new claim
            incoming.Id = 0;
            incoming.IcUserId = userId;

            // Server authoritative HR rate
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            incoming.Rate = user?.HourlyRate ?? 0;

            // Prevent duplicate month submissions
            if (!string.IsNullOrWhiteSpace(incoming.MonthKey))
            {
                bool duplicate = await _db.MonthlyClaims.AnyAsync(c =>
                    c.IcUserId == userId &&
                    c.MonthKey == incoming.MonthKey &&
                    (c.Status == ClaimStatus.Pending ||
                     c.Status == ClaimStatus.VerifiedByCoordinator ||
                     c.Status == ClaimStatus.ApprovedByManager ||
                     c.Status == ClaimStatus.FinalisedByHR));

                if (duplicate)
                {
                    TempData["error"] = "You already submitted a claim for this month.";
                    return RedirectToAction(nameof(My));
                }
            }

            // Set pipeline metadata
            incoming.Status = ClaimStatus.Pending;
            incoming.SubmittedAt = DateTime.UtcNow;
            incoming.CreatedAt = DateTime.UtcNow;
            incoming.UpdatedAt = DateTime.UtcNow;

            // Validate final object
            if (!TryValidateModel(incoming))
            {
                TempData["error"] = "There was a problem with your claim.";
                return RedirectToAction("New");
            }

            _db.MonthlyClaims.Add(incoming);
            await _db.SaveChangesAsync();

            // -------------------------------
            // Handle Supporting Documents
            // -------------------------------
            if (SupportingFiles != null && SupportingFiles.Count > 0)
            {
                var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx",
                    ".xls", ".xlsx", ".csv", ".txt"
                };

                const long maxFileSize = 20 * 1024 * 1024;
                var uploadPath = Path.Combine(_env.WebRootPath, "uploads", "claims", incoming.Id.ToString());

                Directory.CreateDirectory(uploadPath);

                foreach (var file in SupportingFiles)
                {
                    if (file.Length == 0 || file.Length > maxFileSize)
                        continue;

                    string ext = Path.GetExtension(file.FileName);
                    if (!allowedExts.Contains(ext))
                        continue;

                    string storedName = $"{Guid.NewGuid():N}{ext}";
                    string storedPath = Path.Combine(uploadPath, storedName);

                    using (var stream = System.IO.File.Create(storedPath))
                        await file.CopyToAsync(stream);

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

            TempData["ok"] = "Your claim has been submitted.";
            return RedirectToAction("Index", "LecturerDashboard");
        }

        // -----------------------------------------------------------
        // Lecturer: Review existing draft / rejected claim
        // GET /Claims/Review/{id}
        // -----------------------------------------------------------
        [SessionAuthorize("Lecturer")]
        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var claim = await _db.MonthlyClaims
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id && c.IcUserId == userId);

            if (claim == null)
            {
                TempData["warn"] = "Claim not found.";
                return RedirectToAction(nameof(My));
            }

            return View("~/Views/LecturerViews/ReviewClaim.cshtml", claim);
        }

        // -----------------------------------------------------------
        // Lecturer: Confirm submission of draft claim
        // POST /Claims/ConfirmSubmit
        // -----------------------------------------------------------
        [SessionAuthorize("Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSubmit(int id)
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired.";
                return RedirectToAction("Login", "Account");
            }

            var claim = await _db.MonthlyClaims
                .FirstOrDefaultAsync(c => c.Id == id && c.IcUserId == userId);

            if (claim == null)
                return NotFound();

            // Only draft or rejected can be resubmitted
            if (claim.Status != ClaimStatus.Draft &&
                claim.Status != ClaimStatus.Rejected)
            {
                TempData["warn"] = "You cannot submit this claim.";
                return RedirectToAction(nameof(Review), new { id });
            }

            // Server authoritative HR rate
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
                claim.Rate = user.HourlyRate;

            claim.Status = ClaimStatus.Pending;
            claim.SubmittedAt = DateTime.UtcNow;
            claim.UpdatedAt = DateTime.UtcNow;

            if (!TryValidateModel(claim))
            {
                TempData["error"] = "There was a problem.";
                return RedirectToAction(nameof(Review), new { id });
            }

            await _db.SaveChangesAsync();

            TempData["ok"] = "Your claim has been submitted.";
            return RedirectToAction(nameof(My));
        }

        // -----------------------------------------------------------
        // Lecturer: Edit an existing draft/rejected claim (Form)
        // GET /Claims/Edit/{id}
        // -----------------------------------------------------------
        [SessionAuthorize("Lecturer")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired.";
                return RedirectToAction("Login", "Account");
            }

            var claim = await _db.MonthlyClaims
                .FirstOrDefaultAsync(c => c.Id == id && c.IcUserId == userId);

            if (claim == null)
                return NotFound();

            if (claim.Status != ClaimStatus.Draft &&
                claim.Status != ClaimStatus.Rejected)
            {
                TempData["warn"] = "Cannot edit this claim.";
                return RedirectToAction(nameof(Review), new { id });
            }

            if (string.IsNullOrWhiteSpace(claim.MonthKey))
                claim.MonthKey = DateTime.UtcNow.ToString("yyyy-MM");

            return View("~/Views/LecturerViews/EditClaim.cshtml", claim);
        }

        // -----------------------------------------------------------
        // Lecturer: Save edits to draft/rejected claim
        // POST /Claims/Edit
        // -----------------------------------------------------------
        [SessionAuthorize("Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MonthlyClaim model, string entriesJson)
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired.";
                return RedirectToAction("Login", "Account");
            }

            var claim = await _db.MonthlyClaims
                .FirstOrDefaultAsync(c => c.Id == model.Id && c.IcUserId == userId);

            if (claim == null)
                return NotFound();

            if (claim.Status != ClaimStatus.Draft &&
                claim.Status != ClaimStatus.Rejected)
            {
                TempData["warn"] = "Cannot edit this claim.";
                return RedirectToAction(nameof(Review), new { id = claim.Id });
            }

            // Load authoritative user & rate
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
                model.Rate = user.HourlyRate;

            // Recompute hours
            if (!string.IsNullOrWhiteSpace(entriesJson))
            {
                try
                {
                    var hours = ComputeTotalHours(model.MonthKey ?? string.Empty, entriesJson);
                    model.Hours = Math.Round(hours, 1, MidpointRounding.AwayFromZero);

                    _logger.LogInformation(
                        "Recomputed hours for claim {Id}: {Hours}",
                        claim.Id, model.Hours);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to recompute hours in Edit for claim {Id}", claim.Id);

                    ModelState.AddModelError(string.Empty,
                        "We could not read your time entries. Please try again.");
                }
            }

            // Validate MonthKey
            if (!string.IsNullOrWhiteSpace(model.MonthKey) &&
                !Regex.IsMatch(model.MonthKey, @"^\d{4}-\d{2}$"))
            {
                ModelState.AddModelError(nameof(model.MonthKey),
                    "Month must be YYYY-MM format.");
            }

            // Remove server authoritative fields
            ModelState.Remove(nameof(model.IcUserId));
            ModelState.Remove(nameof(model.Hours));
            ModelState.Remove(nameof(model.Rate));

            // Ensure correct ownership
            model.IcUserId = userId;
            model.Status = claim.Status;

            if (!TryValidateModel(model))
            {
                _logger.LogWarning("Validation failed in Edit.");
                return View("~/Views/LecturerViews/EditClaim.cshtml", model);
            }

            // Commit changes
            claim.MonthKey = model.MonthKey ?? claim.MonthKey;
            claim.Hours = model.Hours;
            claim.Rate = model.Rate;
            claim.Notes = model.Notes;
            claim.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["ok"] = "Your draft claim has been updated.";
            return RedirectToAction(nameof(Review), new { id = claim.Id });
        }

        // =====================================================================
        // UTILITY METHODS
        // =====================================================================

        /// <summary>
        /// Computes the total hours from a JSON list of work entries.
        /// Performs validation on dates & times and caps hours to 1dp.
        /// </summary>
        private decimal ComputeTotalHours(string monthKey, string entriesJson)
        {
            if (string.IsNullOrWhiteSpace(entriesJson))
                return 0m;

            List<WorkEntryDto>? entries;

            try
            {
                entries = JsonSerializer.Deserialize<List<WorkEntryDto>>(entriesJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize entriesJson. Raw: {Raw}", entriesJson);
                throw;
            }

            if (entries == null || entries.Count == 0)
                return 0m;

            decimal totalHours = 0m;

            foreach (var e in entries)
            {
                if (!DateTime.TryParse(e.date, out var date))
                {
                    _logger.LogWarning("Invalid date: {Date}", e.date);
                    continue;
                }

                if (!TimeSpan.TryParse(e.start, out var start) ||
                    !TimeSpan.TryParse(e.end, out var end))
                {
                    _logger.LogWarning("Invalid time: {Start} - {End}", e.start, e.end);
                    continue;
                }

                if (end <= start)
                {
                    _logger.LogWarning("Non-positive duration: {Start} - {End}", e.start, e.end);
                    continue;
                }

                var hours = (decimal)(end - start).TotalHours;

                if (hours < 0 || hours > 24)
                {
                    _logger.LogWarning("Unreasonable duration: {Hours}", hours);
                    continue;
                }

                totalHours += hours;
            }

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
