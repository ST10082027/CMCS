using System.ComponentModel.DataAnnotations;
using CMCS.Data;
using CMCS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Controllers.Api
{
    [ApiController]
    [Route("api/v1/claims-automation")]
    public class ClaimAutomationController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ClaimAutomationController> _logger;

        public ClaimAutomationController(ApplicationDbContext db, ILogger<ClaimAutomationController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // === Request / response DTOs ===

        public class ClaimQuoteRequest
        {
            /// <summary>
            /// Total hours the lecturer worked for the month.
            /// </summary>
            public decimal Hours { get; set; }

            /// <summary>
            /// Month in YYYY-MM format. If omitted, current month is used.
            /// </summary>
            public string? MonthKey { get; set; }
        }

        public class ClaimQuoteResponse
        {
            public string LecturerId { get; set; } = string.Empty;
            public string LecturerName { get; set; } = string.Empty;

            public string MonthKey { get; set; } = string.Empty;

            public decimal Hours { get; set; }
            public decimal Rate { get; set; }
            public decimal Amount { get; set; }

            public string[] Warnings { get; set; } = Array.Empty<string>();
        }

        /// <summary>
        /// Calculate a claim quote for the currently logged-in lecturer.
        /// Uses the lecturer's stored hourly rate and MonthlyClaim validation rules.
        /// </summary>
        [HttpPost("quote")]
        public async Task<ActionResult<ClaimQuoteResponse>> GetQuote([FromBody] ClaimQuoteRequest request)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                // No logged-in user – session empty
                return Unauthorized(new { error = "No active session. Please log in as a Lecturer." });
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Unauthorized(new { error = "User not found for current session." });
            }

            if (!string.Equals(user.Role, "Lecturer", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var effectiveMonthKey = string.IsNullOrWhiteSpace(request.MonthKey)
                ? MonthlyClaim.BuildMonthKey(DateTime.UtcNow.Year, DateTime.UtcNow.Month)
                : request.MonthKey.Trim();

            // Build a transient MonthlyClaim to reuse its validation logic
            var claim = new MonthlyClaim
            {
                IcUserId = user.Id,
                MonthKey = effectiveMonthKey,
                Hours = request.Hours,
                Rate = user.HourlyRate
            };

            var validationContext = new ValidationContext(claim);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(
                claim,
                validationContext,
                validationResults,
                validateAllProperties: true);

            if (!isValid)
            {
                var errors = validationResults
                    .Select(r => r.ErrorMessage ?? "Validation error.")
                    .ToArray();

                _logger.LogWarning("Claim quote validation failed for user {UserId}: {Errors}",
                    user.Id, string.Join("; ", errors));

                return BadRequest(new { errors });
            }

            var response = new ClaimQuoteResponse
            {
                LecturerId = user.Id,
                LecturerName = $"{user.FirstName} {user.LastName}".Trim(),
                MonthKey = effectiveMonthKey,
                Hours = claim.Hours,
                Rate = claim.Rate,
                Amount = claim.Amount,
                Warnings = Array.Empty<string>()
            };

            _logger.LogInformation(
                "Generated claim quote for user {UserId} – Month {MonthKey}, Hours {Hours}, Rate {Rate}, Amount {Amount}",
                user.Id,
                response.MonthKey,
                response.Hours,
                response.Rate,
                response.Amount);

            return Ok(response);
        }
    }
}