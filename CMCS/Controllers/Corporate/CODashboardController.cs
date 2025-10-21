using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers
{
    [Authorize(Roles = "CO")]
    public class CODashboardController : Controller
    {
        public IActionResult Index() => View();

        // Stubs for CO actions:
        public IActionResult OrgSettings() => View();
        public IActionResult UserRoleManagement() => View();
        public IActionResult FinanceReports() => View();
        public IActionResult AuditLog() => View();
    }
}
