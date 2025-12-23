using System.Text.Json;

namespace NetWatch.CoreService.Services.Connectors;

public sealed class ConnectorSettingsStore
{
    private readonly string _path;
    private readonly ILogger<ConnectorSettingsStore> _logger;
    private readonly object _lock = new();
    private ConnectorSettings _current;

    public ConnectorSettingsStore(string path, ILogger<ConnectorSettingsStore> logger)
    {
        _path = path;
        _logger = logger;
        _current = new ConnectorSettings();
        LoadFromDisk();
    }

    public ConnectorSettings Get()
    {
        lock (_lock)
        {
            return Clone(_current);
        }
    }

    public ConnectorSettings Update(ConnectorSettings settings)
    {
        lock (_lock)
        {
            _current = Normalize(settings);
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
            var loaded = JsonSerializer.Deserialize<ConnectorSettings>(json);
            if (loaded is not null)
            {
                _current = Normalize(loaded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load connector settings, using defaults.");
        }
    }

    private void SaveToDisk(ConnectorSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save connector settings.");
        }
    }

    private static ConnectorSettings Normalize(ConnectorSettings settings)
    {
        var normalized = Clone(settings);
        normalized.Snmp.TimeoutSeconds = Clamp(normalized.Snmp.TimeoutSeconds, 1, 15);
        normalized.Snmp.Port = Clamp(normalized.Snmp.Port, 1, 65535);
        normalized.Snmp.SnmpWalkPath = string.IsNullOrWhiteSpace(normalized.Snmp.SnmpWalkPath)
            ? "snmpwalk"
            : normalized.Snmp.SnmpWalkPath;
        normalized.Snmp.CommunityCredentialId = string.IsNullOrWhiteSpace(normalized.Snmp.CommunityCredentialId)
            ? null
            : normalized.Snmp.CommunityCredentialId.Trim();
        normalized.Snmp.Hosts = normalized.Snmp.Hosts
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        normalized.DhcpHttp.Url = normalized.DhcpHttp.Url?.Trim() ?? string.Empty;
        normalized.DhcpHttp.AuthHeader = normalized.DhcpHttp.AuthHeader?.Trim() ?? string.Empty;
        normalized.DhcpHttp.AuthValue = normalized.DhcpHttp.AuthValue ?? string.Empty;
        normalized.DhcpHttp.AuthValueCredentialId = string.IsNullOrWhiteSpace(normalized.DhcpHttp.AuthValueCredentialId)
            ? null
            : normalized.DhcpHttp.AuthValueCredentialId.Trim();
        normalized.Unifi.BaseUrl = normalized.Unifi.BaseUrl?.Trim().TrimEnd('/') ?? string.Empty;
        normalized.Unifi.Site = string.IsNullOrWhiteSpace(normalized.Unifi.Site) ? "default" : normalized.Unifi.Site.Trim();
        normalized.Unifi.Username = normalized.Unifi.Username?.Trim() ?? string.Empty;
        normalized.Unifi.Password = normalized.Unifi.Password ?? string.Empty;
        normalized.Unifi.PasswordCredentialId = string.IsNullOrWhiteSpace(normalized.Unifi.PasswordCredentialId)
            ? null
            : normalized.Unifi.PasswordCredentialId.Trim();
        normalized.TpLink.Url = normalized.TpLink.Url?.Trim() ?? string.Empty;
        normalized.TpLink.AuthHeader = normalized.TpLink.AuthHeader?.Trim() ?? string.Empty;
        normalized.TpLink.AuthValue = normalized.TpLink.AuthValue ?? string.Empty;
        normalized.TpLink.AuthValueCredentialId = string.IsNullOrWhiteSpace(normalized.TpLink.AuthValueCredentialId)
            ? null
            : normalized.TpLink.AuthValueCredentialId.Trim();
        NormalizeLease(normalized.TpLink);
        normalized.Netgear.Url = normalized.Netgear.Url?.Trim() ?? string.Empty;
        normalized.Netgear.AuthHeader = normalized.Netgear.AuthHeader?.Trim() ?? string.Empty;
        normalized.Netgear.AuthValue = normalized.Netgear.AuthValue ?? string.Empty;
        normalized.Netgear.AuthValueCredentialId = string.IsNullOrWhiteSpace(normalized.Netgear.AuthValueCredentialId)
            ? null
            : normalized.Netgear.AuthValueCredentialId.Trim();
        NormalizeLease(normalized.Netgear);
        normalized.Orbi.Url = normalized.Orbi.Url?.Trim() ?? string.Empty;
        normalized.Orbi.AuthHeader = normalized.Orbi.AuthHeader?.Trim() ?? string.Empty;
        normalized.Orbi.AuthValue = normalized.Orbi.AuthValue ?? string.Empty;
        normalized.Orbi.AuthValueCredentialId = string.IsNullOrWhiteSpace(normalized.Orbi.AuthValueCredentialId)
            ? null
            : normalized.Orbi.AuthValueCredentialId.Trim();
        NormalizeLease(normalized.Orbi);
        normalized.Omada.Url = normalized.Omada.Url?.Trim() ?? string.Empty;
        normalized.Omada.AuthHeader = normalized.Omada.AuthHeader?.Trim() ?? string.Empty;
        normalized.Omada.AuthValue = normalized.Omada.AuthValue ?? string.Empty;
        normalized.Omada.AuthValueCredentialId = string.IsNullOrWhiteSpace(normalized.Omada.AuthValueCredentialId)
            ? null
            : normalized.Omada.AuthValueCredentialId.Trim();
        NormalizeLease(normalized.Omada);
        normalized.Asus.Url = normalized.Asus.Url?.Trim() ?? string.Empty;
        normalized.Asus.AuthHeader = normalized.Asus.AuthHeader?.Trim() ?? string.Empty;
        normalized.Asus.AuthValue = normalized.Asus.AuthValue ?? string.Empty;
        normalized.Asus.AuthValueCredentialId = string.IsNullOrWhiteSpace(normalized.Asus.AuthValueCredentialId)
            ? null
            : normalized.Asus.AuthValueCredentialId.Trim();
        NormalizeLease(normalized.Asus);
        return normalized;
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

    private static void NormalizeLease(LeaseHttpConnectorSettings settings)
    {
        var format = (settings.Format ?? "json").Trim().ToLowerInvariant();
        settings.Format = format is "csv" or "keyvalue" ? format : "json";
        settings.IpField = string.IsNullOrWhiteSpace(settings.IpField) ? "ipAddress" : settings.IpField.Trim();
        settings.MacField = string.IsNullOrWhiteSpace(settings.MacField) ? "macAddress" : settings.MacField.Trim();
        settings.HostField = string.IsNullOrWhiteSpace(settings.HostField) ? "hostname" : settings.HostField.Trim();
        settings.CsvDelimiter = string.IsNullOrWhiteSpace(settings.CsvDelimiter) ? "," : settings.CsvDelimiter;
        settings.IpColumn = Clamp(settings.IpColumn, 0, 50);
        settings.MacColumn = Clamp(settings.MacColumn, 0, 50);
        settings.HostColumn = Clamp(settings.HostColumn, 0, 50);
    }

    private static ConnectorSettings Clone(ConnectorSettings settings)
    {
        return new ConnectorSettings
        {
            Enabled = new Dictionary<string, bool>(settings.Enabled, StringComparer.OrdinalIgnoreCase),
            Snmp = new SnmpConnectorSettings
            {
                Hosts = new List<string>(settings.Snmp.Hosts),
                Community = settings.Snmp.Community,
                CommunityCredentialId = settings.Snmp.CommunityCredentialId,
                Port = settings.Snmp.Port,
                SnmpWalkPath = settings.Snmp.SnmpWalkPath,
                TimeoutSeconds = settings.Snmp.TimeoutSeconds
            },
            DhcpHttp = new DhcpHttpConnectorSettings
            {
                Url = settings.DhcpHttp.Url,
                AuthHeader = settings.DhcpHttp.AuthHeader,
                AuthValue = settings.DhcpHttp.AuthValue,
                AuthValueCredentialId = settings.DhcpHttp.AuthValueCredentialId
            },
            Unifi = new UnifiConnectorSettings
            {
                BaseUrl = settings.Unifi.BaseUrl,
                Site = settings.Unifi.Site,
                Username = settings.Unifi.Username,
                Password = settings.Unifi.Password,
                PasswordCredentialId = settings.Unifi.PasswordCredentialId,
                SkipTlsVerify = settings.Unifi.SkipTlsVerify
            },
            TpLink = new LeaseHttpConnectorSettings
            {
                Url = settings.TpLink.Url,
                AuthHeader = settings.TpLink.AuthHeader,
                AuthValue = settings.TpLink.AuthValue,
                AuthValueCredentialId = settings.TpLink.AuthValueCredentialId,
                Format = settings.TpLink.Format,
                IpField = settings.TpLink.IpField,
                MacField = settings.TpLink.MacField,
                HostField = settings.TpLink.HostField,
                CsvDelimiter = settings.TpLink.CsvDelimiter,
                IpColumn = settings.TpLink.IpColumn,
                MacColumn = settings.TpLink.MacColumn,
                HostColumn = settings.TpLink.HostColumn
            },
            Netgear = new LeaseHttpConnectorSettings
            {
                Url = settings.Netgear.Url,
                AuthHeader = settings.Netgear.AuthHeader,
                AuthValue = settings.Netgear.AuthValue,
                AuthValueCredentialId = settings.Netgear.AuthValueCredentialId,
                Format = settings.Netgear.Format,
                IpField = settings.Netgear.IpField,
                MacField = settings.Netgear.MacField,
                HostField = settings.Netgear.HostField,
                CsvDelimiter = settings.Netgear.CsvDelimiter,
                IpColumn = settings.Netgear.IpColumn,
                MacColumn = settings.Netgear.MacColumn,
                HostColumn = settings.Netgear.HostColumn
            },
            Orbi = new LeaseHttpConnectorSettings
            {
                Url = settings.Orbi.Url,
                AuthHeader = settings.Orbi.AuthHeader,
                AuthValue = settings.Orbi.AuthValue,
                AuthValueCredentialId = settings.Orbi.AuthValueCredentialId,
                Format = settings.Orbi.Format,
                IpField = settings.Orbi.IpField,
                MacField = settings.Orbi.MacField,
                HostField = settings.Orbi.HostField,
                CsvDelimiter = settings.Orbi.CsvDelimiter,
                IpColumn = settings.Orbi.IpColumn,
                MacColumn = settings.Orbi.MacColumn,
                HostColumn = settings.Orbi.HostColumn
            },
            Omada = new LeaseHttpConnectorSettings
            {
                Url = settings.Omada.Url,
                AuthHeader = settings.Omada.AuthHeader,
                AuthValue = settings.Omada.AuthValue,
                AuthValueCredentialId = settings.Omada.AuthValueCredentialId,
                Format = settings.Omada.Format,
                IpField = settings.Omada.IpField,
                MacField = settings.Omada.MacField,
                HostField = settings.Omada.HostField,
                CsvDelimiter = settings.Omada.CsvDelimiter,
                IpColumn = settings.Omada.IpColumn,
                MacColumn = settings.Omada.MacColumn,
                HostColumn = settings.Omada.HostColumn
            },
            Asus = new LeaseHttpConnectorSettings
            {
                Url = settings.Asus.Url,
                AuthHeader = settings.Asus.AuthHeader,
                AuthValue = settings.Asus.AuthValue,
                AuthValueCredentialId = settings.Asus.AuthValueCredentialId,
                Format = settings.Asus.Format,
                IpField = settings.Asus.IpField,
                MacField = settings.Asus.MacField,
                HostField = settings.Asus.HostField,
                CsvDelimiter = settings.Asus.CsvDelimiter,
                IpColumn = settings.Asus.IpColumn,
                MacColumn = settings.Asus.MacColumn,
                HostColumn = settings.Asus.HostColumn
            }
        };
    }
}
