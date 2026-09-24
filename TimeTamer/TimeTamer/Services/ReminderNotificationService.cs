using Microsoft.EntityFrameworkCore;
using TimeTamer.Data;

namespace TimeTamer.Services
{
    public class ReminderNotificationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReminderNotificationService> _logger;

        public ReminderNotificationService(IServiceProvider serviceProvider, ILogger<ReminderNotificationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SendDueReminderEmails(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reminder notification cycle failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
            }
        }

        private async Task SendDueReminderEmails(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifications = scope.ServiceProvider.GetRequiredService<RealtimeNotificationService>();
            var now = DateTime.Now;

            var dueReminders = await context.Reminders
                .Include(r => r.TaskItem)
                .Where(r => r.RemindAt <= now
                    && r.EmailSentAt == null
                    && r.TaskItem != null
                    && !r.TaskItem.IsDeleted
                    && !r.TaskItem.IsArchived)
                .OrderBy(r => r.RemindAt)
                .Take(25)
                .ToListAsync(stoppingToken);

            foreach (var reminder in dueReminders)
            {
                var task = reminder.TaskItem;
                if (task == null) continue;

                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == task.UserId, stoppingToken);
                if (user == null || string.IsNullOrWhiteSpace(user.Email))
                {
                    reminder.EmailSentAt = now;
                    continue;
                }

                await notifications.NotifyUserAsync(user.Id, new
                {
                    id = reminder.Id,
                    type = "reminder",
                    title = task.Title,
                    message = string.IsNullOrWhiteSpace(reminder.Message) ? "Task reminder" : reminder.Message,
                    time = reminder.RemindAt.ToString("MM/dd/yyyy HH:mm"),
                    url = $"/Task/Edit/{task.Id}",
                    icon = "bi-bell-fill",
                    dismissUrl = "/Reminders/DismissDue"
                });

                var subject = $"TimeTamer reminder: {task.Title}";
                var body = $@"
                    <div style='font-family:Arial,sans-serif;line-height:1.6;color:#172033'>
                        <h2 style='margin-bottom:8px'>Task reminder</h2>
                        <p><strong>{System.Net.WebUtility.HtmlEncode(task.Title)}</strong></p>
                        <p>{System.Net.WebUtility.HtmlEncode(reminder.Message)}</p>
                        <p>Remind time: {reminder.RemindAt:MM/dd/yyyy HH:mm}</p>
                        {(task.Deadline.HasValue ? $"<p>Deadline: {task.Deadline.Value:MM/dd/yyyy HH:mm}</p>" : string.Empty)}
                    </div>";

                var sent = await emailSender.SendAsync(user.Email, subject, body);
                if (sent)
                {
                    reminder.EmailSentAt = DateTime.Now;
                }
            }

            if (dueReminders.Any())
            {
                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
