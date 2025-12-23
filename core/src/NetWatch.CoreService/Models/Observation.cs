namespace NetWatch.CoreService.Models;

public sealed class Observation
{
    public string Source { get; set; } = "unknown";
    public string? MacAddress { get; set; }
    public string? IpAddress { get; set; }
    public string? Hostname { get; set; }
    public string? Vendor { get; set; }
    public string? TypeHint { get; set; }
    public string? ServiceHint { get; set; }
    public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;
}
