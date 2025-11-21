using CMCS.Data;
using CMCS.Infrastructure;
using CMCS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext db, ILogger<AccountController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ========= LOGIN (GET) =========
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View("~/Views/GenericViews/Login.cshtml", new LoginViewModel());
        }

        // ========= LOGIN (POST) =========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View("~/Views/GenericViews/Login.cshtml", model);
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.UserName == model.Username || u.Email == model.Username);

            if (user == null || !PasswordHelper.VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View("~/Views/GenericViews/Login.cshtml", model);
            }

            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("UserFullName", $"{user.FirstName} {user.LastName}");

            _logger.LogInformation("User {UserName} logged in with role {Role}", user.UserName, user.Role);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return user.Role switch
            {
                "Lecturer" => RedirectToAction("Index", "LecturerDashboard"),
                "Coordinator" => RedirectToAction("Index", "ProgrammeCoordinatorDashboard"),
                "AcademicManager" => RedirectToAction("Index", "AcademicManagerDashboard"),
                "HR" => RedirectToAction("Index", "HRDashboard"),
                _ => RedirectToAction("Index", "Home")
            };



        }

        // ========= LOGOUT =========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            _logger.LogInformation("User {UserId} logged out.", HttpContext.Session.GetString("UserId"));
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        // ========= ACCESS DENIED =========
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View("~/Views/GenericViews/AccessDenied.cshtml");
        }

        // ========= RETURN TO DASHBOARD (ROLE-AWARE) =========
        [HttpGet]
        public IActionResult ReturnToDashboard()
        {
            // Read the current user role from Session
            var userRole = HttpContext.Session.GetString("UserRole");

            // If the user somehow get here with no session, send them to Login
            if (string.IsNullOrEmpty(userRole))
            {
                return RedirectToAction("Login", "Account");
            }

            // Redirect based on role
            return userRole switch
            {
                "Lecturer" => RedirectToAction("Index", "LecturerDashboard"),
                "Coordinator" => RedirectToAction("Index", "ProgrammeCoordinatorDashboard"),
                "AcademicManager" => RedirectToAction("Index", "AcademicManagerDashboard"),
                "HR" => RedirectToAction("Index", "HRDashboard"),

                // Fallback – if role is unknown, use the generic dashboard
                _ => RedirectToAction("Index", "Dashboard")
            };
        }


        [HttpGet]
        public IActionResult Exit()
        {
            // HTML to close the browser window
            var html = @"<html>
                    <body>
                        <script>
                            window.onunload = function() {
                                window.close();     // Attempt to close the tab
                            };
                            window.close();         // Immediate attempt
                            setTimeout(function() { window.location.href='about:blank'; }, 500);
                        </script>
                        <p>You may now close this tab.</p>
                    </body>
                 </html>";

            // Schedule application shutdown AFTER response is sent
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                Environment.Exit(0);
            });

            return Content(html, "text/html");
        }

    }
}