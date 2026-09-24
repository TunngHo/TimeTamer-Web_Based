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
    public class SubTasksController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RealtimeNotificationService _notifications;

        public SubTasksController(AppDbContext context, RealtimeNotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        private int GetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdString, out int userId) ? userId : 0;
        }

        private async Task<string> GetUserDisplayNameAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            return string.IsNullOrWhiteSpace(user?.FullName) ? (user?.Username ?? "Unknown user") : user.FullName;
        }

        private async Task<List<TaskItem>> GetSharedTaskCopiesAsync(TaskItem task)
        {
            if (string.IsNullOrWhiteSpace(task.SharedTaskGroupId)) return new List<TaskItem> { task };
            return await _context.TaskItems
                .Where(t => t.SharedTaskGroupId == task.SharedTaskGroupId && !t.IsDeleted && !t.IsArchived)
                .ToListAsync();
        }

        private Task NotifySubTaskDataChangedAsync(IEnumerable<int> userIds, string message = "Subtasks updated.")
        {
            return _notifications.NotifyDataChangedAsync(
                userIds,
                "subtasks",
                new[] { "/", "/Task", "/SubTasks", "/Calendar", "/Goals" },
                message,
                "/Task/Index");
        }

        private async Task<List<int>> GetSubTaskParticipantIdsAsync(TaskItem task)
        {
            var tasks = await GetSharedTaskCopiesAsync(task);
            return tasks.Select(t => t.UserId).Distinct().ToList();
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 5)
        {
            int userId = GetCurrentUserId();

            var allSubTasks = _context.SubTasks
                .Include(s => s.TaskItem)
                .Where(s => s.TaskItem != null && s.TaskItem.UserId == userId)
                .OrderByDescending(s => s.CompletedAt ?? s.Deadline ?? DateTime.MinValue)
                .ThenByDescending(s => s.Id);

            ViewBag.PageSize = pageSize;
            return View(await PaginatedList<SubTask>.CreateAsync(allSubTasks, page, pageSize));
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? taskId)
        {
            if (taskId == null) return RedirectToAction("Create", "Task");

            int userId = GetCurrentUserId();
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return NotFound("Task not found");

            ViewBag.TaskTitle = task.Title;
            ViewBag.TaskId = taskId;
            ViewBag.IsSharedTask = !string.IsNullOrWhiteSpace(task.SharedTaskGroupId);
            ViewBag.SubTaskList = await _context.SubTasks.Where(s => s.TaskItemId == taskId).OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync();

            return View(new SubTask { TaskItemId = taskId.Value });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubTask model)
        {
            int userId = GetCurrentUserId();
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == model.TaskItemId && t.UserId == userId);
            if (task == null) return NotFound();

            if (ModelState.IsValid)
            {
                if (!model.Deadline.HasValue && model.EstimatedMinutes > 0)
                {
                    model.Deadline = DateTime.Now.AddMinutes(model.EstimatedMinutes);
                }

                var creatorName = await GetUserDisplayNameAsync(userId);
                var sharedGroupId = string.IsNullOrWhiteSpace(task.SharedTaskGroupId) ? null : Guid.NewGuid().ToString("N");
                var targetTasks = await GetSharedTaskCopiesAsync(task);
                foreach (var targetTask in targetTasks)
                {
                    _context.SubTasks.Add(new SubTask
                    {
                        Title = model.Title,
                        TaskItemId = targetTask.Id,
                        SortOrder = model.SortOrder,
                        EstimatedMinutes = model.EstimatedMinutes,
                        Deadline = model.Deadline,
                        CreatedByUserId = userId,
                        CreatedByName = creatorName,
                        SharedSubTaskGroupId = sharedGroupId
                    });
                }
                await _context.SaveChangesAsync();
                await NotifySubTaskDataChangedAsync(targetTasks.Select(t => t.UserId), "A subtask was created.");
                return RedirectToAction("Create", new { taskId = model.TaskItemId });
            }

            ViewBag.TaskId = model.TaskItemId;
            ViewBag.TaskTitle = _context.TaskItems.Find(model.TaskItemId)?.Title;
            ViewBag.SubTaskList = _context.SubTasks.Where(s => s.TaskItemId == model.TaskItemId).OrderBy(s => s.SortOrder).ToList();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleComplete(int id)
        {
            int userId = GetCurrentUserId();
            var subTask = await _context.SubTasks.Include(s => s.TaskItem).FirstOrDefaultAsync(s => s.Id == id);

            if (subTask != null && subTask.TaskItem?.UserId == userId)
            {
                var participantIds = await GetSubTaskParticipantIdsAsync(subTask.TaskItem);
                var nextCompleted = !subTask.IsCompleted;
                DateTime? nextCompletedAt = nextCompleted ? DateTime.Now : null;
                var siblings = string.IsNullOrWhiteSpace(subTask.SharedSubTaskGroupId)
                    ? new List<SubTask> { subTask }
                    : await _context.SubTasks.Where(s => s.SharedSubTaskGroupId == subTask.SharedSubTaskGroupId).ToListAsync();
                foreach (var item in siblings)
                {
                    item.IsCompleted = nextCompleted;
                    item.CompletedAt = nextCompletedAt;
                }
                await _context.SaveChangesAsync();
                await NotifySubTaskDataChangedAsync(participantIds, "A subtask was completed.");
            }

            string referer = Request.Headers["Referer"].ToString();
            return !string.IsNullOrEmpty(referer) ? Redirect(referer) : RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> DeleteFast(int id)
        {
            int userId = GetCurrentUserId();
            var subTask = await _context.SubTasks.Include(s => s.TaskItem).FirstOrDefaultAsync(s => s.Id == id);

            if (subTask != null && subTask.TaskItem?.UserId == userId)
            {
                if (string.IsNullOrWhiteSpace(subTask.SharedSubTaskGroupId))
                {
                    _context.SubTasks.Remove(subTask);
                }
                else
                {
                    _context.SubTasks.RemoveRange(_context.SubTasks.Where(s => s.SharedSubTaskGroupId == subTask.SharedSubTaskGroupId));
                }
                await _context.SaveChangesAsync();
                await NotifySubTaskDataChangedAsync(await GetSubTaskParticipantIdsAsync(subTask.TaskItem), "A subtask was deleted.");
            }

            string referer = Request.Headers["Referer"].ToString();
            return !string.IsNullOrEmpty(referer) ? Redirect(referer) : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleCompleteAjax(int id)
        {
            int userId = GetCurrentUserId();
            var subTask = await _context.SubTasks.Include(s => s.TaskItem).FirstOrDefaultAsync(s => s.Id == id);
            if (subTask == null || subTask.TaskItem?.UserId != userId) return Json(new { success = false, message = "Not found" });

            var participantIds = await GetSubTaskParticipantIdsAsync(subTask.TaskItem);
            var nextCompleted = !subTask.IsCompleted;
            DateTime? nextCompletedAt = nextCompleted ? DateTime.Now : null;
            var siblings = string.IsNullOrWhiteSpace(subTask.SharedSubTaskGroupId)
                ? new List<SubTask> { subTask }
                : await _context.SubTasks.Where(s => s.SharedSubTaskGroupId == subTask.SharedSubTaskGroupId).ToListAsync();
            foreach (var item in siblings)
            {
                item.IsCompleted = nextCompleted;
                item.CompletedAt = nextCompletedAt;
            }
            await _context.SaveChangesAsync();
            await NotifySubTaskDataChangedAsync(participantIds, "A subtask was completed.");

            return Json(new
            {
                success = true,
                isCompleted = nextCompleted,
                completedAt = nextCompletedAt?.ToString("yyyy-MM-ddTHH:mm:ss")
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditInlineAjax(int id, string newTitle, int estimatedMinutes, DateTime? deadline)
        {
            if (string.IsNullOrWhiteSpace(newTitle)) return Json(new { success = false, message = "Title cannot be empty" });

            int userId = GetCurrentUserId();
            var subTask = await _context.SubTasks.Include(s => s.TaskItem).FirstOrDefaultAsync(s => s.Id == id);
            if (subTask == null || subTask.TaskItem?.UserId != userId) return Json(new { success = false, message = "Not found" });

            var participantIds = await GetSubTaskParticipantIdsAsync(subTask.TaskItem);
            var nextMinutes = estimatedMinutes < 0 ? 0 : estimatedMinutes;
            var nextDeadline = deadline ?? (nextMinutes > 0 ? DateTime.Now.AddMinutes(nextMinutes) : null);
            var siblings = string.IsNullOrWhiteSpace(subTask.SharedSubTaskGroupId)
                ? new List<SubTask> { subTask }
                : await _context.SubTasks.Where(s => s.SharedSubTaskGroupId == subTask.SharedSubTaskGroupId).ToListAsync();
            foreach (var item in siblings)
            {
                item.Title = newTitle;
                item.EstimatedMinutes = nextMinutes;
                item.Deadline = nextDeadline;
            }

            await _context.SaveChangesAsync();
            await NotifySubTaskDataChangedAsync(participantIds, "A subtask was updated.");

            return Json(new
            {
                success = true,
                title = newTitle,
                minutes = nextMinutes,
                hasDeadline = nextDeadline.HasValue,
                deadlineStr = nextDeadline.HasValue ? nextDeadline.Value.ToString("HH:mm MM/dd/yyyy") : "",
                isLate = nextDeadline.HasValue && nextDeadline.Value < DateTime.Now && !subTask.IsCompleted
            });
        }

        [HttpPost]
        public async Task<IActionResult> ReorderAjax([FromBody] List<int> orderedIds)
        {
            if (orderedIds == null || !orderedIds.Any()) return Json(new { success = false });

            for (int i = 0; i < orderedIds.Count; i++)
            {
                var subTask = await _context.SubTasks.FindAsync(orderedIds[i]);
                if (subTask != null)
                {
                    if (string.IsNullOrWhiteSpace(subTask.SharedSubTaskGroupId))
                    {
                        subTask.SortOrder = i;
                    }
                    else
                    {
                        var siblings = await _context.SubTasks.Where(s => s.SharedSubTaskGroupId == subTask.SharedSubTaskGroupId).ToListAsync();
                        foreach (var item in siblings) item.SortOrder = i;
                    }
                }
            }

            await _context.SaveChangesAsync();
            await NotifySubTaskDataChangedAsync(new[] { GetCurrentUserId() }, "Subtasks were reordered.");
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteParentTaskAjax(int taskId)
        {
            int userId = GetCurrentUserId();
            var task = await _context.TaskItems.Include(t => t.Goal).FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return Json(new { success = false });

            task.IsCompleted = true;
            task.Status = "Completed";
            task.CompletedAt = DateTime.Now;
            if (task.Goal != null)
            {
                task.Goal.IsCompleted = true;
            }

            await _context.SaveChangesAsync();
            await NotifySubTaskDataChangedAsync(await GetSubTaskParticipantIdsAsync(task), "Parent task was completed.");
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetCompletionHistory(int taskId)
        {
            int userId = GetCurrentUserId();
            var exists = await _context.TaskItems.AnyAsync(t => t.Id == taskId && t.UserId == userId);
            if (!exists) return Json(Array.Empty<object>());

            var history = await _context.SubTasks
                .Where(s => s.TaskItemId == taskId && s.IsCompleted && s.CompletedAt.HasValue)
                .OrderByDescending(s => s.CompletedAt)
                .Select(s => new
                {
                    title = s.Title,
                    completedAt = s.CompletedAt!.Value.ToString("MM/dd/yyyy HH:mm"),
                    estimatedMinutes = s.EstimatedMinutes
                })
                .ToListAsync();

            return Json(history);
        }
    }
}

