using System.Net;
using System.Text;
using System.Text.Json;
using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Connectors;

public sealed class UnifiControllerConnector : IRouterConnector
{
    public string Key => "unifi";
    private readonly DeviceStore _store;
    private readonly ILogger<UnifiControllerConnector> _logger;
    private readonly CredentialVault _vault;

    public UnifiControllerConnector(DeviceStore store, ILogger<UnifiControllerConnector> logger, CredentialVault vault)
    {
        _store = store;
        _logger = logger;
        _vault = vault;
    }

    public async Task RunAsync(ConnectorSettings settings, CancellationToken ct)
    {
        var cfg = settings.Unifi;
        if (string.IsNullOrWhiteSpace(cfg.BaseUrl) || string.IsNullOrWhiteSpace(cfg.Username))
        {
            return;
        }

        var password = ResolvePassword(cfg);
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = (_, _, _, _) => cfg.SkipTlsVerify
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(cfg.BaseUrl.EndsWith("/") ? cfg.BaseUrl : $"{cfg.BaseUrl}/"),
            Timeout = TimeSpan.FromSeconds(6)
        };

        var loggedIn = await TryLogin(client, cfg.Username, password, ct);
        if (!loggedIn)
        {
            _logger.LogWarning("UniFi login failed for {BaseUrl}.", cfg.BaseUrl);
            return;
        }

        var site = string.IsNullOrWhiteSpace(cfg.Site) ? "default" : cfg.Site;
        var path = $"proxy/network/api/s/{site}/stat/sta";
        var response = await client.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("UniFi client list failed: {Status}", response.StatusCode);
            return;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        ParseClients(json);
    }

    private async Task<bool> TryLogin(HttpClient client, string username, string password, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { username, password });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var newLogin = await client.PostAsync("api/auth/login", content, ct);
        if (newLogin.IsSuccessStatusCode)
        {
            return true;
        }

        var legacy = await client.PostAsync("api/login", content, ct);
        return legacy.IsSuccessStatusCode;
    }

    private void ParseClients(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in data.EnumerateArray())
        {
            var mac = item.TryGetProperty("mac", out var macVal) ? macVal.GetString() : null;
            var ip = item.TryGetProperty("ip", out var ipVal) ? ipVal.GetString() : null;
            var hostname = item.TryGetProperty("hostname", out var hostVal) ? hostVal.GetString() : null;
            var name = item.TryGetProperty("name", out var nameVal) ? nameVal.GetString() : null;
            var oui = item.TryGetProperty("oui", out var ouiVal) ? ouiVal.GetString() : null;

            if (string.IsNullOrWhiteSpace(ip) && string.IsNullOrWhiteSpace(mac))
            {
                continue;
            }

            var observation = new Observation
            {
                Source = "unifi",
                IpAddress = ip,
                MacAddress = mac,
                Hostname = name ?? hostname,
                Vendor = oui,
                TypeHint = "client",
                ObservedAtUtc = DateTime.UtcNow
            };
            _store.UpsertFromObservation(observation);
        }
    }

    private string ResolvePassword(UnifiConnectorSettings settings)
    {
        if (_vault.TryGetSecret(settings.PasswordCredentialId, out var secret))
        {
            return secret;
        }

        return settings.Password;
    }
}
