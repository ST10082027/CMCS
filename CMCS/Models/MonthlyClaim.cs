using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace CMCS.Models
{
    public enum ClaimStatus
    {
        Draft = 0,                  // Lecturer still editing
        Pending = 1,                // Submitted by Lecturer – waiting for Coordinator
        VerifiedByCoordinator = 2,  // Coordinator has verified
        ApprovedByManager = 3,      // Academic Manager has approved

        // Alias to keep existing views happy (ClaimStatus.Approved)
        Approved = ApprovedByManager,

        Rejected = 4,               // Rejected by Coordinator or Manager
        FinalisedByHR = 5           // Finalised by HR
    }

    public class MonthlyClaim : IValidatableObject
    {
        public int Id { get; set; }

        // === Ownership (Lecturer) ===
        [Required]
        public string IcUserId { get; set; } = string.Empty;

        // Custom user (Session-based auth)
        public UserAccount? IcUser { get; set; }

        // Programme Coordinator who verified the claim (optional)
        public string? CoordinatorUserId { get; set; }
        public UserAccount? CoordinatorUser { get; set; }

        // Convenience property used in some older views (maps to IcUser)
        [NotMapped]
        public UserAccount? LecturerUser
        {
            get => IcUser;
            set => IcUser = value;
        }

        // === Period key (YYYY-MM) ===
        [Required]
        [Display(Name = "Month")]
        [RegularExpression(@"^\d{4}-\d{2}$", ErrorMessage = "Month must be formatted as YYYY-MM")]
        public string MonthKey { get; set; } = $"{DateTime.UtcNow:yyyy-MM}";

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
        /// <summary>
        /// Total hours for the month. Spec: must not exceed 180 hours.
        /// </summary>
        [Range(0, 180, ErrorMessage = "Total hours for a month cannot exceed 180.")]
        [Column(TypeName = "decimal(7,2)")]
        public decimal Hours { get; set; }

        [Range(0, 100000)]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Rate (R/hr)")]
        public decimal Rate { get; set; }

        [NotMapped]
        [Display(Name = "Amount (R)")]
        public decimal Amount => Math.Round(Hours * Rate, 2);

        // === Workflow & timestamps ===
        public DateTime? SubmittedAt { get; set; }

        public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

        /// <summary>
        /// When the Programme Coordinator verified the claim.
        /// </summary>
        public DateTime? VerifiedAt { get; set; }

        [StringLength(2000)]
        public string? ManagerRemark { get; set; }

        public ICollection<Document> Documents { get; set; } = new List<Document>();

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
        public DateTime? RejectedAt { get; set; }

        public static string BuildMonthKey(int year, int month) => $"{year:D4}-{month:D2}";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Validate month format
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

            // Business rule: max 180 hours per month (server-side, even if the UI misbehaves)
            if (Hours > 180)
            {
                yield return new ValidationResult(
                    "Total hours for a single month may not exceed 180.",
                    new[] { nameof(Hours) });
            }

            if (Hours < 0)
            {
                yield return new ValidationResult(
                    "Hours cannot be negative.",
                    new[] { nameof(Hours) });
            }

            if (Rate < 0)
            {
                yield return new ValidationResult(
                    "Hourly rate cannot be negative.",
                    new[] { nameof(Rate) });
            }

            // If pending, SubmittedAt must exist
            if (Status == ClaimStatus.Pending && SubmittedAt == null)
            {
                yield return new ValidationResult(
                    "SubmittedAt must be set when the claim is Pending.",
                    new[] { nameof(SubmittedAt), nameof(Status) });
            }

            if (Amount < 0)
            {
                yield return new ValidationResult(
                    "Calculated Amount cannot be negative.",
                    new[] { nameof(Hours), nameof(Rate) });
            }
        }
    }
}
