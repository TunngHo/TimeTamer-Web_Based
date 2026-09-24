using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace TimeTamer.Services
{
    public class RealtimeNotificationService
    {
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Channel<string>>> _subscribers = new();

        public (Guid SubscriptionId, ChannelReader<string> Reader) Subscribe(int userId)
        {
            var id = Guid.NewGuid();
            var channel = Channel.CreateUnbounded<string>();
            var userChannels = _subscribers.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<string>>());
            userChannels[id] = channel;
            return (id, channel.Reader);
        }

        public void Unsubscribe(int userId, Guid subscriptionId)
        {
            if (_subscribers.TryGetValue(userId, out var channels) && channels.TryRemove(subscriptionId, out var channel))
            {
                channel.Writer.TryComplete();
                if (channels.IsEmpty) _subscribers.TryRemove(userId, out _);
            }
        }

        public async Task NotifyUserAsync(int userId, object payload)
        {
            if (!_subscribers.TryGetValue(userId, out var channels)) return;
            var json = JsonSerializer.Serialize(payload);
            foreach (var channel in channels.Values)
            {
                await channel.Writer.WriteAsync(json);
            }
        }

        public async Task NotifyUsersAsync(IEnumerable<int> userIds, object payload)
        {
            foreach (var userId in userIds.Distinct())
            {
                await NotifyUserAsync(userId, payload);
            }
        }

        public Task NotifyDataChangedAsync(IEnumerable<int> userIds, string scope, IEnumerable<string> paths, string? message = null, string? url = null)
        {
            return NotifyUsersAsync(userIds, new
            {
                id = Guid.NewGuid().ToString("N"),
                type = "dataChanged",
                scope,
                paths = paths.Distinct().ToList(),
                message = message ?? "Data changed.",
                url = url ?? paths.FirstOrDefault() ?? "/"
            });
        }

        public Task NotifyDataChangedAsync(int userId, string scope, IEnumerable<string> paths, string? message = null, string? url = null)
        {
            return NotifyDataChangedAsync(new[] { userId }, scope, paths, message, url);
        }
    }
}
