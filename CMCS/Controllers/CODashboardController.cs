using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers
{
    [Authorize(Roles = "CO")]
    public class CODashboardController : Controller
    {
        public IActionResult Index() => View();
        public IActionResult UserRoleManagement() => View();
    }
}
