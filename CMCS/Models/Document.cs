using System.ComponentModel.DataAnnotations;

namespace CMCS.Models
{
    public class Document
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public int MonthlyClaimId { get; set; }  // FK to MonthlyClaim

        [Required, MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string ContentType { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int FileSize { get; set; }

        [Required, MaxLength(512)]
        public string StoragePath { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
