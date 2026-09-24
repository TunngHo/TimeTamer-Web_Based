using System.ComponentModel.DataAnnotations;

namespace TimeTamer.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Please enter your current password")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a new password")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm the new password")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The confirmation password does not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
