using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using CMCS.Data;

namespace CMCS.Models
{
    public enum ClaimStatus
    {
        Draft = 0,
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }

    public class MonthlyClaim : IValidatableObject
    {
        public int Id { get; set; }

        // === Ownership ===
        [Required]
        public string IcUserId { get; set; } = string.Empty;
        public ApplicationUser? IcUser { get; set; }

        // === Period key (e.g. "2025-10") ===
        // Stored column (see migration AddMonthlyClaims): e.g. "2025-10"
        [Required]
        [Display(Name = "Month")]
        [RegularExpression(@"^\d{4}-\d{2}$", ErrorMessage = "Month must be formatted as YYYY-MM")]
        public string MonthKey { get; set; } = $"{DateTime.UtcNow:yyyy-MM}";

        // Convenience accessors (not mapped)
        [NotMapped]
        public int Year
        {
            get => int.Parse(MonthKey[..4]);
            set => MonthKey = $"{value:D4}-{Month:D2}";
        }

        [NotMapped]
        public int Month
        {
            get => int.Parse(MonthKey[5..7]);
            set
            {
                var year = Year;
                MonthKey = $"{year:D4}-{value:D2}";
            }
        }

        [NotMapped]
        public string PeriodLabel => MonthKey;

        // === Financials ===
        [Range(0, 5000)]
        [Column(TypeName = "decimal(7,2)")]
        [Display(Name = "Hours")]
        public decimal Hours { get; set; }

        [Range(0, 100000)]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Rate (R/hr)")]
        public decimal Rate { get; set; }

        [NotMapped]
        [Display(Name = "Amount (R)")]
        public decimal Amount => Math.Round(Hours * Rate, 2);

        // === Workflow/status ===
        [Display(Name = "Submitted At")]
        public DateTime? SubmittedAt { get; set; }

        [Display(Name = "Status")]
        public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

        [Display(Name = "Manager Remark")]
        [StringLength(2000)]
        public string? ManagerRemark { get; set; }

        // Lecturer can add context before submitting
        [Display(Name = "Notes")]
        [StringLength(2000)]
        public string? Notes { get; set; }

        // === Timestamps for transparency ===
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Updated At")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Approved At")]
        public DateTime? ApprovedAt { get; set; }

        [Display(Name = "Rejected At")]
        public DateTime? RejectedAt { get; set; }

        // === Helpers ===
        public static string BuildMonthKey(int year, int month) => $"{year:D4}-{month:D2}";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Validate MonthKey format and range
            if (!Regex.IsMatch(MonthKey, @"^\d{4}-\d{2}$"))
            {
                yield return new ValidationResult(
                    "Month must be formatted as YYYY-MM.",
                    new[] { nameof(MonthKey) });
            }
            else
            {
                if (Month < 1 || Month > 12)
                {
                    yield return new ValidationResult(
                        "Month must be between 1 and 12.",
                        new[] { nameof(Month) });
                }
            }

            // If pending, ensure SubmittedAt is present
            if (Status == ClaimStatus.Pending && SubmittedAt == null)
            {
                yield return new ValidationResult(
                    "SubmittedAt must be set when the claim is Pending.",
                    new[] { nameof(SubmittedAt), nameof(Status) });
            }

            // Basic sanity: Amount must be >= 0 (given your ranges this should hold)
            if (Amount < 0)
            {
                yield return new ValidationResult(
                    "Calculated Amount cannot be negative.",
                    new[] { nameof(Hours), nameof(Rate) });
            }
        }
    }
}
