namespace NetWatch.CoreService.Services.Connectors;

public sealed class ConnectorSettings
{
    public Dictionary<string, bool> Enabled { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["upnp-igd"] = true,
        ["snmp"] = false,
        ["dhcp-http"] = false,
        ["unifi"] = false,
        ["tplink"] = false,
        ["netgear"] = false,
        ["orbi"] = false,
        ["omada"] = false,
        ["asus"] = false
    };

    public SnmpConnectorSettings Snmp { get; set; } = new();
    public DhcpHttpConnectorSettings DhcpHttp { get; set; } = new();
    public UnifiConnectorSettings Unifi { get; set; } = new();
    public LeaseHttpConnectorSettings TpLink { get; set; } = new();
    public LeaseHttpConnectorSettings Netgear { get; set; } = new();
    public LeaseHttpConnectorSettings Orbi { get; set; } = new();
    public LeaseHttpConnectorSettings Omada { get; set; } = new();
    public LeaseHttpConnectorSettings Asus { get; set; } = new();
}

public sealed class SnmpConnectorSettings
{
    public List<string> Hosts { get; set; } = new();
    public string Community { get; set; } = "public";
    public string? CommunityCredentialId { get; set; }
    public int Port { get; set; } = 161;
    public string SnmpWalkPath { get; set; } = "snmpwalk";
    public int TimeoutSeconds { get; set; } = 3;
}

public sealed class DhcpHttpConnectorSettings
{
    public string Url { get; set; } = string.Empty;
    public string AuthHeader { get; set; } = string.Empty;
    public string AuthValue { get; set; } = string.Empty;
    public string? AuthValueCredentialId { get; set; }
}

public sealed class UnifiConnectorSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Site { get; set; } = "default";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PasswordCredentialId { get; set; }
    public bool SkipTlsVerify { get; set; }
}

public sealed class LeaseHttpConnectorSettings
{
    public string Url { get; set; } = string.Empty;
    public string AuthHeader { get; set; } = string.Empty;
    public string AuthValue { get; set; } = string.Empty;
    public string? AuthValueCredentialId { get; set; }
    public string Format { get; set; } = "json";
    public string IpField { get; set; } = "ipAddress";
    public string MacField { get; set; } = "macAddress";
    public string HostField { get; set; } = "hostname";
    public string CsvDelimiter { get; set; } = ",";
    public int IpColumn { get; set; } = 0;
    public int MacColumn { get; set; } = 1;
    public int HostColumn { get; set; } = 2;
}
