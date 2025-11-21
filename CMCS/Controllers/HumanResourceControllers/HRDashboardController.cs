using CMCS.Data;
using CMCS.Infrastructure;
using CMCS.Models.HumanResourceModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers.HumanResourceControllers
{
    [SessionAuthorize("HR")]
    public class HRDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HRDashboardController(ApplicationDbContext db)
        {
            _db = db;
        }

        // HR lands here after login → redirect to full user management screen.
        public IActionResult Index()
        {
            return RedirectToAction("Index", "HRManageUsers");
        }
    }
}
