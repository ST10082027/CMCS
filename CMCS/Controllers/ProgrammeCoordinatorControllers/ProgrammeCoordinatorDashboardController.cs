using CMCS.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers.ProgrammeCoordinatorControllers
{
    [SessionAuthorize("Coordinator")]
    public class ProgrammeCoordinatorDashboardController : Controller
    {
        public IActionResult Index()
            => View("~/Views/ProgrammeCoordinatorViews/ProgrammeCoordinatorDashboard.cshtml");
    }
}
