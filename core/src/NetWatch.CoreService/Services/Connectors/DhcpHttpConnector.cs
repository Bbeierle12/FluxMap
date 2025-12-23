using System.Net.Http.Headers;
using System.Text.Json;
using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Connectors;

public sealed class DhcpHttpConnector : IRouterConnector
{
    public string Key => "dhcp-http";
    private readonly DeviceStore _store;
    private readonly ILogger<DhcpHttpConnector> _logger;
    private readonly CredentialVault _vault;
    private readonly HttpClient _httpClient = new();

    public DhcpHttpConnector(DeviceStore store, ILogger<DhcpHttpConnector> logger, CredentialVault vault)
    {
        _store = store;
        _logger = logger;
        _vault = vault;
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task RunAsync(ConnectorSettings settings, CancellationToken ct)
    {
        var cfg = settings.DhcpHttp;
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
            var json = await response.Content.ReadAsStringAsync(ct);
            var leases = JsonSerializer.Deserialize<List<DhcpLeaseDto>>(json);
            if (leases is null)
            {
                return;
            }

            foreach (var lease in leases)
            {
                if (string.IsNullOrWhiteSpace(lease.IpAddress))
                {
                    continue;
                }

                var observation = new Observation
                {
                    Source = "dhcp-http",
                    IpAddress = lease.IpAddress,
                    MacAddress = lease.MacAddress,
                    Hostname = lease.Hostname,
                    TypeHint = "dhcp-lease",
                    ObservedAtUtc = DateTime.UtcNow
                };
                _store.UpsertFromObservation(observation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DHCP HTTP connector failed.");
        }
    }

    private sealed class DhcpLeaseDto
    {
        public string? IpAddress { get; set; }
        public string? MacAddress { get; set; }
        public string? Hostname { get; set; }
    }

    private string ResolveAuthValue(DhcpHttpConnectorSettings settings)
    {
        if (_vault.TryGetSecret(settings.AuthValueCredentialId, out var secret))
        {
            return secret;
        }

        return settings.AuthValue;
    }
}
