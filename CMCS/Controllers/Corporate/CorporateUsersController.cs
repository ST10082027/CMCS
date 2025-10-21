using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CMCS.Data;                   // ApplicationDbContext, ApplicationUser
using CMCS.Models.ViewModels;      // CreateUserViewModel, CorporateUserListItem
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers
{
    [Authorize(Roles = "CO")] // Restrict to Corporate users
    public class CorporateUsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public CorporateUsersController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // -------- Index: list users with role + hourly rate (for ICs) ----------
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();

            var list = new List<CorporateUserListItem>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var role = roles.FirstOrDefault() ?? "-";
                list.Add(new CorporateUserListItem
                {
                    Id = u.Id,
                    Email = u.Email,
                    UserName = u.UserName,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = role,
                    HourlyRate = role == "IC" ? u.HourlyRate : (decimal?)null
                });
            }

            return View(list);
        }

        // -------------------- Create (GET) --------------------
        public IActionResult Create()
        {
            var vm = new CreateUserViewModel
            {
                AvailableRoles = new List<string> { "IC", "MR", "CO" }
            };
            return View(vm);
        }

        // -------------------- Create (POST) --------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel vm)
        {
            vm.AvailableRoles = new List<string> { "IC", "MR", "CO" };

            if (!ModelState.IsValid)
                return View(vm);

            // Ensure role exists
            if (!await _roleManager.RoleExistsAsync(vm.SelectedRole))
            {
                ModelState.AddModelError(nameof(vm.SelectedRole), "Selected role does not exist.");
                return View(vm);
            }

            var user = new ApplicationUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                FirstName = vm.FirstName?.Trim(),
                LastName = vm.LastName?.Trim(),
                HourlyRate = vm.SelectedRole == "IC" ? (vm.HourlyRate ?? 0m) : 0m
            };

            var createResult = await _userManager.CreateAsync(user, vm.Password);
            if (!createResult.Succeeded)
            {
                foreach (var e in createResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return View(vm);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, vm.SelectedRole);
            if (!roleResult.Succeeded)
            {
                foreach (var e in roleResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                // Cleanup user if role assignment fails
                await _userManager.DeleteAsync(user);
                return View(vm);
            }

            TempData["OK"] = "User created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // -------------------- EditRate (GET) --------------------
        // Edit hourly rate for an IC only
        public async Task<IActionResult> EditRate(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("IC"))
            {
                TempData["Error"] = "Only IC users have an hourly rate.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Email = user.Email;
            ViewBag.UserId = user.Id;
            return View(model: user.HourlyRate);
        }

        // -------------------- EditRate (POST) --------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRate(string id, decimal hourlyRate)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("IC"))
            {
                TempData["Error"] = "Only IC users have an hourly rate.";
                return RedirectToAction(nameof(Index));
            }

            if (hourlyRate < 0 || hourlyRate > 100000)
            {
                ModelState.AddModelError(string.Empty, "Please enter a valid hourly rate.");
                ViewBag.Email = user.Email;
                ViewBag.UserId = user.Id;
                return View(model: user.HourlyRate);
            }

            user.HourlyRate = hourlyRate;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                ViewBag.Email = user.Email;
                ViewBag.UserId = user.Id;
                return View(model: user.HourlyRate);
            }

            TempData["OK"] = "Hourly rate updated.";
            return RedirectToAction(nameof(Index));
        }
    }
}
