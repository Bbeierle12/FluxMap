namespace NetWatch.CoreService.Services.Connectors;

public sealed class RouterFingerprint
{
    public string GatewayIp { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public double Confidence { get; set; }
    public string? SuggestedConnector { get; set; }
    public List<string> Evidence { get; set; } = new();
    public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;
}
