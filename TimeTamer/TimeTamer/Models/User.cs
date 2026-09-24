using System.ComponentModel.DataAnnotations;

namespace TimeTamer.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter a username")]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a password")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(255)]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? FullName { get; set; }

        [MaxLength(15)]
        public string? PhoneNumber { get; set; }

        public DateTime? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
        public string? PasswordResetTokenHash { get; set; }
        public DateTime? PasswordResetTokenExpiresAt { get; set; }
        public DateTime? PasswordResetRequestedAt { get; set; }
        public int PasswordResetOtpAttempts { get; set; }
        public DateTime? PasswordResetVerifiedAt { get; set; }
        public int DailyUsageLimitMinutes { get; set; } = 120;
        public DateTime LastLoginDate { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsSuspended { get; set; }
        [MaxLength(32)]
        public string UserCode { get; set; } = string.Empty;
        [MaxLength(32)]
        public string? AdminCode { get; set; }

        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
        public virtual ICollection<AdminMessage> AdminMessages { get; set; } = new List<AdminMessage>();
        public virtual ICollection<UserAchievement> Achievements { get; set; } = new List<UserAchievement>();
        public virtual ICollection<AdminUserLink> AdminLinksAsUser { get; set; } = new List<AdminUserLink>();
        public virtual ICollection<AdminUserLink> AdminLinksAsAdmin { get; set; } = new List<AdminUserLink>();
    }
}

