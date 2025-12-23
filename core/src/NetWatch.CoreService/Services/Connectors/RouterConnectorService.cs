using Microsoft.Extensions.Options;

namespace NetWatch.CoreService.Services.Connectors;

public sealed class RouterConnectorService : BackgroundService
{
    private readonly ILogger<RouterConnectorService> _logger;
    private readonly ConnectorRegistry _registry;
    private readonly ConnectorSettingsStore _settingsStore;
    private readonly ConnectorOptions _options;

    public RouterConnectorService(
        ILogger<RouterConnectorService> logger,
        ConnectorRegistry registry,
        ConnectorSettingsStore settingsStore,
        IOptions<ConnectorOptions> options)
    {
        _logger = logger;
        _registry = registry;
        _settingsStore = settingsStore;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = _settingsStore.Get();
                foreach (var connector in _registry.All)
                {
                    if (!IsEnabled(settings, connector.Key))
                    {
                        continue;
                    }

                    try
                    {
                        await connector.RunAsync(settings, stoppingToken);
                        _registry.ReportSuccess(connector.Key);
                    }
                    catch (Exception ex)
                    {
                        _registry.ReportFailure(connector.Key, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Router connector cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
    }

    private static bool IsEnabled(ConnectorSettings settings, string key)
    {
        if (settings.Enabled.TryGetValue(key, out var enabled))
        {
            return enabled;
        }

        return false;
    }
}
