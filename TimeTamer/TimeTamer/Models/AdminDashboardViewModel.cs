namespace TimeTamer.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int SuspendedUsers { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int UnreadUserMessages { get; set; }
        public PaginatedList<AdminUserSummaryViewModel> Users { get; set; } = PaginatedList<AdminUserSummaryViewModel>.Create(Array.Empty<AdminUserSummaryViewModel>(), 1, 10);
    }

    public class AdminUserSummaryViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool IsSuspended { get; set; }
        public int TotalTasks { get; set; }
        public int OpenTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int AchievementCount { get; set; }
        public int UnreadMessages { get; set; }
        public double CompletionRate { get; set; }
        public DateTime? LastTaskCreatedAt { get; set; }
    }

    public class AdminUserDetailViewModel
    {
        public AdminUserSummaryViewModel Summary { get; set; } = new();
        public List<TaskItem> RecentTasks { get; set; } = new();
        public List<AdminMessage> Messages { get; set; } = new();
        public List<UserAchievement> Achievements { get; set; } = new();
    }

    public class UserMessageCenterViewModel
    {
        public List<AdminMessage> Messages { get; set; } = new();
        public List<UserAchievement> Achievements { get; set; } = new();
    }
}

