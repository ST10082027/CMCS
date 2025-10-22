using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers
{
    [Authorize(Roles = "IC")]
    public class ICDashboardController : Controller
    {
        public IActionResult Index() => View();

        // Stubs for IC actions:
        public IActionResult SubmitMonthlyClaim() => View();
        public IActionResult ClaimHistory() => View();
        public IActionResult Profile() => View();
    }
}
