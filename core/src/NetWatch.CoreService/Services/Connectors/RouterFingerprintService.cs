using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace NetWatch.CoreService.Services.Connectors;

public sealed class RouterFingerprintService : BackgroundService
{
    private readonly RouterFingerprintStore _store;
    private readonly ILogger<RouterFingerprintService> _logger;
    private readonly ConnectorOptions _options;

    private static readonly string[] Paths = { "/", "/index.html", "/login.htm", "/login.html", "/cgi-bin/luci" };
    private static readonly Regex TitleRegex = new("<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public RouterFingerprintService(
        RouterFingerprintStore store,
        ILogger<RouterFingerprintService> logger,
        IOptions<ConnectorOptions> options)
    {
        _store = store;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var results = await ScanGateways(stoppingToken);
                _store.Update(results);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Router fingerprint scan failed.");
            }

            var delay = TimeSpan.FromSeconds(Math.Max(60, _options.FingerprintIntervalSeconds));
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<List<RouterFingerprint>> ScanGateways(CancellationToken ct)
    {
        var gateways = GetGateways().Distinct().ToList();
        var results = new List<RouterFingerprint>();
        foreach (var gateway in gateways)
        {
            var result = await ProbeGateway(gateway, ct);
            if (result is not null)
            {
                results.Add(result);
            }
        }
        return results;
    }

    private static IEnumerable<string> GetGateways()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var props = nic.GetIPProperties();
            foreach (var gw in props.GatewayAddresses)
            {
                if (gw.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    yield return gw.Address.ToString();
                }
            }
        }
    }

    private async Task<RouterFingerprint?> ProbeGateway(string gatewayIp, CancellationToken ct)
    {
        var evidence = new List<string>();
        var vendor = string.Empty;
        var model = string.Empty;
        var suggested = string.Empty;
        var confidence = 0.0;

        foreach (var scheme in new[] { "http", "https" })
        {
            var handler = new HttpClientHandler();
            if (scheme == "https")
            {
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            foreach (var path in Paths)
            {
                var url = $"{scheme}://{gatewayIp}{path}";
                try
                {
                    using var response = await client.GetAsync(url, ct);
                    var server = response.Headers.Server.ToString();
                    if (!string.IsNullOrWhiteSpace(server))
                    {
                        evidence.Add($"server:{server}");
                        ScoreMatch(server, ref vendor, ref suggested, ref confidence);
                    }

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                    if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                    {
                        var body = await ReadBody(response, 32000, ct);
                        var title = ExtractTitle(body);
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            evidence.Add($"title:{title}");
                            ScoreMatch(title, ref vendor, ref suggested, ref confidence);
                        }
                        if (string.IsNullOrWhiteSpace(model))
                        {
                            model = GuessModel(body);
                        }
                    }

                    if (confidence >= 0.8)
                    {
                        return new RouterFingerprint
                        {
                            GatewayIp = gatewayIp,
                            BaseUrl = $"{scheme}://{gatewayIp}",
                            Vendor = vendor,
                            Model = string.IsNullOrWhiteSpace(model) ? null : model,
                            Confidence = confidence,
                            SuggestedConnector = string.IsNullOrWhiteSpace(suggested) ? null : suggested,
                            Evidence = evidence,
                            ObservedAtUtc = DateTime.UtcNow
                        };
                    }
                }
                catch
                {
                }
            }
        }

        if (confidence <= 0.0 && evidence.Count == 0)
        {
            return null;
        }

        return new RouterFingerprint
        {
            GatewayIp = gatewayIp,
            BaseUrl = $"http://{gatewayIp}",
            Vendor = string.IsNullOrWhiteSpace(vendor) ? null : vendor,
            Model = string.IsNullOrWhiteSpace(model) ? null : model,
            Confidence = confidence,
            SuggestedConnector = string.IsNullOrWhiteSpace(suggested) ? null : suggested,
            Evidence = evidence,
            ObservedAtUtc = DateTime.UtcNow
        };
    }

    private static string ExtractTitle(string body)
    {
        var match = TitleRegex.Match(body);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string GuessModel(string body)
    {
        if (body.Contains("Nighthawk", StringComparison.OrdinalIgnoreCase))
        {
            return "Nighthawk";
        }
        if (body.Contains("Archer", StringComparison.OrdinalIgnoreCase))
        {
            return "Archer";
        }
        if (body.Contains("Omada", StringComparison.OrdinalIgnoreCase))
        {
            return "Omada";
        }
        return string.Empty;
    }

    private static void ScoreMatch(string text, ref string vendor, ref string suggested, ref double confidence)
    {
        if (text.Contains("netgear", StringComparison.OrdinalIgnoreCase) || text.Contains("nighthawk", StringComparison.OrdinalIgnoreCase))
        {
            vendor = "Netgear";
            suggested = text.Contains("orbi", StringComparison.OrdinalIgnoreCase) ? "orbi" : "netgear";
            confidence = Math.Max(confidence, 0.75);
            return;
        }
        if (text.Contains("tp-link", StringComparison.OrdinalIgnoreCase) || text.Contains("tplink", StringComparison.OrdinalIgnoreCase) || text.Contains("archer", StringComparison.OrdinalIgnoreCase))
        {
            vendor = "TP-Link";
            suggested = "tplink";
            confidence = Math.Max(confidence, 0.75);
            return;
        }
        if (text.Contains("unifi", StringComparison.OrdinalIgnoreCase) || text.Contains("ubiquiti", StringComparison.OrdinalIgnoreCase))
        {
            vendor = "UniFi";
            suggested = "unifi";
            confidence = Math.Max(confidence, 0.75);
            return;
        }
        if (text.Contains("asus", StringComparison.OrdinalIgnoreCase) || text.Contains("rt-", StringComparison.OrdinalIgnoreCase))
        {
            vendor = "Asus";
            suggested = "asus";
            confidence = Math.Max(confidence, 0.6);
            return;
        }
        if (text.Contains("omada", StringComparison.OrdinalIgnoreCase))
        {
            vendor = "Omada";
            suggested = "omada";
            confidence = Math.Max(confidence, 0.6);
            return;
        }
    }

    private static async Task<string> ReadBody(HttpResponseMessage response, int maxBytes, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[maxBytes];
        var read = await stream.ReadAsync(buffer, ct);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }
}
