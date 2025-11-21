using System;
using System.Collections.Generic;
using System.IO;
using CMCS.Data;
using CMCS.Infrastructure;
using CMCS.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers
{
    [SessionAuthorize] // any logged-in user; we’ll check roles inside
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public DocumentsController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // ===== Uploads (Lecturer) =====
        [HttpGet]
        public async Task<IActionResult> Upload(int claimId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (!string.Equals(role, "Lecturer", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var claim = await _db.MonthlyClaims
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == claimId && c.IcUserId == userId);

            if (claim == null)
            {
                TempData["warn"] = "Claim not found or you do not have permission to access it.";
                return RedirectToAction("My", "Claims");
            }

            return View("~/Views/Documents/Upload.cshtml", claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int claimId, IFormFile file)
        {
            var userId = HttpContext.Session.GetString("UserId");
            var role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userId))
            {
                TempData["warn"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            if (!string.Equals(role, "Lecturer", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var claim = await _db.MonthlyClaims
                .FirstOrDefaultAsync(c => c.Id == claimId && c.IcUserId == userId);

            if (claim == null)
            {
                TempData["warn"] = "Claim not found or you do not have permission to access it.";
                return RedirectToAction("My", "Claims");
            }

            if (file == null || file.Length == 0)
            {
                TempData["warn"] = "Please choose a file to upload.";
                return RedirectToAction(nameof(Upload), new { claimId });
            }

            const long maxFileSize = 20 * 1024 * 1024; // 20MB
            if (file.Length > maxFileSize)
            {
                TempData["warn"] = "The file is too large. Maximum allowed size is 20 MB.";
                return RedirectToAction(nameof(Upload), new { claimId });
            }

            var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt"
            };

            var ext = Path.GetExtension(file.FileName);
            if (!allowedExts.Contains(ext))
            {
                TempData["warn"] = "Unsupported file type. Allowed types: PDF, images, Word, Excel, CSV, TXT.";
                return RedirectToAction(nameof(Upload), new { claimId });
            }

            var rootPath = _env.WebRootPath ?? "wwwroot";
            var uploadPath = Path.Combine(rootPath, "uploads", "claims", claim.Id.ToString());
            Directory.CreateDirectory(uploadPath);

            var storedName = $"{Guid.NewGuid():N}{ext}";
            var storedPath = Path.Combine(uploadPath, storedName);

            using (var stream = System.IO.File.Create(storedPath))
            {
                await file.CopyToAsync(stream);
            }

            var doc = new Document
            {
                Id = Guid.NewGuid(),
                MonthlyClaimId = claim.Id,
                FileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType,
                FileSize = (int)file.Length,
                StoragePath = $"uploads/claims/{claim.Id}/{storedName}",
                UploadedAt = DateTime.UtcNow,
                UploadedByUserId = userId
            };

            _db.Documents.Add(doc);
            await _db.SaveChangesAsync();

            TempData["ok"] = "Document uploaded successfully.";
            return RedirectToAction("Review", "Claims", new { id = claim.Id });
        }

        // ===== Downloads (Lecturer + ProgrammeCoordinator + AcademicManager + HR) =====
        [HttpGet("/Claims/DownloadDocument/{id:guid}")]
        public async Task<IActionResult> DownloadDocument(Guid id)
        {
            var role = HttpContext.Session.GetString("UserRole");
            var allowedRoles = new[] { "Lecturer", "ProgrammeCoordinator", "AcademicManager", "HR" };

            if (string.IsNullOrEmpty(role) || !allowedRoles.Contains(role))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();

            var fullPath = Path.Combine(_env.WebRootPath ?? "wwwroot",
                doc.StoragePath.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath)) return NotFound();

            var stream = System.IO.File.OpenRead(fullPath);
            return File(stream, doc.ContentType, doc.FileName);
        }
    }
}
