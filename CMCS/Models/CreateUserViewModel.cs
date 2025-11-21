using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CMCS.Models.HumanResourceModels
{
    public class CreateUserViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "First Name")]
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        [Required]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Hourly Rate (for Lecturers)")]
        [Range(0, 10_000)]
        public decimal? HourlyRate { get; set; }

        [Display(Name = "Role")]
        [Required]
        public string SelectedRole { get; set; } = string.Empty;

        public IEnumerable<string> AvailableRoles { get; set; } = new List<string>();
    }

    public class CorporateUserListItem
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        // For ICs/Lecturers, this shows their HR-assigned hourly rate;
        // 0 / null for other roles
        public decimal? HourlyRate { get; set; }
    }
}
