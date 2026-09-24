using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class AdminUserLink
    {
        [Key]
        public int Id { get; set; }

        public int AdminId { get; set; }

        [ForeignKey(nameof(AdminId))]
        public virtual User? Admin { get; set; }

        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public DateTime? RespondedAt { get; set; }
    }
}
