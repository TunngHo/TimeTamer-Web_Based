using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TimeTamer.Data;

namespace TimeTamer.Services
{
    public class TabSessionMiddleware
    {
        private readonly RequestDelegate _next;

        public TabSessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext db)
        {
            var sid = GetSessionId(context);
            if (!string.IsNullOrWhiteSpace(sid))
            {
                var session = await db.UserLoginSessions
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.SessionKey == sid && s.RevokedAt == null && s.ExpiresAt > DateTime.Now);

                if (session?.User != null && !session.User.IsSuspended)
                {
                    var user = session.User;
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim("FullName", user.FullName ?? user.Username),
                        new Claim("IsAdmin", user.IsAdmin ? "true" : "false"),
                        new Claim("LoginSessionKey", session.SessionKey),
                        new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
                    };
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TabSession"));
                }
            }

            await _next(context);
        }

        private static string? GetSessionId(HttpContext context)
        {
            if (context.Request.Query.TryGetValue("sid", out var querySid)) return querySid.ToString();
            if (context.Request.HasFormContentType && context.Request.Form.TryGetValue("sid", out var formSid)) return formSid.ToString();
            if (context.Request.Headers.TryGetValue("X-TimeTamer-Session", out var headerSid)) return headerSid.ToString();
            return null;
        }
    }
}
