namespace NetWatch.CoreService.Services.Connectors;

public sealed class ConnectorStatusStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ConnectorStatus> _status = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ConnectorStatus> GetAll()
    {
        lock (_lock)
        {
            return _status.Values.ToList().AsReadOnly();
        }
    }

    public void ReportSuccess(string key)
    {
        lock (_lock)
        {
            if (!_status.TryGetValue(key, out var status))
            {
                status = new ConnectorStatus { Key = key };
                _status[key] = status;
            }

            status.LastSuccessUtc = DateTime.UtcNow;
            status.LastError = null;
        }
    }

    public void ReportFailure(string key, string error)
    {
        lock (_lock)
        {
            if (!_status.TryGetValue(key, out var status))
            {
                status = new ConnectorStatus { Key = key };
                _status[key] = status;
            }

            status.LastErrorUtc = DateTime.UtcNow;
            status.LastError = error;
        }
    }
}

public sealed class ConnectorStatus
{
    public string Key { get; set; } = string.Empty;
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastErrorUtc { get; set; }
    public string? LastError { get; set; }
}
