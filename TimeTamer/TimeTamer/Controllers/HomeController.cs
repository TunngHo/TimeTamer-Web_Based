using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTamer.Data;
using TimeTamer.Models;

namespace TimeTamer.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        public HomeController(AppDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return RedirectToAction("Login", "Account");

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var weekEnd = today.AddDays(7);
            var tasks = await _context.TaskItems.Include(t => t.Category).Where(t => t.UserId == user.Id && !t.IsDeleted && !t.IsArchived).ToListAsync();

            ViewBag.DisplayName = user.FullName ?? user.Username;
            ViewBag.TaskCount = tasks.Count(t => !t.IsCompleted);
            ViewBag.CompletedCount = tasks.Count(t => t.IsCompleted);
            ViewBag.TodayCount = tasks.Count(t => !t.IsCompleted && ((t.Deadline >= today && t.Deadline < tomorrow) || (t.StartTime >= today && t.StartTime < tomorrow)));
            ViewBag.OverdueCount = tasks.Count(t => !t.IsCompleted && t.Deadline.HasValue && t.Deadline.Value < DateTime.Now);
            ViewBag.UpcomingCount = tasks.Count(t => !t.IsCompleted && t.Deadline.HasValue && t.Deadline.Value >= DateTime.Now && t.Deadline.Value <= weekEnd);
            ViewBag.Avatar = user.AvatarUrl ?? "https://via.placeholder.com/150";
            ViewBag.TypeStats = tasks.Where(t => t.Category != null).GroupBy(t => t.Category!.TaskType).Select(g => new { Type = g.Key, Count = g.Count() }).ToList();
            ViewBag.NextTasks = tasks.Where(t => !t.IsCompleted).OrderBy(t => t.Deadline ?? DateTime.MaxValue).Take(5).ToList();
            ViewBag.Goals = await _context.Goals.Include(g => g.Tasks).Where(g => g.UserId == user.Id && !g.IsCompleted).OrderBy(g => g.TargetDate).Take(4).ToListAsync();
            ViewBag.Reminders = await _context.Reminders.Include(r => r.TaskItem).Where(r => r.TaskItem != null && r.TaskItem.UserId == user.Id && !r.IsSent && r.RemindAt >= DateTime.Now).OrderBy(r => r.RemindAt).Take(4).ToListAsync();
            ViewBag.HabitDoneToday = await _context.HabitCheckIns.CountAsync(c => c.Habit != null && c.Habit.UserId == user.Id && c.CheckDate.Date == today);
            ViewBag.HabitTotal = await _context.Habits.CountAsync(h => h.UserId == user.Id && !h.IsArchived);

            return View();
        }

        [AllowAnonymous]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
