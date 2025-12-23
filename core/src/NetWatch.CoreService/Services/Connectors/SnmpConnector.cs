using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Connectors;

public sealed class SnmpConnector : IRouterConnector
{
    public string Key => "snmp";
    private readonly DeviceStore _store;
    private readonly ILogger<SnmpConnector> _logger;
    private readonly CredentialVault _vault;

    private static readonly Regex IpRegex = new(@"(\d{1,3}\.){3}\d{1,3}", RegexOptions.Compiled);
    private static readonly Regex MacRegex = new(@"([0-9A-Fa-f]{1,2}:){5}[0-9A-Fa-f]{1,2}", RegexOptions.Compiled);

    public SnmpConnector(DeviceStore store, ILogger<SnmpConnector> logger, CredentialVault vault)
    {
        _store = store;
        _logger = logger;
        _vault = vault;
    }

    public async Task RunAsync(ConnectorSettings settings, CancellationToken ct)
    {
        var snmp = settings.Snmp;
        if (snmp.Hosts.Count == 0)
        {
            return;
        }

        var community = ResolveCommunity(snmp);

        foreach (var host in snmp.Hosts)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await RunSnmpWalk(host, snmp, community, ct);
        }
    }

    private async Task RunSnmpWalk(string host, SnmpConnectorSettings settings, string community, CancellationToken ct)
    {
        var args = $"-v2c -c {community} -t {settings.TimeoutSeconds} -r 1 {host}:{settings.Port} 1.3.6.1.2.1.4.22.1";
        var psi = new ProcessStartInfo
        {
            FileName = settings.SnmpWalkPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                _logger.LogWarning("snmpwalk process failed to start.");
                return;
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var err = await process.StandardError.ReadToEndAsync(ct);
                _logger.LogWarning("snmpwalk exited with {Code}: {Error}", process.ExitCode, err);
                return;
            }

            ParseSnmpWalk(output);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "snmpwalk failed for host {Host}.", host);
        }
    }

    private void ParseSnmpWalk(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var ipMatch = IpRegex.Match(line);
            if (!ipMatch.Success || !IPAddress.TryParse(ipMatch.Value, out var ip))
            {
                continue;
            }

            var macMatch = MacRegex.Match(line);
            if (!macMatch.Success)
            {
                continue;
            }

            var mac = NormalizeMac(macMatch.Value);
            var observation = new Observation
            {
                Source = "snmp",
                IpAddress = ip.ToString(),
                MacAddress = mac,
                TypeHint = "arp-table",
                ObservedAtUtc = DateTime.UtcNow
            };
            _store.UpsertFromObservation(observation);
        }
    }

    private static string NormalizeMac(string mac)
    {
        return mac.Split(':')
            .Select(part => int.Parse(part, NumberStyles.HexNumber).ToString("X2"))
            .Aggregate((a, b) => $"{a}:{b}");
    }

    private string ResolveCommunity(SnmpConnectorSettings settings)
    {
        if (_vault.TryGetSecret(settings.CommunityCredentialId, out var secret))
        {
            return secret;
        }

        return settings.Community;
    }
}
