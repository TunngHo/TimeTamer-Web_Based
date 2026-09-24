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
    public class GoalsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RealtimeNotificationService _notifications;
        public GoalsController(AppDbContext context, RealtimeNotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }
        private int UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private Task NotifyGoalChangedAsync(int userId, string message = "Goals updated.") =>
            _notifications.NotifyDataChangedAsync(userId, "goals", new[] { "/", "/Goals", "/Task", "/Calendar" }, message, "/Goals/Index");

        public async Task<IActionResult> Index()
        {
            var userId = UserId();
            var goals = await _context.Goals.Include(g => g.Tasks).Where(g => g.UserId == userId).OrderBy(g => g.IsCompleted).ThenBy(g => g.TargetDate).ToListAsync();
            return View(goals);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Goal goal)
        {
            goal.UserId = UserId();
            goal.CreatedAt = DateTime.Now;
            goal.TaskType = string.IsNullOrWhiteSpace(goal.TaskType) ? "Work" : goal.TaskType;
            ModelState.Remove("User");
            ModelState.Remove("Tasks");
            if (ModelState.IsValid)
            {
                _context.Goals.Add(goal);
                await _context.SaveChangesAsync();
                await NotifyGoalChangedAsync(goal.UserId, "A goal was created.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == UserId());
            if (goal != null)
            {
                goal.IsCompleted = !goal.IsCompleted;
                await _context.SaveChangesAsync();
                await NotifyGoalChangedAsync(goal.UserId, "A goal was updated.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == UserId());
            if (goal != null)
            {
                _context.Goals.Remove(goal);
                await _context.SaveChangesAsync();
                await NotifyGoalChangedAsync(goal.UserId, "A goal was deleted.");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}

