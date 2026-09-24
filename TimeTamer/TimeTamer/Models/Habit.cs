using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class Habit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(140)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(30)]
        public string Schedule { get; set; } = "Daily";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsArchived { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public virtual ICollection<HabitCheckIn> CheckIns { get; set; } = new List<HabitCheckIn>();
    }
}
