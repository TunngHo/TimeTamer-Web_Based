using Microsoft.EntityFrameworkCore;
using TimeTamer.Models;

namespace TimeTamer.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<SubTask> SubTasks { get; set; }
        public DbSet<Reminder> Reminders { get; set; }
        public DbSet<TimeLog> TimeLogs { get; set; }
        public DbSet<Goal> Goals { get; set; }
        public DbSet<Habit> Habits { get; set; }
        public DbSet<HabitCheckIn> HabitCheckIns { get; set; }
        public DbSet<AdminMessage> AdminMessages { get; set; }
        public DbSet<UserAchievement> UserAchievements { get; set; }
        public DbSet<AdminUserLink> AdminUserLinks { get; set; }
        public DbSet<UserLoginSession> UserLoginSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AdminMessage>()
                .HasOne(m => m.User)
                .WithMany(u => u.AdminMessages)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AdminMessage>()
                .HasOne(m => m.SenderUser)
                .WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserAchievement>()
                .HasOne(a => a.User)
                .WithMany(u => u.Achievements)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserAchievement>()
                .HasOne(a => a.AwardedByAdmin)
                .WithMany()
                .HasForeignKey(a => a.AwardedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubTask>()
                .HasOne(s => s.CreatedByUser)
                .WithMany()
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<AdminUserLink>()
                .HasOne(l => l.Admin)
                .WithMany(u => u.AdminLinksAsAdmin)
                .HasForeignKey(l => l.AdminId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AdminUserLink>()
                .HasOne(l => l.User)
                .WithMany(u => u.AdminLinksAsUser)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AdminUserLink>()
                .HasIndex(l => new { l.AdminId, l.UserId })
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.UserCode)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.AdminCode);

            modelBuilder.Entity<UserLoginSession>()
                .HasIndex(s => s.SessionKey)
                .IsUnique();

            modelBuilder.Entity<UserLoginSession>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}


