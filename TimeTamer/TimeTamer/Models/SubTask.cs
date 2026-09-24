using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class SubTask
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }

        public int TaskItemId { get; set; }


        [ForeignKey("TaskItemId")]
        public virtual TaskItem? TaskItem { get; set; }

        public int? CreatedByUserId { get; set; }

        [ForeignKey(nameof(CreatedByUserId))]
        public virtual User? CreatedByUser { get; set; }

        [MaxLength(120)]
        public string? CreatedByName { get; set; }

        [MaxLength(64)]
        public string? SharedSubTaskGroupId { get; set; }

        public int SortOrder { get; set; } = 0;
        public int EstimatedMinutes { get; set; } = 0;
        public DateTime? Deadline { get; set; }
    }
}
