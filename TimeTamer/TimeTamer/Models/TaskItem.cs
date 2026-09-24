using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class TaskItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }
        [MaxLength(150)]
        public string? Implementer { get; set; }

        [MaxLength(64)]
        public string? SharedTaskGroupId { get; set; }

        public string? SharedTaskCompletedByUserIds { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? StartTime { get; set; }
        public DateTime? Deadline { get; set; }
        public string Status { get; set; } = "Pending";
        public int PriorityLevel { get; set; } = 1;
        public bool IsCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int UserId { get; set; }

        public int? CategoryId { get; set; }
        public int? GoalId { get; set; }

        [ForeignKey("GoalId")]
        public virtual Goal? Goal { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        public string? Metadata { get; set; }
        public string? ImageUrl { get; set; }

        public virtual ICollection<SubTask>? SubTasks { get; set; }
    }
}




