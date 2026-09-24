using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TimeTamer.Services;

namespace TimeTamer.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly RealtimeNotificationService _notifications;

        public NotificationsController(RealtimeNotificationService notifications)
        {
            _notifications = notifications;
        }

        [HttpGet]
        public async Task Stream()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Content-Type"] = "text/event-stream";
            var subscription = _notifications.Subscribe(userId);

            try
            {
                await Response.WriteAsync("event: connected\ndata: {}\n\n");
                await Response.Body.FlushAsync();

                await foreach (var json in subscription.Reader.ReadAllAsync(HttpContext.RequestAborted))
                {
                    await Response.WriteAsync($"event: notification\ndata: {json}\n\n");
                    await Response.Body.FlushAsync();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _notifications.Unsubscribe(userId, subscription.SubscriptionId);
            }
        }
    }
}
