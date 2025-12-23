namespace NetWatch.CoreService.Services.Connectors;

public sealed class ConnectorOptions
{
    public int PollIntervalSeconds { get; set; } = 300;
    public int FingerprintIntervalSeconds { get; set; } = 300;
}
