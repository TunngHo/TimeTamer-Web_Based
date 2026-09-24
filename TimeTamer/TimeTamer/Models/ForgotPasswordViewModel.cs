using System.ComponentModel.DataAnnotations;

namespace TimeTamer.Models
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
