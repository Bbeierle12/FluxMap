using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Options;
using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Discovery;

public sealed class ActiveDiscoveryService : BackgroundService
{
    private readonly DeviceStore _store;
    private readonly ILogger<ActiveDiscoveryService> _logger;
    private readonly SettingsStore _settingsStore;

    public ActiveDiscoveryService(DeviceStore store, ILogger<ActiveDiscoveryService> logger, SettingsStore settingsStore)
    {
        _store = store;
        _logger = logger;
        _settingsStore = settingsStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunScan(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Active discovery scan failed.");
            }

            var options = _settingsStore.Get();
            await Task.Delay(TimeSpan.FromSeconds(options.ScanIntervalSeconds), stoppingToken);
        }
    }

    private async Task RunScan(CancellationToken ct)
    {
        var options = _settingsStore.Get();
        var subnets = GetLocalSubnets();
        foreach (var subnet in subnets)
        {
            await ScanSubnet(subnet, options, ct);
        }

        if (options.EnableSsdp)
        {
            await RunSsdpProbe(ct);
        }
    }

    private async Task ScanSubnet(SubnetInfo subnet, DiscoveryOptions options, CancellationToken ct)
    {
        if (subnet.HostCount > options.MaxHostsPerSubnet)
        {
            _logger.LogInformation("Skipping subnet {Subnet} with {Hosts} hosts (limit {Limit}).",
                subnet.Cidr, subnet.HostCount, options.MaxHostsPerSubnet);
            return;
        }

        using var semaphore = new SemaphoreSlim(options.MaxConcurrentPings);
        var tasks = new List<Task>();

        foreach (var ip in subnet.Hosts)
        {
            await semaphore.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await ProbeHost(ip, options, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProbeHost(IPAddress ip, DiscoveryOptions options, CancellationToken ct)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(ip, options.PingTimeoutMs);
        if (reply.Status != IPStatus.Success)
        {
            return;
        }

        var mac = TryResolveMac(ip);
        var observation = new Observation
        {
            Source = "active-ping",
            IpAddress = ip.ToString(),
            MacAddress = mac,
            ObservedAtUtc = DateTime.UtcNow
        };

        _store.UpsertFromObservation(observation);
        await ProbeTcpPorts(ip, options, ct);
    }

    private async Task RunSsdpProbe(CancellationToken ct)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.EnableBroadcast = true;
        udp.Client.ReceiveTimeout = 1500;

        var request = """
            M-SEARCH * HTTP/1.1
            HOST: 239.255.255.250:1900
            MAN: "ssdp:discover"
            MX: 1
            ST: ssdp:all
            
            """;
        var bytes = Encoding.ASCII.GetBytes(request.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        var endpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
        await udp.SendAsync(bytes, bytes.Length, endpoint);

        var stopAt = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < stopAt && !ct.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(ct);
                var response = Encoding.ASCII.GetString(result.Buffer);
                var server = ParseHeader(response, "SERVER");
                var usn = ParseHeader(response, "USN");

                var observation = new Observation
                {
                    Source = "ssdp",
                    IpAddress = result.RemoteEndPoint.Address.ToString(),
                    Hostname = usn,
                    Vendor = server,
                    TypeHint = "upnp-device",
                    ObservedAtUtc = DateTime.UtcNow
                };
                _store.UpsertFromObservation(observation);
            }
            catch (SocketException)
            {
                break;
            }
        }
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

    private async Task ProbeTcpPorts(IPAddress ip, DiscoveryOptions options, CancellationToken ct)
    {
        if (options.TcpPorts is null || options.TcpPorts.Count == 0)
        {
            return;
        }

        foreach (var port in options.TcpPorts)
        {
            if (port <= 0 || port > 65535)
            {
                continue;
            }

            if (await IsPortOpen(ip, port, options.TcpConnectTimeoutMs, ct))
            {
                var observation = new Observation
                {
                    Source = "active-tcp",
                    IpAddress = ip.ToString(),
                    ServiceHint = $"tcp/{port}",
                    ObservedAtUtc = DateTime.UtcNow
                };
                _store.UpsertFromObservation(observation);
            }
        }
    }

    private static async Task<bool> IsPortOpen(IPAddress ip, int port, int timeoutMs, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            await client.ConnectAsync(ip, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<SubnetInfo> GetLocalSubnets()
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
            foreach (var unicast in props.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (unicast.IPv4Mask is null)
                {
                    continue;
                }

                yield return SubnetInfo.From(unicast.Address, unicast.IPv4Mask);
            }
        }
    }

    private static string? TryResolveMac(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return null;
        }

        var addr = BitConverter.ToInt32(ip.GetAddressBytes(), 0);
        var mac = new byte[6];
        var len = mac.Length;

        var result = SendARP(addr, 0, mac, ref len);
        if (result != 0 || len == 0)
        {
            return null;
        }

        return string.Join(":", mac.Take(len).Select(b => b.ToString("X2")));
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(int destIp, int srcIp, byte[] pMacAddr, ref int phyAddrLen);

    private sealed class SubnetInfo
    {
        public required string Cidr { get; init; }
        public required IEnumerable<IPAddress> Hosts { get; init; }
        public required int HostCount { get; init; }

        public static SubnetInfo From(IPAddress ip, IPAddress mask)
        {
            var ipBytes = ip.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            var networkBytes = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }

            var network = new IPAddress(networkBytes);
            var hostCount = CountHosts(maskBytes);
            var hosts = EnumerateHosts(networkBytes, maskBytes);
            var cidr = $"{network}/{MaskToCidr(maskBytes)}";

            return new SubnetInfo
            {
                Cidr = cidr,
                Hosts = hosts,
                HostCount = hostCount
            };
        }

        private static int CountHosts(byte[] maskBytes)
        {
            var hostBits = 0;
            foreach (var b in maskBytes)
            {
                hostBits += 8 - CountBits(b);
            }

            var hosts = (int)Math.Pow(2, hostBits);
            return Math.Max(0, hosts - 2);
        }

        private static int CountBits(byte b)
        {
            var count = 0;
            while (b > 0)
            {
                count += b & 1;
                b >>= 1;
            }
            return count;
        }

        private static int MaskToCidr(byte[] maskBytes)
        {
            var cidr = 0;
            foreach (var b in maskBytes)
            {
                cidr += CountBits(b);
            }
            return cidr;
        }

        private static IEnumerable<IPAddress> EnumerateHosts(byte[] network, byte[] mask)
        {
            var start = (uint)IPAddressToUint(network) + 1;
            var end = (uint)(IPAddressToUint(BroadcastAddress(network, mask)) - 1);
            for (var current = start; current <= end; current++)
            {
                yield return UintToIPAddress(current);
            }
        }

        private static byte[] BroadcastAddress(byte[] network, byte[] mask)
        {
            var broadcast = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                broadcast[i] = (byte)(network[i] | ~mask[i]);
            }
            return broadcast;
        }

        private static uint IPAddressToUint(byte[] bytes)
        {
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static IPAddress UintToIPAddress(uint address)
        {
            return new IPAddress(new[]
            {
                (byte)((address >> 24) & 0xFF),
                (byte)((address >> 16) & 0xFF),
                (byte)((address >> 8) & 0xFF),
                (byte)(address & 0xFF)
            });
        }
    }
}
