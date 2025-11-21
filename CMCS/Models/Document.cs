using System;

namespace CMCS.Models
{
    public class Document
    {
        public Guid Id { get; set; }

        // FK must match MonthlyClaim.Id (int)
        public int MonthlyClaimId { get; set; }

        // Single, unambiguous navigation
        public MonthlyClaim MonthlyClaim { get; set; } = null!;

        public string FileName { get; set; } = null!;
        public string ContentType { get; set; } = "application/octet-stream";
        public int FileSize { get; set; }

        // Relative path under wwwroot, e.g. "uploads/claims/123/xyz.pdf"
        public string StoragePath { get; set; } = null!;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Set from the logged-in user (Session) when the document is created.
        // Non-nullable because every document must have an uploader.
        public string UploadedByUserId { get; set; } = null!;
    }
}
