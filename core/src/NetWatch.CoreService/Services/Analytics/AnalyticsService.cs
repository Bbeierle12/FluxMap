using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Analytics;

public sealed class AnalyticsService
{
    private readonly DeviceStore _store;

    public AnalyticsService(DeviceStore store)
    {
        _store = store;
    }

    public AnalyticsSummary GetSummary(TimeSpan window)
    {
        var since = DateTime.UtcNow.Subtract(window);
        var devices = _store.GetAll();
        var events = _store.GetEventsSince(since, 5000);
        var joins = events.Count(e => e.EventType == "join");
        var leaves = events.Count(e => e.EventType == "leave");
        return new AnalyticsSummary
        {
            WindowHours = window.TotalHours,
            DeviceCount = devices.Count,
            OnlineCount = devices.Count(d => d.IsOnline),
            JoinCount = joins,
            LeaveCount = leaves
        };
    }

    public DeviceSummary GetDeviceSummary(string deviceId, TimeSpan window)
    {
        var since = DateTime.UtcNow.Subtract(window);
        var device = _store.GetAll().FirstOrDefault(d => d.DeviceId == deviceId);
        var events = _store.GetEventsForDeviceSince(deviceId, since, 2000).ToList();
        var onlineSeconds = CalculateOnlineSeconds(events, since, DateTime.UtcNow);
        return new DeviceSummary
        {
            DeviceId = deviceId,
            WindowHours = window.TotalHours,
            OnlineSeconds = (int)onlineSeconds,
            JoinCount = events.Count(e => e.EventType == "join"),
            LeaveCount = events.Count(e => e.EventType == "leave"),
            LastSeenUtc = device?.LastSeenUtc
        };
    }

    private static double CalculateOnlineSeconds(List<DeviceEvent> events, DateTime windowStart, DateTime windowEnd)
    {
        var onlineSeconds = 0.0;
        DateTime? currentStart = null;

        foreach (var ev in events.OrderBy(e => e.OccurredAtUtc))
        {
            if (ev.EventType == "join")
            {
                currentStart = ev.OccurredAtUtc < windowStart ? windowStart : ev.OccurredAtUtc;
            }
            else if (ev.EventType == "leave" && currentStart.HasValue)
            {
                var end = ev.OccurredAtUtc;
                if (end > windowEnd)
                {
                    end = windowEnd;
                }
                onlineSeconds += Math.Max(0, (end - currentStart.Value).TotalSeconds);
                currentStart = null;
            }
        }

        if (currentStart.HasValue)
        {
            onlineSeconds += Math.Max(0, (windowEnd - currentStart.Value).TotalSeconds);
        }

        return onlineSeconds;
    }
}

public sealed class AnalyticsSummary
{
    public double WindowHours { get; set; }
    public int DeviceCount { get; set; }
    public int OnlineCount { get; set; }
    public int JoinCount { get; set; }
    public int LeaveCount { get; set; }
}

public sealed class DeviceSummary
{
    public string DeviceId { get; set; } = string.Empty;
    public double WindowHours { get; set; }
    public int OnlineSeconds { get; set; }
    public int JoinCount { get; set; }
    public int LeaveCount { get; set; }
    public DateTime? LastSeenUtc { get; set; }
}
