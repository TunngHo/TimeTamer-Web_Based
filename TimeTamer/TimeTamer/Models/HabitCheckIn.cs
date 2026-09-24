using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class HabitCheckIn
    {
        [Key]
        public int Id { get; set; }

        public DateTime CheckDate { get; set; } = DateTime.Today;

        [MaxLength(300)]
        public string? Notes { get; set; }

        public int HabitId { get; set; }

        [ForeignKey("HabitId")]
        public virtual Habit? Habit { get; set; }
    }
}
