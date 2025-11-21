using CMCS.Data;
using CMCS.Infrastructure;
using CMCS.Models;
using CMCS.Models.HumanResourceModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers.HumanResourceControllers
{
    [SessionAuthorize("HR")]
    public class HRManageUsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<HRManageUsersController> _logger;

        public HRManageUsersController(ApplicationDbContext db, ILogger<HRManageUsersController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ========= LIST USERS (HRDashboard.cshtml) =========
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.UserName)
                .Select(u => new CorporateUserListItem
                {
                    Id = u.Id,
                    Email = u.Email,
                    UserName = u.UserName,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = u.Role,
                    HourlyRate = u.Role == "Lecturer" ? u.HourlyRate : null
                })
                .ToListAsync();

            // Uses the HR dashboard view
            return View("~/Views/HumanResourceViews/HRDashboard.cshtml", users);
        }

        // ========= CREATE USER (GET) -> HRCreateUser.cshtml =========
        [HttpGet]
        public IActionResult Create()
        {
            var vm = new HRUserEditViewModel
            {
                Role = "Lecturer" // default role
            };

            return View("~/Views/HumanResourceViews/HRCreateUser.cshtml", vm);
        }

        // ========= CREATE USER (POST) -> HRCreateUser.cshtml =========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HRUserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/HumanResourceViews/HRCreateUser.cshtml", model);
            }

            // Enforce unique email/username
            var existing = await _db.Users
                .AnyAsync(u => u.Email == model.Email || u.UserName == model.UserName);

            if (existing)
            {
                ModelState.AddModelError(string.Empty, "A user with that email or username already exists.");
                return View("~/Views/HumanResourceViews/HRCreateUser.cshtml", model);
            }

            var user = new UserAccount
            {
                Id = Guid.NewGuid().ToString(),
                UserName = model.UserName.Trim(),
                Email = model.Email.Trim(),
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber)
                    ? null
                    : model.PhoneNumber.Trim(),
                Role = model.Role,
                HourlyRate = model.Role == "Lecturer" ? model.HourlyRate : 0m,
                PasswordHash = PasswordHelper.HashPassword(model.Password ?? string.Empty)
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            TempData["OK"] = $"User {user.UserName} ({user.Role}) created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ========= EDIT USER (GET) -> HRCreateUser.cshtml =========
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var vm = new HRUserEditViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                HourlyRate = user.HourlyRate,
                Password = string.Empty // optional reset
            };

            return View("~/Views/HumanResourceViews/HRCreateUser.cshtml", vm);
        }

        // ========= EDIT USER (POST) -> HRCreateUser.cshtml =========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(HRUserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/HumanResourceViews/HRCreateUser.cshtml", model);
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == model.Id);
            if (user == null) return NotFound();

            // Unique check for email/username excluding this user
            var duplicate = await _db.Users
                .AnyAsync(u =>
                    (u.Email == model.Email || u.UserName == model.UserName) &&
                    u.Id != model.Id);

            if (duplicate)
            {
                ModelState.AddModelError(string.Empty, "Another user with that email or username already exists.");
                return View("~/Views/HumanResourceViews/HRCreateUser.cshtml", model);
            }

            user.UserName = model.UserName.Trim();
            user.Email = model.Email.Trim();
            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber)
                ? null
                : model.PhoneNumber.Trim();
            user.Role = model.Role;
            user.HourlyRate = model.Role == "Lecturer" ? model.HourlyRate : 0m;

            // Optional password reset
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                user.PasswordHash = PasswordHelper.HashPassword(model.Password);
            }

            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            TempData["OK"] = $"User {user.UserName} updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ========= EDIT LECTURER RATE (GET) -> HREditUserRate.cshtml =========
        [HttpGet]
        public async Task<IActionResult> EditRate(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "Lecturer");
            if (user == null) return NotFound();

            ViewBag.Email = user.Email;
            ViewBag.UserId = user.Id;

            // View model here is just decimal (current hourly rate)
            return View("~/Views/HumanResourceViews/HREditUserRate.cshtml", user.HourlyRate);
        }

        // ========= EDIT LECTURER RATE (POST) -> HREditUserRate.cshtml =========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRate(string id, decimal hourlyRate)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            if (hourlyRate < 0)
            {
                ModelState.AddModelError(string.Empty, "Hourly rate must be a non-negative value.");

                var badUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
                if (badUser == null) return NotFound();

                ViewBag.Email = badUser.Email;
                ViewBag.UserId = badUser.Id;

                return View("~/Views/HumanResourceViews/HREditUserRate.cshtml", hourlyRate);
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "Lecturer");
            if (user == null) return NotFound();

            user.HourlyRate = hourlyRate;
            await _db.SaveChangesAsync();

            TempData["OK"] = $"Hourly rate updated for {user.UserName}.";
            return RedirectToAction(nameof(Index));
        }

        // ========= DELETE (GET CONFIRM) =========
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            return View(user);
        }

        // ========= DELETE (POST) =========
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            TempData["OK"] = $"User {user.UserName} deleted.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ========= VIEWMODEL USED BY HRCreateUser.cshtml =========
    public class HRUserEditViewModel
    {
        public string? Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Email { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        public string Role { get; set; } = "Lecturer"; // Lecturer, Coordinator, AcademicManager, HR

        [System.ComponentModel.DataAnnotations.Range(0, 100000)]
        [System.ComponentModel.DataAnnotations.Display(Name = "Hourly Rate (R/hr)")]
        public decimal HourlyRate { get; set; }

        [System.ComponentModel.DataAnnotations.DataType(
            System.ComponentModel.DataAnnotations.DataType.Password)]
        [System.ComponentModel.DataAnnotations.Display(Name = "Password (leave blank to keep current)")]
        public string? Password { get; set; }
    }
}