using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Models
{
    /// <summary>
    /// Custom user entity for Session-based auth.
    /// HR will create these users.
    /// </summary>
    public class UserAccount
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required, MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        [Required, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Role { get; set; } = string.Empty; // "Lecturer", "Coordinator", "AcademicManager", "HR"

        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Range(0, 100000)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; } = 0m;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(100)]
        public string FullName => $"{FirstName} {LastName}".Trim();

    }
}
