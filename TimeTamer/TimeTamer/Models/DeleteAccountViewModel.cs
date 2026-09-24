using System.ComponentModel.DataAnnotations;

namespace TimeTamer.Models
{
    public class DeleteAccountViewModel
    {
        [Required(ErrorMessage = "Please enter your current password")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;
    }
}
