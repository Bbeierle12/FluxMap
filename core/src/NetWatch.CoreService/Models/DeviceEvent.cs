namespace NetWatch.CoreService.Models;

public sealed class DeviceEvent
{
    public long EventId { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
    public string? Detail { get; init; }
}
