using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class AdminMessage
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        public int SenderUserId { get; set; }

        [ForeignKey(nameof(SenderUserId))]
        public virtual User? SenderUser { get; set; }

        public bool IsFromAdmin { get; set; }

        [MaxLength(120)]
        public string Subject { get; set; } = "General";

        [Required]
        [MaxLength(2000)]
        public string Body { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.Now;
        public DateTime? ReadAt { get; set; }
    }
}
