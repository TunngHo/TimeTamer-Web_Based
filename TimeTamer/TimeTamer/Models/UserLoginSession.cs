using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class UserLoginSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(80)]
        public string SessionKey { get; set; } = string.Empty;

        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddHours(2);
        public DateTime? RevokedAt { get; set; }
    }
}
