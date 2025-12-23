namespace NetWatch.CoreService.Services.Connectors;

public sealed class ConnectorRegistry
{
    private readonly IReadOnlyDictionary<string, IRouterConnector> _connectors;
    private readonly ConnectorStatusStore _statusStore;

    public ConnectorRegistry(IEnumerable<IRouterConnector> connectors, ConnectorStatusStore statusStore)
    {
        _connectors = connectors.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        _statusStore = statusStore;
    }

    public IReadOnlyCollection<IRouterConnector> All => _connectors.Values.ToList().AsReadOnly();

    public bool TryGet(string key, out IRouterConnector connector) => _connectors.TryGetValue(key, out connector!);

    public void ReportSuccess(string key)
    {
        _statusStore.ReportSuccess(key);
    }

    public void ReportFailure(string key, string error)
    {
        _statusStore.ReportFailure(key, error);
    }

    public IReadOnlyCollection<ConnectorStatus> GetStatus() => _statusStore.GetAll();
}
