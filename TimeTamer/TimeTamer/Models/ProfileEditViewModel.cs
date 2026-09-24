using System.ComponentModel.DataAnnotations;

namespace TimeTamer.Models
{
    public class ProfileEditViewModel
    {
        [MaxLength(100)]
        public string? FullName { get; set; }
        [MaxLength(15)]
        public string? PhoneNumber { get; set; }
        [MaxLength(255)]
        public string? Email { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public string? CurrentAvatarUrl { get; set; } 
        public IFormFile? AvatarFile { get; set; }   
    }
}