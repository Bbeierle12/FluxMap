namespace NetWatch.CoreService.Services.Connectors;

public sealed class RouterFingerprintStore
{
    private readonly object _lock = new();
    private readonly List<RouterFingerprint> _items = new();
    private DateTime _lastScanUtc = DateTime.MinValue;

    public IReadOnlyCollection<RouterFingerprint> GetAll()
    {
        lock (_lock)
        {
            return _items.Select(i => i).ToList().AsReadOnly();
        }
    }

    public DateTime LastScanUtc
    {
        get
        {
            lock (_lock)
            {
                return _lastScanUtc;
            }
        }
    }

    public void Update(IEnumerable<RouterFingerprint> items)
    {
        lock (_lock)
        {
            _items.Clear();
            _items.AddRange(items);
            _lastScanUtc = DateTime.UtcNow;
        }
    }
}
