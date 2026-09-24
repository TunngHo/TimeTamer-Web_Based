using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class Goal
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(600)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string TaskType { get; set; } = "Work";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? TargetDate { get; set; }
        public bool IsCompleted { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}

