using CMCS.Data;
using CMCS.Infrastructure;
using CMCS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers.ProgrammeCoordinatorControllers
{
    [SessionAuthorize("Coordinator")]
    public class ClaimsReviewController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ClaimsReviewController(ApplicationDbContext db)
        {
            _db = db;
        }

        // List all claims that are waiting for Coordinator verification
        public async Task<IActionResult> Queue()
        {
            var list = await _db.MonthlyClaims
    .Include(c => c.Documents)
    .Include(c => c.IcUser) // <- this is the navigation property
    .Where(c => c.Status == ClaimStatus.Pending)
    .OrderByDescending(c => c.SubmittedAt)
    .ToListAsync();

            // and also point to the correct view file:
            return View("~/Views/ProgrammeCoordinatorViews/CoordinatorReviewQueue.cshtml", list);

        }
        // Review a specific claim
        public async Task<IActionResult> Review(int id)
        {
            var claim = await _db.MonthlyClaims
    .Include(c => c.Documents)
    .Include(c => c.IcUser)
    .FirstOrDefaultAsync(c => c.Id == id);


            if (claim == null)
            {
                TempData["Error"] = "Claim not found.";
                return RedirectToAction(nameof(Queue));
            }

            if (claim.Status != ClaimStatus.Pending)
            {
                TempData["Error"] = "Only pending claims can be reviewed here.";
                return RedirectToAction(nameof(Queue));
            }

            return View("~/Views/ProgrammeCoordinatorViews/CoordinatorReviewClaim.cshtml", claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id, string? remark)
        {
            var claim = await _db.MonthlyClaims.FindAsync(id);
            if (claim == null)
            {
                TempData["Error"] = "Claim not found.";
                return RedirectToAction(nameof(Queue));
            }

            if (claim.Status != ClaimStatus.Pending)
            {
                TempData["Error"] = "Only pending claims can be verified.";
                return RedirectToAction(nameof(Queue));
            }

            var coordinatorId = HttpContext.Session.GetString("UserId");
            claim.Status = ClaimStatus.VerifiedByCoordinator;
            claim.CoordinatorUserId = coordinatorId;
            claim.VerifiedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(remark))
            {
                claim.ManagerRemark = remark.Trim(); // reuse remark field
            }

            await _db.SaveChangesAsync();
            TempData["OK"] = "Claim verified and sent to the Academic Manager.";
            return RedirectToAction(nameof(Queue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? remark)
        {
            var claim = await _db.MonthlyClaims.FindAsync(id);
            if (claim == null)
            {
                TempData["Error"] = "Claim not found.";
                return RedirectToAction(nameof(Queue));
            }

            if (claim.Status != ClaimStatus.Pending)
            {
                TempData["Error"] = "Only pending claims can be rejected.";
                return RedirectToAction(nameof(Queue));
            }

            claim.Status = ClaimStatus.Rejected;
            claim.ManagerRemark = string.IsNullOrWhiteSpace(remark) ? "Rejected by Coordinator." : remark.Trim();

            await _db.SaveChangesAsync();
            TempData["OK"] = "Claim rejected.";
            return RedirectToAction(nameof(Queue));
        }
    }
}
