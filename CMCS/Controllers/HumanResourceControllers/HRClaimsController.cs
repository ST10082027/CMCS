using CMCS.Data;
using CMCS.Infrastructure;
using CMCS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers.HumanResourceControllers
{
    [SessionAuthorize("HR")]
    public class HRClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HRClaimsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Simple landing page – redirect to overview for now
        public IActionResult Index()
        {
            return RedirectToAction(nameof(All));
        }

        // ===== HR: Overview of all claims =====
        public async Task<IActionResult> All()
        {
            var list = await _db.MonthlyClaims
                .Include(c => c.IcUser)
                .Include(c => c.Documents)
                .OrderByDescending(c => c.SubmittedAt)
                .ThenByDescending(c => c.Id)
                .ToListAsync();

            // Uses existing HumanResource view
            return View("~/Views/HumanResourceViews/HRClaimsOverview.cshtml", list);
        }

        // ===== HR: Detailed report for claims =====
        [HttpGet]
        public async Task<IActionResult> Report(string? monthKey, string? statusFilter)
        {
            // Base query with all the info we need
            var query = _db.MonthlyClaims
                .Include(c => c.IcUser)
                .Include(c => c.CoordinatorUser)
                .Include(c => c.Documents)
                .AsQueryable();

            // Default behaviour: only ApprovedByManager claims if no filter chosen
            if (string.IsNullOrWhiteSpace(statusFilter))
            {
                statusFilter = ClaimStatus.ApprovedByManager.ToString();
            }

            if (!string.IsNullOrWhiteSpace(monthKey))
            {
                query = query.Where(c => c.MonthKey == monthKey);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter)
                && Enum.TryParse<ClaimStatus>(statusFilter, out var parsedStatus))
            {
                query = query.Where(c => c.Status == parsedStatus);
            }

            var claims = await query
                .OrderBy(c => c.IcUser!.LastName)
                .ThenBy(c => c.IcUser!.FirstName)
                .ThenBy(c => c.MonthKey)
                .ToListAsync();

            // For dropdown filter options
            ViewBag.MonthOptions = await _db.MonthlyClaims
                .Select(c => c.MonthKey)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

            ViewBag.StatusOptions = Enum.GetNames(typeof(ClaimStatus));
            ViewBag.SelectedMonth = monthKey;
            ViewBag.SelectedStatus = statusFilter;

            return View("~/Views/HumanResourceViews/HRClaimsReport.cshtml", claims);
        }
    }
}