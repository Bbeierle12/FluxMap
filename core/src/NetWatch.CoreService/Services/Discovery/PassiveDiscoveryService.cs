using System.Net;
using System.Net.Sockets;
using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Discovery;

public sealed class PassiveDiscoveryService : BackgroundService
{
    private readonly DeviceStore _store;
    private readonly ILogger<PassiveDiscoveryService> _logger;

    public PassiveDiscoveryService(DeviceStore store, ILogger<PassiveDiscoveryService> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listeners = new[]
        {
            StartUdpListener("mdns", IPAddress.Parse("224.0.0.251"), 5353, stoppingToken),
            StartUdpListener("llmnr", IPAddress.Parse("224.0.0.252"), 5355, stoppingToken),
            StartUdpListener("nbns", IPAddress.Broadcast, 137, stoppingToken)
        };

        await Task.WhenAll(listeners);
    }

    private async Task StartUdpListener(string source, IPAddress multicast, int port, CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));

            if (!Equals(multicast, IPAddress.Broadcast))
            {
                udp.JoinMulticastGroup(multicast);
            }

            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await udp.ReceiveAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    _logger.LogWarning(ex, "Passive listener error ({Source}).", source);
                    await Task.Delay(500, ct);
                    continue;
                }

                var observation = new Observation
                {
                    Source = source,
                    IpAddress = result.RemoteEndPoint.Address.ToString(),
                    TypeHint = source,
                    ObservedAtUtc = DateTime.UtcNow
                };
                _store.UpsertFromObservation(observation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start passive listener {Source}.", source);
        }
    }
}
