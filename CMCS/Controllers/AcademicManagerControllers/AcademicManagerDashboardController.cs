using CMCS.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers.AcademicManagerControllers
{
    [SessionAuthorize("AcademicManager")]
    public class AcademicManagerDashboardController : Controller
    {
        public IActionResult Index()
            => View("~/Views/AcademicManagerViews/AcademicManagerDashboard.cshtml");
    }
}
