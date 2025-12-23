namespace NetWatch.CoreService.Services.Connectors;

public interface IRouterConnector
{
    string Key { get; }
    Task RunAsync(ConnectorSettings settings, CancellationToken ct);
}
