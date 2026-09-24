using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IEmailSender _emailSender;
        private readonly RealtimeNotificationService _notifications;

        public ProfileController(AppDbContext context, IWebHostEnvironment webHostEnvironment, IEmailSender emailSender, RealtimeNotificationService notifications)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _emailSender = emailSender;
            _notifications = notifications;
        }

        private Task NotifyProfileDataChangedAsync(IEnumerable<int> userIds, string message = "Profile data updated.") =>
            _notifications.NotifyDataChangedAsync(userIds, "profile", new[] { "/Profile", "/Admin", "/Messages", "/Task" }, message, "/Profile/Index");

        private User? GetCurrentUser()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdString, out int userId)
                ? _context.Users.FirstOrDefault(u => u.Id == userId)
                : null;
        }

        private static string? RemoveNamesFromList(string? raw, IEnumerable<string> names)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            var remove = names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!remove.Any()) return raw;

            var remaining = raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(name => !remove.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return remaining.Any() ? string.Join(", ", remaining) : null;
        }

        private static string? RemoveUserIdFromJsonList(string? raw, int userId)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            try
            {
                var ids = JsonSerializer.Deserialize<List<int>>(raw) ?? new List<int>();
                ids = ids.Where(id => id != userId).Distinct().OrderBy(id => id).ToList();
                return ids.Any() ? JsonSerializer.Serialize(ids) : null;
            }
            catch
            {
                return raw;
            }
        }

        private void DeleteAvatarFile(string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl) || !avatarUrl.StartsWith("/images/avatars/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(avatarUrl);
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var path = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars", fileName);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        public async Task<IActionResult> Index()
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.PendingAdminRequests = await _context.AdminUserLinks
                .Include(l => l.Admin)
                .Where(l => l.UserId == user.Id && l.Status == "Pending")
                .OrderByDescending(l => l.RequestedAt)
                .ToListAsync();
            ViewBag.AcceptedAdmins = await _context.AdminUserLinks
                .Include(l => l.Admin)
                .Where(l => l.UserId == user.Id && l.Status == "Accepted")
                .OrderByDescending(l => l.RespondedAt)
                .ToListAsync();

            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> PendingAdminRequests()
        {
            var user = GetCurrentUser();
            if (user == null) return Unauthorized();

            var requests = await _context.AdminUserLinks
                .Include(l => l.Admin)
                .Where(l => l.UserId == user.Id && l.Status == "Pending")
                .OrderByDescending(l => l.RequestedAt)
                .Select(l => new
                {
                    id = l.Id,
                    adminName = string.IsNullOrWhiteSpace(l.Admin!.FullName) ? l.Admin.Username : l.Admin.FullName,
                    adminCode = l.Admin.AdminCode,
                    requestedAt = l.RequestedAt.ToString("MM/dd/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(requests);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondAdminRequest(int requestId, string decision)
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");

            var request = await _context.AdminUserLinks
                .Include(l => l.Admin)
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == requestId && l.UserId == user.Id && l.Status == "Pending");
            if (request == null) return RedirectToAction(nameof(Index));

            var accepted = string.Equals(decision, "Accept", StringComparison.OrdinalIgnoreCase);
            request.Status = accepted ? "Accepted" : "Declined";
            request.RespondedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            await NotifyProfileDataChangedAsync(new[] { user.Id, request.AdminId }, accepted ? "Admin request accepted." : "Admin request declined.");

            if (accepted)
            {
                var adminEmail = request.Admin?.Email;
                var userEmail = request.User?.Email;
                var adminName = request.Admin?.Username ?? "Admin";
                var userName = request.User?.Username ?? user.Username;

                if (!string.IsNullOrWhiteSpace(adminEmail))
                {
                    await _emailSender.SendAsync(adminEmail, "TimeTamer admin access approved", $"<p>User <strong>{userName}</strong> approved your admin management request.</p><p>You can now manage their tasks, messages, and achievements in TimeTamer.</p>");
                }

                if (!string.IsNullOrWhiteSpace(userEmail))
                {
                    await _emailSender.SendAsync(userEmail, "TimeTamer admin connected", $"<p>You approved <strong>{adminName}</strong> as your TimeTamer admin.</p><p>This admin can now manage your account productivity view, send messages, and award achievements.</p>");
                }
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public IActionResult Edit()
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");

            var model = new ProfileEditViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                CurrentAvatarUrl = user.AvatarUrl
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileEditViewModel model)
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                model.CurrentAvatarUrl = user.AvatarUrl;
                model.Email = user.Email;
                return View(model);
            }

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.DateOfBirth = model.DateOfBirth;

            if (model.AvatarFile != null)
            {
                var extension = Path.GetExtension(model.AvatarFile.FileName);
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                Directory.CreateDirectory(uploadsFolder);

                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await using var fileStream = new FileStream(filePath, FileMode.Create);
                await model.AvatarFile.CopyToAsync(fileStream);
                user.AvatarUrl = "/images/avatars/" + uniqueFileName;
            }

            _context.Update(user);
            await _context.SaveChangesAsync();
            await NotifyProfileDataChangedAsync(new[] { user.Id }, "Profile was updated.");
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult ChangePassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");
            if (!ModelState.IsValid) return View(model);

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.Password))
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect");
                return View(model);
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            _context.Update(user);
            await _context.SaveChangesAsync();
            await NotifyProfileDataChangedAsync(new[] { user.Id }, "Password was changed.");

            ViewBag.Success = "Password changed successfully.";
            return View();
        }

        [HttpGet]
        public IActionResult DeleteAccount() => View(new DeleteAccountViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(DeleteAccountViewModel model)
        {
            var user = GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Account");
            if (!ModelState.IsValid) return View(model);

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.Password))
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect");
                return View(model);
            }

            var taskIds = await _context.TaskItems.Where(t => t.UserId == user.Id).Select(t => t.Id).ToListAsync();
            var habitIds = await _context.Habits.Where(h => h.UserId == user.Id).Select(h => h.Id).ToListAsync();
            var userNames = new[] { user.FullName, user.Username, user.Email }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var linkedTasks = await _context.TaskItems
                .Where(t => t.UserId != user.Id && (!string.IsNullOrWhiteSpace(t.Implementer) || !string.IsNullOrWhiteSpace(t.SharedTaskCompletedByUserIds)))
                .ToListAsync();
            foreach (var task in linkedTasks)
            {
                task.Implementer = RemoveNamesFromList(task.Implementer, userNames);
                task.SharedTaskCompletedByUserIds = RemoveUserIdFromJsonList(task.SharedTaskCompletedByUserIds, user.Id);
            }

            var sharedSubTasks = await _context.SubTasks
                .Where(s => s.CreatedByUserId == user.Id && !taskIds.Contains(s.TaskItemId))
                .ToListAsync();
            foreach (var subTask in sharedSubTasks)
            {
                subTask.CreatedByUserId = null;
                subTask.CreatedByName = null;
            }

            _context.AdminUserLinks.RemoveRange(_context.AdminUserLinks.Where(l => l.UserId == user.Id || l.AdminId == user.Id));
            _context.AdminMessages.RemoveRange(_context.AdminMessages.Where(m => m.UserId == user.Id || m.SenderUserId == user.Id));
            _context.UserAchievements.RemoveRange(_context.UserAchievements.Where(a => a.UserId == user.Id || a.AwardedByAdminId == user.Id));
            _context.Reminders.RemoveRange(_context.Reminders.Where(r => taskIds.Contains(r.TaskItemId)));
            _context.SubTasks.RemoveRange(_context.SubTasks.Where(s => taskIds.Contains(s.TaskItemId)));
            _context.TimeLogs.RemoveRange(_context.TimeLogs.Where(t => taskIds.Contains(t.TaskItemId)));
            _context.TaskItems.RemoveRange(_context.TaskItems.Where(t => t.UserId == user.Id));
            _context.Categories.RemoveRange(_context.Categories.Where(c => c.UserId == user.Id));
            _context.Goals.RemoveRange(_context.Goals.Where(g => g.UserId == user.Id));
            _context.HabitCheckIns.RemoveRange(_context.HabitCheckIns.Where(c => habitIds.Contains(c.HabitId)));
            _context.Habits.RemoveRange(_context.Habits.Where(h => h.UserId == user.Id));
            _context.UserLoginSessions.RemoveRange(_context.UserLoginSessions.Where(s => s.UserId == user.Id));
            DeleteAvatarFile(user.AvatarUrl);
            _context.Users.Remove(user);

            await _context.SaveChangesAsync();
            await NotifyProfileDataChangedAsync(linkedTasks.Select(t => t.UserId).Append(user.Id), "An account was deleted.");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}



