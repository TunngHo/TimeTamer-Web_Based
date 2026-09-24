using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TimeTamer.Data;
using TimeTamer.Models;
using TimeTamer.Services;

namespace TimeTamer.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;

        public AccountController(AppDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            model.Email = model.Email.Trim();
            model.Username = model.Username.Trim();
            if (_context.Users.Any(u => u.Username == model.Username))
            {
                ViewBag.Error = "Username already exists.";
                return View(model);
            }

            if (_context.Users.Any(u => u.Email == model.Email))
            {
                ViewBag.Error = "Email already exists.";
                return View(model);
            }

            var welcomeSent = await _emailSender.SendAsync(
                model.Email,
                "Welcome to TimeTamer",
                $@"
                <div style='font-family:Arial,sans-serif;line-height:1.6;color:#172033'>
                    <h2>Welcome to TimeTamer</h2>
                    <p>Hello <strong>{WebUtility.HtmlEncode(model.Username)}</strong>,</p>
                    <p>Your TimeTamer account is ready. You can now manage tasks, reminders, goals, and habits in one place.</p>
                    <p>If you did not create this account, please ignore this email.</p>
                </div>");

            if (!welcomeSent)
            {
                ModelState.AddModelError(nameof(model.Email), "Email does not exist or cannot receive mail.");
                return View(model);
            }

            var isAdmin = string.Equals(model.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            _context.Users.Add(new User
            {
                Username = model.Username,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Email = model.Email,
                DailyUsageLimitMinutes = 120,
                IsAdmin = isAdmin,
                UserCode = GenerateAccountCode("USR", code => !_context.Users.Any(u => u.UserCode == code)),
                AdminCode = isAdmin ? GenerateAccountCode("ADM", code => !_context.Users.Any(u => u.AdminCode == code)) : null
            });
            await _context.SaveChangesAsync();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = _context.Users.FirstOrDefault(u => u.Username == model.Username);
            if (user != null && BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
            {
                if (user.IsSuspended)
                {
                    ViewBag.Error = "This account has been suspended. Please contact the administrator.";
                    return View(model);
                }


                var sessionKey = GenerateToken();
                _context.UserLoginSessions.Add(new UserLoginSession
                {
                    UserId = user.Id,
                    SessionKey = sessionKey,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddHours(2)
                });
                await _context.SaveChangesAsync();

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim("FullName", user.FullName ?? user.Username),
                    new Claim("IsAdmin", user.IsAdmin ? "true" : "false"),
                    new Claim("LoginSessionKey", sessionKey),
                    new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
                };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
                return RedirectToAction("Index", "Home");
            }
            ViewBag.Error = "Invalid username or password.";
            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);
            string? devOtp = null;
            if (user != null)
            {
                var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
                user.PasswordResetTokenHash = HashToken(otp);
                user.PasswordResetTokenExpiresAt = DateTime.Now.AddMinutes(10);
                user.PasswordResetRequestedAt = DateTime.Now;
                user.PasswordResetOtpAttempts = 0;
                user.PasswordResetVerifiedAt = null;
                await _context.SaveChangesAsync();
                var sent = await _emailSender.SendAsync(user.Email!, "Your TimeTamer password reset OTP", $"<p>Your TimeTamer password reset OTP is:</p><h2>{otp}</h2><p>This code expires in 10 minutes. Do not share it with anyone.</p>");
                if (!sent) devOtp = otp;
            }
            TempData["ResetEmail"] = model.Email;
            TempData["DevOtp"] = devOtp;
            return RedirectToAction(nameof(VerifyResetOtp), new { email = model.Email });
        }

        [HttpGet]
        public IActionResult VerifyResetOtp(string? email)
        {
            var resetEmail = email ?? TempData["ResetEmail"] as string ?? string.Empty;
            ViewBag.DevOtp = TempData["DevOtp"];
            return View(new VerifyOtpViewModel { Email = resetEmail });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyResetOtp(VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);
            if (user == null || user.PasswordResetTokenHash == null || user.PasswordResetTokenExpiresAt == null || user.PasswordResetTokenExpiresAt <= DateTime.Now)
            {
                ModelState.AddModelError("Otp", "OTP is invalid or expired.");
                return View(model);
            }
            if (user.PasswordResetOtpAttempts >= 5)
            {
                ClearResetState(user);
                await _context.SaveChangesAsync();
                ModelState.AddModelError("Otp", "Too many incorrect attempts. Please request a new OTP.");
                return View(model);
            }
            if (user.PasswordResetTokenHash != HashToken(model.Otp.Trim()))
            {
                user.PasswordResetOtpAttempts += 1;
                await _context.SaveChangesAsync();
                ModelState.AddModelError("Otp", "Incorrect OTP.");
                return View(model);
            }

            var resetSession = GenerateToken();
            user.PasswordResetTokenHash = HashToken(resetSession);
            user.PasswordResetTokenExpiresAt = DateTime.Now.AddMinutes(10);
            user.PasswordResetVerifiedAt = DateTime.Now;
            user.PasswordResetOtpAttempts = 0;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(CompleteResetPassword), new { session = resetSession });
        }

        [HttpGet]
        public IActionResult CompleteResetPassword(string session)
        {
            if (string.IsNullOrWhiteSpace(session)) return RedirectToAction(nameof(ForgotPassword));
            return View(new CompleteResetPasswordViewModel { ResetSession = session });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteResetPassword(CompleteResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var sessionHash = HashToken(model.ResetSession);
            var user = _context.Users.FirstOrDefault(u => u.PasswordResetTokenHash == sessionHash && u.PasswordResetVerifiedAt != null && u.PasswordResetTokenExpiresAt != null && u.PasswordResetTokenExpiresAt > DateTime.Now);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Reset session is invalid or expired. Please request a new OTP.");
                return View(model);
            }
            user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            ClearResetState(user);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                await _emailSender.SendAsync(
                    user.Email,
                    "Your TimeTamer password was changed",
                    $@"
                    <div style='font-family:Arial,sans-serif;line-height:1.6;color:#172033'>
                        <h2>Password changed</h2>
                        <p>The password for your TimeTamer account was changed successfully.</p>
                        <p><strong>Username:</strong> {WebUtility.HtmlEncode(user.Username)}</p>
                        <p><strong>Changed at:</strong> {DateTime.Now:MM/dd/yyyy HH:mm}</p>
                        <p>If you did not make this change, please reset your password again immediately.</p>
                    </div>");
            }

            ViewBag.Success = "Password reset successfully. You can log in now.";
            return View("ResetPasswordConfirmation");
        }

        [HttpGet]
        public IActionResult ResetPassword(string token) => RedirectToAction(nameof(VerifyResetOtp));

        public async Task<IActionResult> Logout()
        {
            var sessionKey = User.FindFirstValue("LoginSessionKey");
            if (!string.IsNullOrWhiteSpace(sessionKey))
            {
                var session = await _context.UserLoginSessions.FirstOrDefaultAsync(s => s.SessionKey == sessionKey && s.RevokedAt == null);
                if (session != null)
                {
                    session.RevokedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
        private static void ClearResetState(User user)
        {
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpiresAt = null;
            user.PasswordResetRequestedAt = null;
            user.PasswordResetVerifiedAt = null;
            user.PasswordResetOtpAttempts = 0;
        }

        private static string GenerateToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private static string GenerateAccountCode(string prefix, Func<string, bool> isAvailable)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var chars = new char[8];
                for (var i = 0; i < chars.Length; i++)
                {
                    chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
                }

                var code = $"{prefix}-{new string(chars)}";
                if (isAvailable(code)) return code;
            }

            return $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }
    }
}






