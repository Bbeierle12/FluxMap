using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Connectors;

public sealed class UpnpIgdConnector : IRouterConnector
{
    public string Key => "upnp-igd";
    private readonly DeviceStore _store;
    private readonly ILogger<UpnpIgdConnector> _logger;
    private readonly HttpClient _httpClient = new();

    public UpnpIgdConnector(DeviceStore store, ILogger<UpnpIgdConnector> logger)
    {
        _store = store;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(3);
    }

    public async Task RunAsync(ConnectorSettings settings, CancellationToken ct)
    {
        foreach (var location in await DiscoverLocations(ct))
        {
            try
            {
                await LoadDeviceDescription(location, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "UPnP IGD description fetch failed.");
            }
        }
    }

    private async Task<List<string>> DiscoverLocations(CancellationToken ct)
    {
        var locations = new List<string>();
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.EnableBroadcast = true;
        udp.Client.ReceiveTimeout = 1500;

        var request = """
            M-SEARCH * HTTP/1.1
            HOST: 239.255.255.250:1900
            MAN: "ssdp:discover"
            MX: 1
            ST: urn:schemas-upnp-org:device:InternetGatewayDevice:1
            
            """;
        var bytes = Encoding.ASCII.GetBytes(request.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        await udp.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));

        var stopAt = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < stopAt && !ct.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(ct);
                var response = Encoding.ASCII.GetString(result.Buffer);
                var location = ParseHeader(response, "LOCATION");
                if (!string.IsNullOrWhiteSpace(location))
                {
                    locations.Add(location);
                }
            }
            catch (SocketException)
            {
                break;
            }
        }

        return locations.Distinct().ToList();
    }

    private async Task LoadDeviceDescription(string location, CancellationToken ct)
    {
        var response = await _httpClient.GetStringAsync(location, ct);
        var doc = XDocument.Parse(response);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var device = doc.Descendants(ns + "device").FirstOrDefault();
        if (device is null)
        {
            return;
        }

        var friendly = device.Element(ns + "friendlyName")?.Value;
        var manufacturer = device.Element(ns + "manufacturer")?.Value;
        var model = device.Element(ns + "modelName")?.Value;
        var udn = device.Element(ns + "UDN")?.Value;

        var host = new Uri(location).Host;
        var observation = new Observation
        {
            Source = "upnp-igd",
            IpAddress = host,
            Hostname = friendly,
            Vendor = manufacturer,
            TypeHint = "gateway",
            ServiceHint = model,
            ObservedAtUtc = DateTime.UtcNow
        };
        _store.UpsertFromObservation(observation);
    }

    private static string? ParseHeader(string response, string name)
    {
        foreach (var line in response.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            {
                var idx = line.IndexOf(':');
                if (idx >= 0 && idx < line.Length - 1)
                {
                    return line[(idx + 1)..].Trim();
                }
            }
        }
        return null;
    }
}
