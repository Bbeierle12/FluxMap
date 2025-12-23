namespace NetWatch.CoreService.Models;

public sealed class DeviceObservation
{
    public long ObservationId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? MacAddress { get; init; }
    public string? IpAddress { get; init; }
    public string? Hostname { get; init; }
    public string? Vendor { get; init; }
    public string? TypeHint { get; init; }
    public string? ServiceHint { get; init; }
    public DateTime ObservedAtUtc { get; init; }
}
