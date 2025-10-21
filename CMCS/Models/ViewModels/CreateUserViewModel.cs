using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CMCS.Models.ViewModels
{
    public class CreateUserViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, MinLength(6)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Display(Name = "Role")]
        [Required(ErrorMessage = "Please select a role")]
        public string SelectedRole { get; set; }

        // Only required/used when creating an IC
        [Display(Name = "Hourly Rate (R/hr)")]
        [Range(0, 100000)]
        public decimal? HourlyRate { get; set; }

        public List<string> AvailableRoles { get; set; } = new();
    }

    public class CorporateUserListItem
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set;}
        public string LastName { get; set; }
        public string Role { get; set; }

        // For ICs, this shows their CO-assigned hourly rate; 0 / null for other roles
        public decimal? HourlyRate { get; set; }
    }
}
