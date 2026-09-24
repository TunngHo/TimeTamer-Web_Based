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
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RealtimeNotificationService _notifications;
        private readonly IEmailSender _emailSender;

        public AdminController(AppDbContext context, RealtimeNotificationService notifications, IEmailSender emailSender)
        {
            _context = context;
            _notifications = notifications;
            _emailSender = emailSender;
        }

        private Task NotifyAdminDataChangedAsync(IEnumerable<int> userIds, string message = "Admin data updated.") =>
            _notifications.NotifyDataChangedAsync(userIds, "admin", new[] { "/Admin", "/Profile", "/Messages", "/Achievements" }, message, "/Admin/Index");

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            if (!IsAdmin()) return Forbid();
            var adminId = CurrentUserId();

            var userIds = await _context.AdminUserLinks
                .Where(l => l.AdminId == adminId && l.Status == "Accepted")
                .Select(l => l.UserId)
                .ToListAsync();

            var users = await _context.Users
                .Include(u => u.Tasks)
                .Include(u => u.Achievements)
                .Where(u => userIds.Contains(u.Id))
                .OrderBy(u => u.Username)
                .ToListAsync();

            var unreadCounts = await _context.AdminMessages
                .Where(m => userIds.Contains(m.UserId) && !m.IsFromAdmin && m.ReadAt == null)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            ViewBag.AdminCode = await _context.Users.Where(u => u.Id == adminId).Select(u => u.AdminCode).FirstOrDefaultAsync();
            ViewBag.PendingRequests = await _context.AdminUserLinks
                .Include(l => l.User)
                .Where(l => l.AdminId == adminId && l.Status != "Accepted")
                .OrderByDescending(l => l.RequestedAt)
                .ToListAsync();

            var summaries = users.Select(user => BuildSummary(user, unreadCounts.GetValueOrDefault(user.Id))).ToList();
            var pagedUsers = PaginatedList<AdminUserSummaryViewModel>.Create(summaries, page, pageSize);
            var model = new AdminDashboardViewModel
            {
                TotalUsers = users.Count,
                SuspendedUsers = users.Count(u => u.IsSuspended),
                TotalTasks = summaries.Sum(u => u.TotalTasks),
                CompletedTasks = summaries.Sum(u => u.CompletedTasks),
                UnreadUserMessages = summaries.Sum(u => u.UnreadMessages),
                Users = pagedUsers
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestUserAccess(string userIdentifier)
        {
            if (!IsAdmin()) return Forbid();
            var adminId = CurrentUserId();
            userIdentifier = (userIdentifier ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(userIdentifier))
            {
                TempData["AdminError"] = "Please enter a user code or registered email.";
                return RedirectToAction(nameof(Index));
            }

            var normalizedCode = userIdentifier.ToUpperInvariant();
            var normalizedEmail = userIdentifier.ToLowerInvariant();
            var target = await _context.Users.FirstOrDefaultAsync(u =>
                u.UserCode == normalizedCode ||
                (u.Email != null && u.Email.ToLower() == normalizedEmail));
            if (target == null)
            {
                TempData["AdminError"] = "User code or registered email was not found.";
                return RedirectToAction(nameof(Index));
            }

            if (target.Id == adminId)
            {
                TempData["AdminError"] = "You cannot request access to your own account.";
                return RedirectToAction(nameof(Index));
            }

            var existing = await _context.AdminUserLinks.FirstOrDefaultAsync(l => l.AdminId == adminId && l.UserId == target.Id);
            AdminUserLink requestLink;
            if (existing == null)
            {
                requestLink = new AdminUserLink { AdminId = adminId, UserId = target.Id, Status = "Pending" };
                _context.AdminUserLinks.Add(requestLink);
            }
            else
            {
                existing.Status = "Pending";
                existing.RequestedAt = DateTime.Now;
                existing.RespondedAt = null;
                requestLink = existing;
            }

            await _context.SaveChangesAsync();
            var adminName = User.FindFirstValue("FullName") ?? User.Identity?.Name ?? "Admin";
            await _notifications.NotifyUserAsync(target.Id, new { id = requestLink.Id, type = "adminRequest", title = "Admin access request", message = $"{adminName} wants to manage your TimeTamer account.", time = requestLink.RequestedAt.ToString("MM/dd/yyyy HH:mm"), url = "/Profile/Index", icon = "bi-shield-lock-fill" });
            await NotifyAdminDataChangedAsync(new[] { adminId, target.Id }, "Admin access request was updated.");
            TempData["AdminSuccess"] = "Request sent. The user will see it in their profile.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> UserDetails(int id)
        {
            if (!IsAdmin()) return Forbid();
            if (!await CanManageUser(id)) return Forbid();

            var user = await _context.Users
                .Include(u => u.Tasks)
                .Include(u => u.Achievements)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var unread = await _context.AdminMessages.CountAsync(m => m.UserId == id && !m.IsFromAdmin && m.ReadAt == null);
            var incoming = await _context.AdminMessages.Where(m => m.UserId == id && !m.IsFromAdmin && m.ReadAt == null).ToListAsync();
            foreach (var message in incoming) message.ReadAt = DateTime.Now;
            if (incoming.Count > 0) await _context.SaveChangesAsync();

            var model = new AdminUserDetailViewModel
            {
                Summary = BuildSummary(user, unread),
                RecentTasks = user.Tasks
                    .Where(t => !t.IsDeleted)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(8)
                    .ToList(),
                Messages = await _context.AdminMessages
                    .Include(m => m.SenderUser)
                    .Where(m => m.UserId == id)
                    .OrderByDescending(m => m.SentAt)
                    .Take(30)
                    .ToListAsync(),
                Achievements = await _context.UserAchievements
                    .Include(a => a.AwardedByAdmin)
                    .Where(a => a.UserId == id)
                    .OrderByDescending(a => a.AwardedAt)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int userId, string subject, string body)
        {
            if (!IsAdmin()) return Forbid();
            if (!await CanManageUser(userId)) return Forbid();
            if (string.IsNullOrWhiteSpace(body)) return RedirectToAction(nameof(UserDetails), new { id = userId });

            var message = new AdminMessage
            {
                UserId = userId,
                SenderUserId = CurrentUserId(),
                IsFromAdmin = true,
                Subject = "Admin message",
                Body = body.Trim()
            };
            _context.AdminMessages.Add(message);
            await _context.SaveChangesAsync();
            await _notifications.NotifyUserAsync(userId, new { id = message.Id, type = "message", title = "New admin message", message = message.Body, time = message.SentAt.ToString("MM/dd/yyyy HH:mm"), url = "/Messages/Index", icon = "bi-chat-dots-fill", dismissUrl = "/Messages/DismissNotification" });
            await NotifyAdminDataChangedAsync(new[] { CurrentUserId(), userId }, "A new admin message was sent.");
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AwardAchievement(int userId, string title, string? description, string icon = "trophy")
        {
            if (!IsAdmin()) return Forbid();
            if (!await CanManageUser(userId)) return Forbid();
            if (string.IsNullOrWhiteSpace(title)) return RedirectToAction(nameof(UserDetails), new { id = userId });

            var achievement = new UserAchievement
            {
                UserId = userId,
                AwardedByAdminId = CurrentUserId(),
                Title = title.Trim(),
                Description = description?.Trim(),
                Icon = string.IsNullOrWhiteSpace(icon) ? "trophy" : icon.Trim()
            };
            _context.UserAchievements.Add(achievement);
            await _context.SaveChangesAsync();
            await _notifications.NotifyUserAsync(userId, new
            {
                id = $"achievement-{achievement.Id}",
                type = "message",
                title = "New achievement",
                message = $"You received '{achievement.Title}' from your admin.",
                time = achievement.AwardedAt.ToString("MM/dd/yyyy HH:mm"),
                url = "/Achievements/Index",
                icon = "bi-award-fill"
            });
            await NotifyAdminDataChangedAsync(new[] { CurrentUserId(), userId }, "An achievement was awarded.");
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAchievement(int id, int userId)
        {
            if (!IsAdmin()) return Forbid();
            if (!await CanManageUser(userId)) return Forbid();
            var achievement = await _context.UserAchievements.FindAsync(id);
            if (achievement != null && achievement.UserId == userId)
            {
                _context.UserAchievements.Remove(achievement);
                await _context.SaveChangesAsync();
                await NotifyAdminDataChangedAsync(new[] { CurrentUserId(), userId }, "An achievement was removed.");
            }
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSuspended(int id)
        {
            if (!IsAdmin()) return Forbid();
            if (!await CanManageUser(id)) return Forbid();
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsSuspended = !user.IsSuspended;
            await _context.SaveChangesAsync();
            await NotifyAdminDataChangedAsync(new[] { CurrentUserId(), id }, "User status was updated.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUser(int id)
        {
            if (!IsAdmin()) return Forbid();
            var adminId = CurrentUserId();
            var link = await _context.AdminUserLinks
                .Include(l => l.User)
                .Include(l => l.Admin)
                .FirstOrDefaultAsync(l => l.AdminId == adminId && l.UserId == id && l.Status == "Accepted");
            if (link != null)
            {
                var userEmail = link.User?.Email;
                var userName = string.IsNullOrWhiteSpace(link.User?.FullName) ? link.User?.Username : link.User?.FullName;
                var adminName = string.IsNullOrWhiteSpace(link.Admin?.FullName) ? link.Admin?.Username : link.Admin?.FullName;

                _context.AdminUserLinks.Remove(link);
                await _context.SaveChangesAsync();
                await NotifyAdminDataChangedAsync(new[] { adminId, id }, "Admin access was removed.");

                if (!string.IsNullOrWhiteSpace(userEmail))
                {
                    await _emailSender.SendAsync(
                        userEmail,
                        "TimeTamer admin access removed",
                        $@"<div style='font-family:Arial,sans-serif;line-height:1.6;color:#172033'>
                            <h2>Admin access removed</h2>
                            <p>Hello <strong>{System.Net.WebUtility.HtmlEncode(userName ?? "User")}</strong>,</p>
                            <p><strong>{System.Net.WebUtility.HtmlEncode(adminName ?? "Admin")}</strong> has removed their admin management access to your TimeTamer account.</p>
                            <p>They can no longer manage your tasks, messages, achievements, or productivity view unless you approve a new request.</p>
                        </div>");
                }
                TempData["AdminSuccess"] = "User access removed and notification email was sent.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleAdmin(int id) => RedirectToAction(nameof(Index));

        private async Task<bool> CanManageUser(int userId)
        {
            var adminId = CurrentUserId();
            return await _context.AdminUserLinks.AnyAsync(l => l.AdminId == adminId && l.UserId == userId && l.Status == "Accepted");
        }

        private AdminUserSummaryViewModel BuildSummary(User user, int unreadMessages)
        {
            var activeTasks = user.Tasks.Where(t => !t.IsDeleted && !t.IsArchived).ToList();
            var completed = activeTasks.Count(t => t.IsCompleted || string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            var total = activeTasks.Count;
            return new AdminUserSummaryViewModel
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName,
                IsAdmin = user.IsAdmin,
                IsSuspended = user.IsSuspended,
                TotalTasks = total,
                OpenTasks = activeTasks.Count(t => !(t.IsCompleted || string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase))),
                CompletedTasks = completed,
                OverdueTasks = activeTasks.Count(t => !(t.IsCompleted || string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase)) && t.Deadline.HasValue && t.Deadline.Value < DateTime.Now),
                AchievementCount = user.Achievements.Count,
                UnreadMessages = unreadMessages,
                CompletionRate = total == 0 ? 0 : Math.Round(completed * 100.0 / total, 1),
                LastTaskCreatedAt = activeTasks.OrderByDescending(t => t.CreatedAt).FirstOrDefault()?.CreatedAt
            };
        }

        private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private bool IsAdmin() => User.HasClaim("IsAdmin", "true") || User.IsInRole("Admin");
    }
}







