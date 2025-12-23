namespace NetWatch.CoreService.Services.Discovery;

public sealed class DiscoveryOptions
{
    public int ScanIntervalSeconds { get; set; } = 60;
    public int PingTimeoutMs { get; set; } = 800;
    public int TcpConnectTimeoutMs { get; set; } = 300;
    public int MaxConcurrentPings { get; set; } = 64;
    public int MaxHostsPerSubnet { get; set; } = 1024;
    public bool EnableSsdp { get; set; } = true;
    public List<int> TcpPorts { get; set; } = new() { 22, 23, 80, 443, 445, 554, 8000, 8080, 8443, 3389 };
}
