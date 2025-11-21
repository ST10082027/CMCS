using CMCS.Data;
using CMCS.Infrastructure;
using CMCS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers.AcademicManagerControllers
{
    [SessionAuthorize("AcademicManager")]
    public class ClaimsApprovalController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ClaimsApprovalController> _logger;

        public ClaimsApprovalController(ApplicationDbContext db, ILogger<ClaimsApprovalController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ===== Manager: review queue =====
        // Only claims already verified by the Programme Coordinator
        [HttpGet]
        public async Task<IActionResult> Queue()
        {
            var claims = await _db.MonthlyClaims
                .Include(c => c.IcUser)
                .Include(c => c.Documents)
                .Where(c => c.Status == ClaimStatus.VerifiedByCoordinator)
                .OrderBy(c => c.SubmittedAt)
                .ThenBy(c => c.Id)
                .ToListAsync();

            _logger.LogInformation("Academic Manager loaded approval queue with {Count} claims", claims.Count);

            return View("~/Views/AcademicManagerViews/AcademicManagerReviewQueue.cshtml", claims);
        }

        // ===== Manager: approve =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var claim = await _db.MonthlyClaims.FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null)
            {
                TempData["Error"] = "Claim not found.";
                return RedirectToAction(nameof(Queue));
            }

            if (claim.Status != ClaimStatus.VerifiedByCoordinator)
            {
                TempData["Error"] = "Only claims verified by the Programme Coordinator can be approved.";
                return RedirectToAction(nameof(Queue));
            }

            claim.Status = ClaimStatus.ApprovedByManager;
            claim.ManagerRemark = "Approved by Academic Manager.";
            claim.ApprovedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["OK"] = "Claim approved.";
            return RedirectToAction(nameof(Queue));
        }

        // ===== Manager: reject =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? remark)
        {
            var claim = await _db.MonthlyClaims.FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null)
            {
                TempData["Error"] = "Claim not found.";
                return RedirectToAction(nameof(Queue));
            }

            if (claim.Status != ClaimStatus.VerifiedByCoordinator)
            {
                TempData["Error"] = "Only claims verified by the Programme Coordinator can be rejected.";
                return RedirectToAction(nameof(Queue));
            }

            claim.Status = ClaimStatus.Rejected;
            claim.ManagerRemark = string.IsNullOrWhiteSpace(remark)
                ? "Rejected by Academic Manager."
                : remark.Trim();
            claim.RejectedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["OK"] = "Claim rejected.";
            return RedirectToAction(nameof(Queue));
        }
    }
}
