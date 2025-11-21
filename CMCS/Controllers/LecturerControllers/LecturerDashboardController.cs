using CMCS.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers.LecturerControllers
{
    [SessionAuthorize("Lecturer")]
    public class LecturerDashboardController : Controller
    {
        // GET: /LecturerDashboard
        public IActionResult Index()
            => View("~/Views/LecturerViews/LecturerDashboard.cshtml");
    }
}
