using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    public class MonthlyClaim
    {
        public int Id { get; set; }

        [Required]
        public string IcUserId { get; set; } = string.Empty;

        public ApplicationUser? IcUser { get; set; }

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

        [Range(0, 5000)]
        [Display(Name = "Hours")]
        public decimal Hours { get; set; }

        [Range(0, 100000)]
        [Display(Name = "Rate (R/hr)")]
        public decimal Rate { get; set; }

        [NotMapped]
        [Display(Name = "Amount (R)")]
        public decimal Amount => Math.Round(Hours * Rate, 2);

        [Display(Name = "Submitted At")]
        public DateTime? SubmittedAt { get; set; }

        [Display(Name = "Status")]
        public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

        [Display(Name = "Manager Remark")]
        [StringLength(2000)]
        public string? ManagerRemark { get; set; }

        public static string BuildMonthKey(int year, int month) => $"{year:D4}-{month:D2}";
    }
}
