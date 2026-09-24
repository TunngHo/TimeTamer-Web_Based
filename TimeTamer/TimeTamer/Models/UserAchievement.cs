using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class UserAchievement
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        public int AwardedByAdminId { get; set; }

        [ForeignKey(nameof(AwardedByAdminId))]
        public virtual User? AwardedByAdmin { get; set; }

        [Required]
        [MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(800)]
        public string? Description { get; set; }

        [MaxLength(40)]
        public string Icon { get; set; } = "trophy";

        public DateTime AwardedAt { get; set; } = DateTime.Now;
    }
}
