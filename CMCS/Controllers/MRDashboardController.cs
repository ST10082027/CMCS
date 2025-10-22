using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers
{
    [Authorize(Roles = "MR")]
    public class MRDashboardController : Controller
    {
        public IActionResult Index() => View();

        // Stubs for MR actions:
        public IActionResult ReviewClaims() => View();
        public IActionResult ApproveReject() => View();
        public IActionResult TeamOverview() => View();
        public IActionResult Reports() => View();
    }
}
