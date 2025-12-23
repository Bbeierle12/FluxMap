namespace NetWatch.CoreService.Models;

public sealed class Device
{
    public string DeviceId { get; init; } = Guid.NewGuid().ToString("N");
    public string? MacAddress { get; set; }
    public string? IpAddress { get; set; }
    public string? Hostname { get; set; }
    public string? Vendor { get; set; }
    public string? TypeGuess { get; set; }
    public DateTime FirstSeenUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public double Confidence { get; set; } = 0.1;
    public bool IsOnline { get; set; } = true;
}
