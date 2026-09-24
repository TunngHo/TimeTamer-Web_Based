using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class Reminder
    {
        [Key]
        public int Id { get; set; }

        public string Message { get; set; } = string.Empty;
        public DateTime RemindAt { get; set; }
        public bool IsSent { get; set; }
        public DateTime? EmailSentAt { get; set; }
        public DateTime? WebDismissedAt { get; set; }

        public int TaskItemId { get; set; }

        [ForeignKey("TaskItemId")]
        public virtual TaskItem? TaskItem { get; set; }
    }
}
