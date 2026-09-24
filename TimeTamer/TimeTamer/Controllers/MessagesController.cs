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
    public class MessagesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RealtimeNotificationService _notifications;

        public MessagesController(AppDbContext context, RealtimeNotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        private Task NotifyMessagesChangedAsync(IEnumerable<int> userIds, string message = "Messages updated.") =>
            _notifications.NotifyDataChangedAsync(userIds, "messages", new[] { "/Messages", "/Admin", "/Profile" }, message, "/Messages/Index");

        [HttpGet("/Messages")]
        [HttpGet("/Messages/Index")]
        public async Task<IActionResult> Index(int? userId)
        {
            var currentUserId = CurrentUserId();
            var isAdmin = IsAdmin();
            ViewBag.IsAdmin = isAdmin;

            if (isAdmin)
            {
                var managedUsers = await _context.AdminUserLinks
                    .Include(l => l.User)
                    .Where(l => l.AdminId == currentUserId && l.Status == "Accepted")
                    .OrderBy(l => l.User!.FullName ?? l.User!.Username)
                    .ToListAsync();
                ViewBag.ManagedUsers = managedUsers;

                var selectedUserId = userId ?? managedUsers.FirstOrDefault()?.UserId;
                ViewBag.SelectedUserId = selectedUserId;
                if (selectedUserId.HasValue && !managedUsers.Any(l => l.UserId == selectedUserId.Value)) return Forbid();

                var incoming = selectedUserId.HasValue
                    ? await _context.AdminMessages.Where(m => m.UserId == selectedUserId.Value && !m.IsFromAdmin && m.ReadAt == null).ToListAsync()
                    : new List<AdminMessage>();
                foreach (var message in incoming) message.ReadAt = DateTime.Now;
                if (incoming.Count > 0) await _context.SaveChangesAsync();

                return View(new UserMessageCenterViewModel
                {
                    Messages = selectedUserId.HasValue
                        ? await _context.AdminMessages.Include(m => m.SenderUser).Where(m => m.UserId == selectedUserId.Value).OrderByDescending(m => m.SentAt).Take(50).ToListAsync()
                        : new List<AdminMessage>(),
                    Achievements = new List<UserAchievement>()
                });
            }

            ViewBag.HasApprovedAdmin = await _context.AdminUserLinks.AnyAsync(l => l.UserId == currentUserId && l.Status == "Accepted");
            var incomingUser = await _context.AdminMessages.Where(m => m.UserId == currentUserId && m.IsFromAdmin && m.ReadAt == null).ToListAsync();
            foreach (var message in incomingUser) message.ReadAt = DateTime.Now;
            if (incomingUser.Count > 0) await _context.SaveChangesAsync();

            var model = new UserMessageCenterViewModel
            {
                Messages = await _context.AdminMessages
                    .Include(m => m.SenderUser)
                    .Where(m => m.UserId == currentUserId)
                    .OrderByDescending(m => m.SentAt)
                    .Take(50)
                    .ToListAsync(),
                Achievements = await _context.UserAchievements
                    .Include(a => a.AwardedByAdmin)
                    .Where(a => a.UserId == currentUserId)
                    .OrderByDescending(a => a.AwardedAt)
                    .Take(8)
                    .ToListAsync()
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> UnreadNotifications()
        {
            var userId = CurrentUserId();
            if (IsAdmin())
            {
                var managedUserIds = await _context.AdminUserLinks
                    .Where(l => l.AdminId == userId && l.Status == "Accepted")
                    .Select(l => l.UserId)
                    .ToListAsync();

                var adminMessages = await _context.AdminMessages
                    .Include(m => m.SenderUser)
                    .Where(m => managedUserIds.Contains(m.UserId) && !m.IsFromAdmin && m.ReadAt == null)
                    .OrderByDescending(m => m.SentAt)
                    .Take(5)
                    .ToListAsync();

                return Json(adminMessages.Select(m =>
                {
                    var senderName = string.IsNullOrWhiteSpace(m.SenderUser?.FullName) ? (m.SenderUser?.Username ?? "User") : m.SenderUser.FullName;
                    return new
                    {
                        id = m.Id,
                        title = "New user message",
                        message = $"{senderName}: {m.Body}",
                        sentAt = m.SentAt.ToString("MM/dd/yyyy HH:mm"),
                        url = $"/Messages/Index?userId={m.UserId}"
                    };
                }));
            }

            var messages = await _context.AdminMessages
                .Where(m => m.UserId == userId && m.IsFromAdmin && m.ReadAt == null)
                .OrderByDescending(m => m.SentAt)
                .Take(5)
                .Select(m => new
                {
                    id = m.Id,
                    title = m.Subject == "New task assigned" ? "New task assigned" : "New admin message",
                    message = m.Body,
                    sentAt = m.SentAt.ToString("MM/dd/yyyy HH:mm"),
                    url = m.Subject == "New task assigned" ? "/Task/Index" : "/Messages/Index"
                })
                .ToListAsync();

            return Json(messages);
        }

        [HttpPost]
        public async Task<IActionResult> DismissNotification(int id)
        {
            var userId = CurrentUserId();
            AdminMessage? message;
            if (IsAdmin())
            {
                var managedUserIds = await _context.AdminUserLinks
                    .Where(l => l.AdminId == userId && l.Status == "Accepted")
                    .Select(l => l.UserId)
                    .ToListAsync();
                message = await _context.AdminMessages.FirstOrDefaultAsync(m => m.Id == id && managedUserIds.Contains(m.UserId) && !m.IsFromAdmin);
            }
            else
            {
                message = await _context.AdminMessages.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId && m.IsFromAdmin);
            }

            if (message != null)
            {
                message.ReadAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await NotifyMessagesChangedAsync(new[] { userId }, "A message was read.");
            }
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int? targetUserId, string body)
        {
            var currentUserId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(body)) return RedirectToAction(nameof(Index), new { userId = targetUserId });

            if (IsAdmin())
            {
                if (!targetUserId.HasValue) return RedirectToAction(nameof(Index));
                var canManage = await _context.AdminUserLinks.AnyAsync(l => l.AdminId == currentUserId && l.UserId == targetUserId.Value && l.Status == "Accepted");
                if (!canManage) return Forbid();

                var message = new AdminMessage
                {
                    UserId = targetUserId.Value,
                    SenderUserId = currentUserId,
                    IsFromAdmin = true,
                    Subject = "Admin message",
                    Body = body.Trim()
                };
                _context.AdminMessages.Add(message);
                await _context.SaveChangesAsync();
                await _notifications.NotifyUserAsync(targetUserId.Value, new { id = message.Id, type = "message", title = "New admin message", message = message.Body, time = message.SentAt.ToString("MM/dd/yyyy HH:mm"), url = "/Messages/Index", icon = "bi-chat-dots-fill", dismissUrl = "/Messages/DismissNotification" });
                await NotifyMessagesChangedAsync(new[] { currentUserId, targetUserId.Value }, "A new message was sent.");
                return RedirectToAction(nameof(Index), new { userId = targetUserId.Value });
            }

            var hasApprovedAdmin = await _context.AdminUserLinks.AnyAsync(l => l.UserId == currentUserId && l.Status == "Accepted");
            if (!hasApprovedAdmin)
            {
                TempData["MessageError"] = "You can send messages after approving at least one admin.";
                return RedirectToAction(nameof(Index));
            }
            var userMessage = new AdminMessage
            {
                UserId = currentUserId,
                SenderUserId = currentUserId,
                IsFromAdmin = false,
                Subject = "User message",
                Body = body.Trim()
            };
            _context.AdminMessages.Add(userMessage);
            await _context.SaveChangesAsync();

            var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
            var senderName = string.IsNullOrWhiteSpace(sender?.FullName) ? sender?.Username : sender?.FullName;
            var adminIds = await _context.AdminUserLinks.Where(l => l.UserId == currentUserId && l.Status == "Accepted").Select(l => l.AdminId).ToListAsync();
            foreach (var adminId in adminIds)
            {
                await _notifications.NotifyUserAsync(adminId, new { id = userMessage.Id, type = "message", title = "New user message", message = $"{senderName}: {userMessage.Body}", time = userMessage.SentAt.ToString("MM/dd/yyyy HH:mm"), url = $"/Messages/Index?userId={currentUserId}", icon = "bi-chat-dots-fill" });
            }
            await NotifyMessagesChangedAsync(adminIds.Append(currentUserId), "A new message was sent.");

            return RedirectToAction(nameof(Index));
        }

        private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private bool IsAdmin() => User.HasClaim("IsAdmin", "true") || User.IsInRole("Admin");
    }
}







