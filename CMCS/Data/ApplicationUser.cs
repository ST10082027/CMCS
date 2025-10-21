using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS.Data
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(50)]
        public string FirstName { get; set; }

        [MaxLength(50)]
        public string LastName { get; set; }

        // CO-assigned hourly wage for Independent Contractors (R/hour)
        [Range(0, 100000)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; } = 0m;
    }
}
