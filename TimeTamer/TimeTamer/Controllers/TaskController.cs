using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TimeTamer.Data;
using TimeTamer.Models;
using TimeTamer.Services;

namespace TimeTamer.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly RealtimeNotificationService _notifications;

        public TaskController(AppDbContext context, IWebHostEnvironment webHostEnvironment, RealtimeNotificationService notifications)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _notifications = notifications;
        }

        private int GetCurrentUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(id, out int userId) ? userId : 0;
        }

        private bool IsAdmin() => User.HasClaim("IsAdmin", "true") || User.IsInRole("Admin");

        private async Task<string> GetUserDisplayNameAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            return string.IsNullOrWhiteSpace(user?.FullName) ? (user?.Username ?? "Unknown user") : user.FullName;
        }

        private async Task CompleteLinkedGoalsAsync(IEnumerable<TaskItem> tasks)
        {
            var goalIds = tasks.Where(t => t.GoalId.HasValue).Select(t => t.GoalId!.Value).Distinct().ToList();
            if (!goalIds.Any()) return;

            var goals = await _context.Goals.Where(g => goalIds.Contains(g.Id)).ToListAsync();
            foreach (var goal in goals)
            {
                goal.IsCompleted = true;
            }
        }

        private async Task LoadGoalOptionsAsync(string? taskType = null)
        {
            var userId = GetCurrentUserId();
            taskType = string.IsNullOrWhiteSpace(taskType) ? null : taskType;
            var query = _context.Goals.Where(g => g.UserId == userId && !g.IsCompleted);
            if (taskType != null)
            {
                query = taskType == "Work"
                    ? query.Where(g => g.TaskType == taskType || g.TaskType == "")
                    : query.Where(g => g.TaskType == taskType);
            }
            ViewBag.Goals = await query.OrderBy(g => g.Title).ToListAsync();
        }

        private async Task<Category> GetOrCreateCategoryAsync(int userId, string taskType)
        {
            taskType = string.IsNullOrWhiteSpace(taskType) ? "Work" : taskType;
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.UserId == userId && c.TaskType == taskType);
            if (category != null) return category;

            category = new Category
            {
                UserId = userId,
                TaskType = taskType,
                Name = taskType,
                ColorCode = taskType switch
                {
                    "Study" => "#0dcaf0",
                    "Hangout" => "#ffc107",
                    "Sport" => "#198754",
                    "Daily" => "#6c757d",
                    _ => "#0d6efd"
                }
            };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category;
        }

        private static List<string> ParseTaskImages(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return new List<string>();
            imageUrl = imageUrl.Trim();
            if (imageUrl.StartsWith("["))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<string>>(imageUrl)?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            return new List<string> { imageUrl };
        }

        private static string? SerializeTaskImages(IEnumerable<string> images)
        {
            var list = images.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (!list.Any()) return null;
            return list.Count == 1 ? list[0] : JsonSerializer.Serialize(list);
        }

        private async Task<List<string>> SaveTaskImagesAsync(IEnumerable<IFormFile>? imageFiles)
        {
            var saved = new List<string>();
            if (imageFiles == null) return saved;
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "tasks");
            Directory.CreateDirectory(uploadsFolder);

            foreach (var imageFile in imageFiles.Where(f => f != null && f.Length > 0))
            {
                var extension = Path.GetExtension(imageFile.FileName);
                if (!allowedExtensions.Contains(extension)) continue;
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                await using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);
                saved.Add($"/images/tasks/{fileName}");
            }

            return saved;
        }


        private static HashSet<int> ParseSharedCompletedUsers(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new HashSet<int>();
            try
            {
                return JsonSerializer.Deserialize<List<int>>(raw)?.ToHashSet() ?? new HashSet<int>();
            }
            catch
            {
                return new HashSet<int>();
            }
        }

        private static string SerializeSharedCompletedUsers(IEnumerable<int> userIds)
        {
            return JsonSerializer.Serialize(userIds.Distinct().OrderBy(id => id).ToList());
        }

        private async Task<int?> GetOrCreateAssignedGoalIdAsync(int targetUserId, int? sourceGoalId, string taskType)
        {
            if (!sourceGoalId.HasValue) return null;
            var sourceGoal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == sourceGoalId.Value);
            if (sourceGoal == null) return null;

            var targetGoal = await _context.Goals.FirstOrDefaultAsync(g =>
                g.UserId == targetUserId &&
                g.Title == sourceGoal.Title &&
                g.TaskType == sourceGoal.TaskType &&
                !g.IsCompleted);
            if (targetGoal != null) return targetGoal.Id;

            targetGoal = new Goal
            {
                UserId = targetUserId,
                Title = sourceGoal.Title,
                Description = sourceGoal.Description,
                TaskType = string.IsNullOrWhiteSpace(sourceGoal.TaskType) ? taskType : sourceGoal.TaskType,
                TargetDate = sourceGoal.TargetDate,
                IsCompleted = false,
                CreatedAt = DateTime.Now
            };
            _context.Goals.Add(targetGoal);
            await _context.SaveChangesAsync();
            return targetGoal.Id;
        }

        private async Task<string> BuildImplementerListAsync(IEnumerable<int> userIds)
        {
            var ids = userIds.Distinct().ToList();
            var users = await _context.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
            return string.Join(", ", users.Select(u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct());
        }

        private async Task NotifySharedTaskUsersAsync(IEnumerable<int> userIds, int actorUserId, string title, string message)
        {
            foreach (var userId in userIds.Distinct().Where(id => id != actorUserId))
            {
                await _notifications.NotifyUserAsync(userId, new
                {
                    id = Guid.NewGuid().ToString("N"),
                    type = "message",
                    title,
                    message,
                    time = DateTime.Now.ToString("MM/dd/yyyy HH:mm"),
                    url = "/Task/Index",
                    icon = "bi-check2-circle"
                });
            }
        }

        private async Task NotifySharedTaskConfirmationAsync(IEnumerable<TaskItem> groupTasks, IEnumerable<int> pendingUserIds, int actorUserId, string actorName, string taskTitle)
        {
            var pending = pendingUserIds.ToHashSet();
            foreach (var item in groupTasks.Where(t => t.UserId != actorUserId && pending.Contains(t.UserId)))
            {
                await _notifications.NotifyUserAsync(item.UserId, new
                {
                    id = $"shared-{item.Id}-{Guid.NewGuid():N}",
                    type = "sharedTaskConfirmation",
                    title = "Confirm shared task",
                    message = $"{actorName} marked '{taskTitle}' as completed. Is this task completed?",
                    time = DateTime.Now.ToString("MM/dd/yyyy HH:mm"),
                    taskId = item.Id,
                    icon = "bi-check2-circle"
                });
            }
        }

        private Task NotifyTaskDataChangedAsync(IEnumerable<int> userIds, string message = "Tasks updated.")
        {
            return _notifications.NotifyDataChangedAsync(
                userIds,
                "tasks",
                new[] { "/", "/Task", "/Calendar", "/Category", "/Goals", "/Reminders", "/Admin", "/Messages" },
                message,
                "/Task/Index");
        }

        private async Task<List<int>> GetTaskParticipantIdsAsync(TaskItem task)
        {
            if (string.IsNullOrWhiteSpace(task.SharedTaskGroupId)) return new List<int> { task.UserId };

            return await _context.TaskItems
                .Where(t => t.SharedTaskGroupId == task.SharedTaskGroupId && !t.IsDeleted && !t.IsArchived)
                .Select(t => t.UserId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<IActionResult> Index(string? type, string? q, string? status, int? priority, DateTime? from, DateTime? to, string? sort, string? scope, int page = 1, int pageSize = 5)
        {
            int userId = GetCurrentUserId();
            var query = _context.TaskItems.Include(t => t.Category).Include(t => t.Goal).Where(t => t.UserId == userId && !t.IsDeleted && !t.IsArchived);

            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            {
                ViewBag.FilterError = "From date cannot be later than To date.";
                from = null;
                to = null;
            }

            var today = DateTime.Today;
            if (scope == "open") query = query.Where(t => !t.IsCompleted);
            if (scope == "today") query = query.Where(t => !t.IsCompleted && ((t.Deadline >= today && t.Deadline < today.AddDays(1)) || (t.StartTime >= today && t.StartTime < today.AddDays(1))));
            if (scope == "overdue") query = query.Where(t => !t.IsCompleted && t.Deadline.HasValue && t.Deadline.Value < DateTime.Now);
            if (scope == "upcoming") query = query.Where(t => !t.IsCompleted && t.Deadline.HasValue && t.Deadline.Value >= DateTime.Now && t.Deadline.Value <= today.AddDays(7));

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(t => t.Category != null && t.Category.TaskType == type);
                ViewBag.CurrentFilter = type;
            }
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(t => t.Title.Contains(q) || (t.Description != null && t.Description.Contains(q)));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
            if (priority.HasValue) query = query.Where(t => t.PriorityLevel == priority.Value);
            if (from.HasValue) query = query.Where(t => (t.Deadline ?? t.StartTime ?? t.CreatedAt) >= from.Value.Date);
            if (to.HasValue) query = query.Where(t => (t.Deadline ?? t.StartTime ?? t.CreatedAt) < to.Value.Date.AddDays(1));

            query = sort switch
            {
                "deadline" => query.OrderBy(t => t.Deadline ?? DateTime.MaxValue),
                "priority" => query.OrderByDescending(t => t.PriorityLevel).ThenBy(t => t.Deadline),
                "status" => query.OrderBy(t => t.Status).ThenBy(t => t.Deadline),
                "title" => query.OrderBy(t => t.Title),
                _ => query.OrderByDescending(t => t.CreatedAt)
            };

            ViewBag.Query = q;
            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Sort = sort;
            ViewBag.Scope = scope;
            ViewBag.ManagedUsers = IsAdmin()
                ? await _context.AdminUserLinks.Include(l => l.User).Where(l => l.AdminId == userId && l.Status == "Accepted").OrderBy(l => l.User!.FullName ?? l.User!.Username).ToListAsync()
                : new List<AdminUserLink>();
            ViewBag.IsAdmin = IsAdmin();
            return View(await PaginatedList<TaskItem>.CreateAsync(query, page, pageSize));
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpGet]
        public async Task<IActionResult> CreateSpecific(string type, DateTime? date)
        {
            var selectedTaskType = string.IsNullOrWhiteSpace(type) ? "Work" : type;
            ViewBag.TaskType = selectedTaskType;
            if (date.HasValue)
            {
                ViewBag.DefaultStart = date.Value.ToString("yyyy-MM-ddT09:00");
                ViewBag.DefaultDeadline = date.Value.ToString("yyyy-MM-ddT17:00");
            }
            await LoadGoalOptionsAsync(selectedTaskType);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateSpecific(TaskItem task, IFormCollection form, string taskType, List<IFormFile>? imageFiles)
        {
            int userId = GetCurrentUserId();
            var category = await GetOrCreateCategoryAsync(userId, taskType);
            var metaDict = new Dictionary<string, string>();
            foreach (var key in form.Keys.Where(k => k.StartsWith("meta_"))) metaDict[key.Replace("meta_", "")] = form[key].ToString();
            if (task.GoalId.HasValue && !await _context.Goals.AnyAsync(g => g.Id == task.GoalId.Value && g.UserId == userId && (g.TaskType == taskType || (taskType == "Work" && g.TaskType == "")) && !g.IsCompleted))
            {
                task.GoalId = null;
            }

            task.Implementer = IsAdmin() && !string.IsNullOrWhiteSpace(task.Implementer)
                ? task.Implementer.Trim()
                : await GetUserDisplayNameAsync(userId);
            task.Metadata = JsonSerializer.Serialize(metaDict);
            task.UserId = userId;
            task.CategoryId = category.Id;
            task.Status = task.StartTime.HasValue ? "Pending" : (task.Deadline.HasValue ? "Process" : "Pending");
            task.ImageUrl = SerializeTaskImages(await SaveTaskImagesAsync(imageFiles));
            ModelState.Remove("Category");
            ModelState.Remove("Goal");
            ModelState.Remove("SubTasks");
            ModelState.Remove("imageFiles");
            ModelState.Remove("removeImages");
            ModelState.Remove("taskType");
            if (ModelState.IsValid)
            {
                _context.TaskItems.Add(task);
                await _context.SaveChangesAsync();
                await NotifyTaskDataChangedAsync(new[] { userId }, "A task was created.");
                return RedirectToAction(nameof(Index));
            }
            ViewBag.TaskType = taskType;
            await LoadGoalOptionsAsync(taskType);
            return View(task);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            var task = await _context.TaskItems.Include(t => t.Category).Include(t => t.Goal).FirstOrDefaultAsync(t => t.Id == id && t.UserId == GetCurrentUserId() && !t.IsDeleted);
            if (task != null && !IsAdmin() && !string.IsNullOrWhiteSpace(task.SharedTaskGroupId)) return Forbid();
            await LoadGoalOptionsAsync(task?.Category?.TaskType);
            return task == null ? NotFound() : View(task);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, TaskItem task, IFormCollection form, List<IFormFile>? imageFiles)
        {
            int userId = GetCurrentUserId();
            var existingTask = await _context.TaskItems.Include(t => t.Category).FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && !t.IsDeleted);
            if (existingTask == null) return NotFound();
            if (!IsAdmin() && !string.IsNullOrWhiteSpace(existingTask.SharedTaskGroupId)) return Forbid();
            var currentTaskType = existingTask.Category?.TaskType;
            if (task.GoalId.HasValue && !await _context.Goals.AnyAsync(g => g.Id == task.GoalId.Value && g.UserId == userId && (g.TaskType == currentTaskType || (currentTaskType == "Work" && g.TaskType == "")) && !g.IsCompleted))
            {
                task.GoalId = null;
            }

            existingTask.Title = task.Title;
            existingTask.Description = task.Description;
            if (IsAdmin())
            {
                existingTask.Implementer = string.IsNullOrWhiteSpace(task.Implementer) ? null : task.Implementer.Trim();
            }
            existingTask.Deadline = task.Deadline;
            existingTask.StartTime = task.StartTime;
            existingTask.PriorityLevel = task.PriorityLevel;
            existingTask.GoalId = task.GoalId;
            var existingImages = ParseTaskImages(existingTask.ImageUrl);
            var removeImages = form["removeImages"].ToHashSet(StringComparer.OrdinalIgnoreCase);
            existingImages = existingImages.Where(image => !removeImages.Contains(image)).ToList();
            existingImages.AddRange(await SaveTaskImagesAsync(imageFiles));
            existingTask.ImageUrl = SerializeTaskImages(existingImages);
            var metaDict = new Dictionary<string, string>();
            foreach (var key in form.Keys.Where(k => k.StartsWith("meta_"))) metaDict[key.Replace("meta_", "")] = form[key].ToString();
            existingTask.Metadata = JsonSerializer.Serialize(metaDict);
            if (IsAdmin() && !string.IsNullOrWhiteSpace(existingTask.SharedTaskGroupId))
            {
                var groupTasks = await _context.TaskItems
                    .Where(t => t.SharedTaskGroupId == existingTask.SharedTaskGroupId && t.Id != existingTask.Id && !t.IsDeleted && !t.IsArchived)
                    .ToListAsync();
                foreach (var groupTask in groupTasks)
                {
                    groupTask.Title = existingTask.Title;
                    groupTask.Description = existingTask.Description;
                    groupTask.Implementer = existingTask.Implementer;
                    groupTask.Deadline = existingTask.Deadline;
                    groupTask.StartTime = existingTask.StartTime;
                    groupTask.PriorityLevel = existingTask.PriorityLevel;
                    groupTask.GoalId = groupTask.UserId == existingTask.UserId
                        ? existingTask.GoalId
                        : await GetOrCreateAssignedGoalIdAsync(groupTask.UserId, existingTask.GoalId, currentTaskType ?? "Work");
                    groupTask.Metadata = existingTask.Metadata;
                    groupTask.ImageUrl = existingTask.ImageUrl;
                }
            }
            ModelState.Remove("Category");
            ModelState.Remove("Goal");
            ModelState.Remove("SubTasks");
            ModelState.Remove("imageFiles");
            ModelState.Remove("removeImages");
            if (ModelState.IsValid)
            {
                await _context.SaveChangesAsync();
                await NotifyTaskDataChangedAsync(await GetTaskParticipantIdsAsync(existingTask), "A task was updated.");
                return RedirectToAction(nameof(Index));
            }
            await LoadGoalOptionsAsync(existingTask.Category?.TaskType);
            return View(existingTask);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int id, int targetUserId)
        {
            var adminId = GetCurrentUserId();
            if (!IsAdmin()) return Forbid();

            var canManage = await _context.AdminUserLinks.AnyAsync(l => l.AdminId == adminId && l.UserId == targetUserId && l.Status == "Accepted");
            if (!canManage) return Forbid();

            var source = await _context.TaskItems.Include(t => t.Category).Include(t => t.Goal).FirstOrDefaultAsync(t => t.Id == id && t.UserId == adminId && !t.IsDeleted && !t.IsArchived);
            if (source == null) return NotFound();

            var taskType = source.Category?.TaskType ?? "Work";
            var duplicateExists = await _context.TaskItems.Include(t => t.Category).AnyAsync(t => t.UserId == targetUserId && !t.IsDeleted && !t.IsArchived && t.Title == source.Title && t.Category != null && t.Category.TaskType == taskType);
            if (duplicateExists)
            {
                TempData["TaskError"] = "This task already exists for the selected user.";
                return RedirectToAction(nameof(Index));
            }

            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
            if (targetUser == null) return NotFound();

            source.SharedTaskGroupId = string.IsNullOrWhiteSpace(source.SharedTaskGroupId) ? Guid.NewGuid().ToString("N") : source.SharedTaskGroupId;
            source.Status = "Process";
            source.IsCompleted = false;
            source.CompletedAt = null;
            source.SharedTaskCompletedByUserIds = SerializeSharedCompletedUsers(Array.Empty<int>());

            var groupTasks = await _context.TaskItems.Where(t => t.SharedTaskGroupId == source.SharedTaskGroupId && !t.IsDeleted && !t.IsArchived).ToListAsync();
            var participantIds = groupTasks.Select(t => t.UserId).Append(source.UserId).Append(targetUserId).Distinct().ToList();
            var implementerList = await BuildImplementerListAsync(participantIds);
            foreach (var groupTask in groupTasks)
            {
                groupTask.Implementer = implementerList;
                groupTask.Status = "Process";
                groupTask.IsCompleted = false;
                groupTask.CompletedAt = null;
                groupTask.SharedTaskCompletedByUserIds = SerializeSharedCompletedUsers(Array.Empty<int>());
            }
            source.Implementer = implementerList;

            var category = await GetOrCreateCategoryAsync(targetUserId, taskType);
            var assignedGoalId = await GetOrCreateAssignedGoalIdAsync(targetUserId, source.GoalId, taskType);
            var assigned = new TaskItem
            {
                Title = source.Title,
                Description = source.Description,
                Implementer = implementerList,
                SharedTaskGroupId = source.SharedTaskGroupId,
                SharedTaskCompletedByUserIds = SerializeSharedCompletedUsers(Array.Empty<int>()),
                CreatedAt = DateTime.Now,
                StartTime = source.StartTime,
                Deadline = source.Deadline,
                Status = "Process",
                PriorityLevel = source.PriorityLevel,
                IsCompleted = false,
                UserId = targetUserId,
                CategoryId = category.Id,
                GoalId = assignedGoalId,
                Metadata = source.Metadata,
                ImageUrl = source.ImageUrl
            };

            _context.TaskItems.Add(assigned);
            var assignmentMessage = new AdminMessage
            {
                UserId = targetUserId,
                SenderUserId = adminId,
                IsFromAdmin = true,
                Subject = "New task assigned",
                Body = $"A new shared task was assigned to you: {source.Title}"
            };
            _context.AdminMessages.Add(assignmentMessage);
            await _context.SaveChangesAsync();
            await _notifications.NotifyUserAsync(targetUserId, new { id = assignmentMessage.Id, type = "message", title = "New task assigned", message = assignmentMessage.Body, time = assignmentMessage.SentAt.ToString("MM/dd/yyyy HH:mm"), url = "/Task/Index", icon = "bi-send-check-fill", dismissUrl = "/Messages/DismissNotification" });
            await NotifyTaskDataChangedAsync(participantIds, "A shared task was assigned.");
            TempData["TaskSuccess"] = "Task assigned successfully.";
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> DeleteFast(int id)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == GetCurrentUserId());
            if (task != null)
            {
                task.IsDeleted = true;
                task.DeletedAt = DateTime.Now;
                task.IsArchived = false;
                task.ArchivedAt = null;
                await _context.SaveChangesAsync();
                await NotifyTaskDataChangedAsync(new[] { task.UserId }, "A task was moved to Trash.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == GetCurrentUserId() && !t.IsDeleted);
            if (task != null)
            {
                if (!task.IsCompleted && task.Status != "Completed")
                {
                    TempData["TaskError"] = "Only completed tasks can be moved to Achieve.";
                    return RedirectToAction(nameof(Index));
                }
                task.IsArchived = true;
                task.ArchivedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await NotifyTaskDataChangedAsync(new[] { task.UserId }, "A task was moved to Achieve.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Achieve(int id) => Archive(id);

        public async Task<IActionResult> ArchiveList(int page = 1, int pageSize = 5)
        {
            var userId = GetCurrentUserId();
            var tasks = _context.TaskItems
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.IsArchived && !t.IsDeleted)
                .OrderByDescending(t => t.ArchivedAt);
            ViewBag.PageSize = pageSize;
            return View(await PaginatedList<TaskItem>.CreateAsync(tasks, page, pageSize));
        }

        public Task<IActionResult> AchieveList() => ArchiveList();

        public async Task<IActionResult> Trash(int page = 1, int pageSize = 5)
        {
            var userId = GetCurrentUserId();
            var tasks = _context.TaskItems
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.IsDeleted)
                .OrderByDescending(t => t.DeletedAt);
            ViewBag.PageSize = pageSize;
            return View(await PaginatedList<TaskItem>.CreateAsync(tasks, page, pageSize));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == GetCurrentUserId());
            if (task != null)
            {
                task.IsDeleted = false;
                task.DeletedAt = null;
                task.IsArchived = false;
                task.ArchivedAt = null;
                await _context.SaveChangesAsync();
                await NotifyTaskDataChangedAsync(new[] { task.UserId }, "A task was restored.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentlyDelete(int id)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == GetCurrentUserId() && t.IsDeleted);
            if (task != null)
            {
                _context.TaskItems.Remove(task);
                await _context.SaveChangesAsync();
                await NotifyTaskDataChangedAsync(new[] { task.UserId }, "A task was permanently deleted.");
            }
            return RedirectToAction(nameof(Trash));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveOldCompleted()
        {
            var userId = GetCurrentUserId();
            var cutoff = DateTime.Now.AddDays(-30);
            var tasks = await _context.TaskItems.Where(t => t.UserId == userId && !t.IsDeleted && !t.IsArchived && t.IsCompleted && t.CompletedAt.HasValue && t.CompletedAt.Value < cutoff).ToListAsync();
            foreach (var task in tasks)
            {
                task.IsArchived = true;
                task.ArchivedAt = DateTime.Now;
            }
            await _context.SaveChangesAsync();
            await NotifyTaskDataChangedAsync(new[] { userId }, "Old completed tasks were archived.");
            return RedirectToAction(nameof(ArchiveList));
        }

        public async Task<IActionResult> Kanban()
        {
            int userId = GetCurrentUserId();
            var tasks = await _context.TaskItems.Include(t => t.Category).Include(t => t.Goal).Where(t => t.UserId == userId && !t.IsDeleted && !t.IsArchived).ToListAsync();
            ViewBag.IsAdmin = IsAdmin();
            return View(tasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateRecurring(int id)
        {
            int userId = GetCurrentUserId();
            var source = await _context.TaskItems.Include(t => t.Category).FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && !t.IsDeleted);
            if (source == null || string.IsNullOrEmpty(source.Metadata)) return RedirectToAction(nameof(Index));
            if (!IsAdmin() && !string.IsNullOrWhiteSpace(source.SharedTaskGroupId))
            {
                TempData["TaskError"] = "Only the admin can create recurring copies for assigned tasks.";
                return RedirectToAction(nameof(Index));
            }
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(source.Metadata) ?? new Dictionary<string, string>();
            if (!metadata.TryGetValue("Recurring", out var recurring) || recurring == "No") return RedirectToAction(nameof(Index));
            var offset = recurring == "Weekly" ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);
            var next = new TaskItem
            {
                Title = source.Title,
                Description = source.Description,
                StartTime = source.StartTime?.Add(offset),
                Deadline = source.Deadline?.Add(offset),
                PriorityLevel = source.PriorityLevel,
                Status = "Pending",
                UserId = userId,
                CategoryId = source.CategoryId,
                GoalId = source.GoalId,
                Metadata = source.Metadata,
                ImageUrl = source.ImageUrl
            };
            _context.TaskItems.Add(next);
            await _context.SaveChangesAsync();
            await NotifyTaskDataChangedAsync(new[] { userId }, "A recurring task was created.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus, DateTime? deadline, bool noDeadline = false, bool resetDeadline = false)
        {
            int userId = GetCurrentUserId();
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
            if (task == null || task.UserId != userId || task.IsDeleted || task.IsArchived) return BadRequest();
            var isSharedTask = !string.IsNullOrWhiteSpace(task.SharedTaskGroupId);
            if (!IsAdmin() && isSharedTask && task.Status == "Completed" && newStatus != "Completed") return BadRequest("Completed assigned tasks are locked.");
            if (newStatus == "Process" && !noDeadline && !deadline.HasValue) return BadRequest("Please choose a deadline or mark this task as no deadline.");
            if (resetDeadline && !noDeadline && !deadline.HasValue) return BadRequest("Please choose a new deadline or mark this task as no deadline.");

            if (string.IsNullOrWhiteSpace(task.SharedTaskGroupId))
            {
                if (newStatus == "Process")
                {
                    task.StartTime = DateTime.Now;
                    task.Deadline = noDeadline ? null : deadline;
                }
                else if (newStatus == "Pending" && resetDeadline)
                {
                    task.Deadline = noDeadline ? null : deadline;
                }
                task.Status = newStatus;
                task.IsCompleted = newStatus == "Completed";
                task.CompletedAt = task.IsCompleted ? DateTime.Now : null;
                if (task.IsCompleted) await CompleteLinkedGoalsAsync(new[] { task });
                await _context.SaveChangesAsync();
                await NotifyTaskDataChangedAsync(new[] { userId }, "Task status was updated.");
                return Ok();
            }

            var groupTasks = await _context.TaskItems
                .Where(t => t.SharedTaskGroupId == task.SharedTaskGroupId && !t.IsDeleted && !t.IsArchived)
                .ToListAsync();
            if (!groupTasks.Any()) return BadRequest();

            var participantIds = groupTasks.Select(t => t.UserId).Distinct().ToList();
            var users = await _context.Users.Where(u => participantIds.Contains(u.Id)).ToListAsync();
            var actor = users.FirstOrDefault(u => u.Id == userId);
            var actorName = string.IsNullOrWhiteSpace(actor?.FullName) ? (actor?.Username ?? "A member") : actor.FullName;
            var completedUsers = groupTasks.SelectMany(t => ParseSharedCompletedUsers(t.SharedTaskCompletedByUserIds)).ToHashSet();

            if (newStatus == "Completed")
            {
                completedUsers.Add(userId);
                var serialized = SerializeSharedCompletedUsers(completedUsers);
                var allConfirmed = participantIds.All(id => completedUsers.Contains(id));

                foreach (var item in groupTasks)
                {
                    item.SharedTaskCompletedByUserIds = serialized;
                    item.Status = allConfirmed ? "Completed" : "Process";
                    item.IsCompleted = allConfirmed;
                    item.CompletedAt = allConfirmed ? DateTime.Now : null;
                }

                if (allConfirmed)
                {
                    await CompleteLinkedGoalsAsync(groupTasks);
                    await NotifySharedTaskUsersAsync(participantIds, userId, "Shared task completed", $"All implementers confirmed that '{task.Title}' is completed.");
                }
                else
                {
                    await NotifySharedTaskConfirmationAsync(groupTasks, participantIds.Where(id => !completedUsers.Contains(id)), userId, actorName, task.Title);
                }
            }
            else
            {
                var previousCompletedUsers = completedUsers.ToList();
                completedUsers.Clear();
                var serialized = SerializeSharedCompletedUsers(completedUsers);
                foreach (var item in groupTasks)
                {
                    if (newStatus == "Process")
                    {
                        item.StartTime = DateTime.Now;
                        item.Deadline = noDeadline ? null : deadline;
                    }
                    else if (newStatus == "Pending" && resetDeadline)
                    {
                        item.Deadline = noDeadline ? null : deadline;
                    }
                    item.SharedTaskCompletedByUserIds = serialized;
                    item.Status = newStatus == "Pending" ? "Pending" : "Process";
                    item.IsCompleted = false;
                    item.CompletedAt = null;
                }

                var notifyIds = previousCompletedUsers.Where(id => id != userId).ToList();
                await NotifySharedTaskUsersAsync(notifyIds, userId, "Shared task returned to process", $"{actorName} marked '{task.Title}' as not completed yet. The shared task has been moved back to Process for everyone.");
            }

            await _context.SaveChangesAsync();
            await NotifyTaskDataChangedAsync(participantIds, "Shared task status was updated.");
            return Ok();
        }
    }
}







