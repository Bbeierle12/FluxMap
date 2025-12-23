using System.Text.Json;
using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Connectors;

public sealed class LeaseHttpConnector : IRouterConnector
{
    private readonly string _key;
    private readonly Func<ConnectorSettings, LeaseHttpConnectorSettings> _selector;
    private readonly string _sourceName;
    private readonly DeviceStore _store;
    private readonly ILogger<LeaseHttpConnector> _logger;
    private readonly CredentialVault _vault;
    private readonly HttpClient _httpClient = new();

    public LeaseHttpConnector(
        string key,
        string sourceName,
        Func<ConnectorSettings, LeaseHttpConnectorSettings> selector,
        DeviceStore store,
        ILogger<LeaseHttpConnector> logger,
        CredentialVault vault)
    {
        _key = key;
        _sourceName = sourceName;
        _selector = selector;
        _store = store;
        _logger = logger;
        _vault = vault;
        _httpClient.Timeout = TimeSpan.FromSeconds(6);
    }

    public string Key => _key;

    public async Task RunAsync(ConnectorSettings settings, CancellationToken ct)
    {
        var cfg = _selector(settings);
        if (string.IsNullOrWhiteSpace(cfg.Url))
        {
            return;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, cfg.Url);
        var authValue = ResolveAuthValue(cfg);
        if (!string.IsNullOrWhiteSpace(cfg.AuthHeader) && !string.IsNullOrWhiteSpace(authValue))
        {
            request.Headers.Add(cfg.AuthHeader, authValue);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(ct);
            foreach (var lease in ParseLeases(cfg, content))
            {
                if (string.IsNullOrWhiteSpace(lease.IpAddress) && string.IsNullOrWhiteSpace(lease.MacAddress))
                {
                    continue;
                }

                var obs = new Observation
                {
                    Source = _sourceName,
                    IpAddress = lease.IpAddress,
                    MacAddress = lease.MacAddress,
                    Hostname = lease.Hostname,
                    TypeHint = "dhcp-lease",
                    ObservedAtUtc = DateTime.UtcNow
                };
                _store.UpsertFromObservation(obs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Source} lease connector failed.", _sourceName);
        }
    }

    private string ResolveAuthValue(LeaseHttpConnectorSettings settings)
    {
        if (_vault.TryGetSecret(settings.AuthValueCredentialId, out var secret))
        {
            return secret;
        }

        return settings.AuthValue;
    }

    private static IEnumerable<LeaseDto> ParseLeases(LeaseHttpConnectorSettings settings, string content)
    {
        var format = (settings.Format ?? "json").Trim().ToLowerInvariant();
        if (format == "csv")
        {
            return ParseCsv(settings, content);
        }

        if (format == "keyvalue")
        {
            return ParseKeyValue(settings, content);
        }

        return ParseJson(settings, content);
    }

    private static IEnumerable<LeaseDto> ParseJson(LeaseHttpConnectorSettings settings, string content)
    {
        var leases = new List<LeaseDto>();
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return leases;
        }

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            leases.Add(new LeaseDto
            {
                IpAddress = GetString(item, settings.IpField),
                MacAddress = GetString(item, settings.MacField),
                Hostname = GetString(item, settings.HostField)
            });
        }

        return leases;
    }

    private static IEnumerable<LeaseDto> ParseCsv(LeaseHttpConnectorSettings settings, string content)
    {
        var leases = new List<LeaseDto>();
        var delimiter = string.IsNullOrEmpty(settings.CsvDelimiter) ? "," : settings.CsvDelimiter;
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(delimiter);
            if (parts.Length <= Math.Max(settings.MacColumn, Math.Max(settings.IpColumn, settings.HostColumn)))
            {
                continue;
            }
            leases.Add(new LeaseDto
            {
                IpAddress = parts[settings.IpColumn].Trim(),
                MacAddress = parts[settings.MacColumn].Trim(),
                Hostname = parts[settings.HostColumn].Trim()
            });
        }
        return leases;
    }

    private static IEnumerable<LeaseDto> ParseKeyValue(LeaseHttpConnectorSettings settings, string content)
    {
        var leases = new List<LeaseDto>();
        var current = new LeaseDto();
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (!string.IsNullOrWhiteSpace(current.IpAddress) || !string.IsNullOrWhiteSpace(current.MacAddress))
                {
                    leases.Add(current);
                }
                current = new LeaseDto();
                continue;
            }

            var idx = trimmed.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }
            var key = trimmed[..idx].Trim().ToLowerInvariant();
            var value = trimmed[(idx + 1)..].Trim();
            if (key == settings.IpField.ToLowerInvariant())
            {
                current.IpAddress = value;
            }
            else if (key == settings.MacField.ToLowerInvariant())
            {
                current.MacAddress = value;
            }
            else if (key == settings.HostField.ToLowerInvariant())
            {
                current.Hostname = value;
            }
        }
        return leases;
    }

    private static string? GetString(JsonElement element, string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }
        if (element.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }
        return null;
    }

    private sealed class LeaseDto
    {
        public string? IpAddress { get; set; }
        public string? MacAddress { get; set; }
        public string? Hostname { get; set; }
    }
}
