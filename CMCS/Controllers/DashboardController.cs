using CMCS.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers
{
    [SessionAuthorize] // any logged-in user
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Dashboard";
            return View();
        }
    }
}
