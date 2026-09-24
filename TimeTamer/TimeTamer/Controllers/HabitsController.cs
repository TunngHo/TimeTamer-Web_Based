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
    public class HabitsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RealtimeNotificationService _notifications;
        public HabitsController(AppDbContext context, RealtimeNotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }
        private int UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private Task NotifyHabitChangedAsync(int userId, string message = "Habits updated.") =>
            _notifications.NotifyDataChangedAsync(userId, "habits", new[] { "/", "/Habits" }, message, "/Habits/Index");

        public async Task<IActionResult> Index()
        {
            var userId = UserId();
            var habits = await _context.Habits.Include(h => h.CheckIns).Where(h => h.UserId == userId && !h.IsArchived).OrderBy(h => h.Name).ToListAsync();
            return View(habits);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Habit habit)
        {
            habit.UserId = UserId();
            habit.CreatedAt = DateTime.Now;
            ModelState.Remove("User");
            ModelState.Remove("CheckIns");
            if (ModelState.IsValid)
            {
                _context.Habits.Add(habit);
                await _context.SaveChangesAsync();
                await NotifyHabitChangedAsync(habit.UserId, "A habit was created.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(int id, string? notes)
        {
            var userId = UserId();
            var habit = await _context.Habits.Include(h => h.CheckIns).FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
            if (habit != null && !habit.CheckIns.Any(c => c.CheckDate.Date == DateTime.Today))
            {
                habit.CheckIns.Add(new HabitCheckIn { CheckDate = DateTime.Today, Notes = notes });
                await _context.SaveChangesAsync();
                await NotifyHabitChangedAsync(userId, "A habit was checked in.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var habit = await _context.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == UserId());
            if (habit != null)
            {
                habit.IsArchived = true;
                await _context.SaveChangesAsync();
                await NotifyHabitChangedAsync(habit.UserId, "A habit was archived.");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
