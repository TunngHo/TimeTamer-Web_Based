using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TimeTamer.Data;
using TimeTamer.Models;
using TimeTamer.Services;

namespace TimeTamer.Controllers
{
    [Authorize]
    public class RemindersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RealtimeNotificationService _notifications;
        public RemindersController(AppDbContext context, RealtimeNotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }
        private int UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private Task NotifyReminderChangedAsync(int userId, string message = "Reminders updated.") =>
            _notifications.NotifyDataChangedAsync(userId, "reminders", new[] { "/", "/Reminders", "/Task" }, message, "/Reminders/Index");

        public async Task<IActionResult> Index(int? taskId)
        {
            var userId = UserId();
            ViewBag.Tasks = await _context.TaskItems.Where(t => t.UserId == userId && !t.IsCompleted && !t.IsDeleted && !t.IsArchived && t.Deadline.HasValue).OrderBy(t => t.Deadline).ToListAsync();
            ViewBag.Error = TempData["ReminderError"];
            ViewBag.SelectedTaskId = taskId;
            var reminders = await _context.Reminders.Include(r => r.TaskItem).Where(r => r.TaskItem != null && r.TaskItem.UserId == userId).OrderBy(r => r.IsSent).ThenBy(r => r.RemindAt).ToListAsync();
            return View(reminders);
        }

        [HttpGet]
        public async Task<IActionResult> Due()
        {
            var userId = UserId();
            var now = DateTime.Now;
            var reminders = await _context.Reminders
                .Include(r => r.TaskItem)
                .Where(r => r.TaskItem != null
                    && r.TaskItem.UserId == userId
                    && !r.TaskItem.IsDeleted
                    && !r.TaskItem.IsArchived
                    && r.RemindAt <= now
                    && r.WebDismissedAt == null)
                .OrderBy(r => r.RemindAt)
                .Take(5)
                .Select(r => new
                {
                    id = r.Id,
                    message = r.Message,
                    remindAt = r.RemindAt.ToString("MM/dd/yyyy HH:mm"),
                    taskTitle = r.TaskItem!.Title,
                    taskId = r.TaskItemId
                })
                .ToListAsync();

            return Json(reminders);
        }

        [HttpPost]
        public async Task<IActionResult> DismissDue(int id)
        {
            var userId = UserId();
            var reminder = await _context.Reminders.Include(r => r.TaskItem).FirstOrDefaultAsync(r => r.Id == id && r.TaskItem != null && r.TaskItem.UserId == userId);
            if (reminder == null) return NotFound();

            reminder.WebDismissedAt = DateTime.Now;
            reminder.IsSent = true;
            await _context.SaveChangesAsync();
            await NotifyReminderChangedAsync(userId, "A reminder was dismissed.");
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reminder reminder)
        {
            var userId = UserId();
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == reminder.TaskItemId && t.UserId == userId && !t.IsDeleted && !t.IsArchived && t.Deadline.HasValue);
            if (task == null)
            {
                TempData["ReminderError"] = "Please choose a task with a deadline.";
                return RedirectToAction(nameof(Index), new { taskId = reminder.TaskItemId });
            }
            if (reminder.RemindAt >= task.Deadline!.Value)
            {
                TempData["ReminderError"] = "Remind At must be earlier than the task deadline.";
                return RedirectToAction(nameof(Index), new { taskId = reminder.TaskItemId });
            }
            if (reminder.RemindAt > DateTime.MinValue)
            {
                _context.Reminders.Add(reminder);
                await _context.SaveChangesAsync();
                await NotifyReminderChangedAsync(userId, "A reminder was created.");
            }
            return RedirectToAction(nameof(Index), new { taskId = reminder.TaskItemId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var userId = UserId();
            var reminder = await _context.Reminders.Include(r => r.TaskItem).FirstOrDefaultAsync(r => r.Id == id && r.TaskItem != null && r.TaskItem.UserId == userId);
            if (reminder != null)
            {
                reminder.IsSent = !reminder.IsSent;
                if (!reminder.IsSent)
                {
                    reminder.WebDismissedAt = null;
                }
                await _context.SaveChangesAsync();
                await NotifyReminderChangedAsync(userId, "A reminder was updated.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = UserId();
            var reminder = await _context.Reminders.Include(r => r.TaskItem).FirstOrDefaultAsync(r => r.Id == id && r.TaskItem != null && r.TaskItem.UserId == userId);
            if (reminder != null)
            {
                _context.Reminders.Remove(reminder);
                await _context.SaveChangesAsync();
                await NotifyReminderChangedAsync(userId, "A reminder was deleted.");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
