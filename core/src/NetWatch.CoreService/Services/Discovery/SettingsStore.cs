using System.Text.Json;

namespace NetWatch.CoreService.Services.Discovery;

public sealed class SettingsStore
{
    private readonly string _path;
    private readonly ILogger<SettingsStore> _logger;
    private readonly object _lock = new();
    private DiscoveryOptions _current;

    public SettingsStore(string path, DiscoveryOptions defaults, ILogger<SettingsStore> logger)
    {
        _path = path;
        _logger = logger;
        _current = Normalize(defaults);
        LoadFromDisk();
    }

    public DiscoveryOptions Get()
    {
        lock (_lock)
        {
            return Clone(_current);
        }
    }

    public DiscoveryOptions Update(DiscoveryOptions options)
    {
        lock (_lock)
        {
            _current = Normalize(options);
            SaveToDisk(_current);
            return Clone(_current);
        }
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_path))
        {
            SaveToDisk(_current);
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<DiscoveryOptions>(json);
            if (loaded is not null)
            {
                _current = Normalize(loaded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults.");
        }
    }

    private void SaveToDisk(DiscoveryOptions options)
    {
        try
        {
            var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save settings.");
        }
    }

    private static DiscoveryOptions Normalize(DiscoveryOptions options)
    {
        var normalized = Clone(options);
        normalized.ScanIntervalSeconds = Clamp(normalized.ScanIntervalSeconds, 10, 3600);
        normalized.PingTimeoutMs = Clamp(normalized.PingTimeoutMs, 100, 5000);
        normalized.TcpConnectTimeoutMs = Clamp(normalized.TcpConnectTimeoutMs, 100, 5000);
        normalized.MaxConcurrentPings = Clamp(normalized.MaxConcurrentPings, 1, 256);
        normalized.MaxHostsPerSubnet = Clamp(normalized.MaxHostsPerSubnet, 16, 65535);
        normalized.TcpPorts = normalized.TcpPorts.Where(p => p > 0 && p <= 65535).Distinct().ToList();
        return normalized;
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

    private static DiscoveryOptions Clone(DiscoveryOptions options)
    {
        return new DiscoveryOptions
        {
            ScanIntervalSeconds = options.ScanIntervalSeconds,
            PingTimeoutMs = options.PingTimeoutMs,
            TcpConnectTimeoutMs = options.TcpConnectTimeoutMs,
            MaxConcurrentPings = options.MaxConcurrentPings,
            MaxHostsPerSubnet = options.MaxHostsPerSubnet,
            EnableSsdp = options.EnableSsdp,
            TcpPorts = new List<int>(options.TcpPorts)
        };
    }
}
