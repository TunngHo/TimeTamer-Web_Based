using System.ComponentModel.DataAnnotations;

namespace TimeTamer.Models
{
    public class VerifyOtpViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(6)]
        public string Otp { get; set; } = string.Empty;
    }
}
