namespace NetWatch.CoreService.Services;

public sealed class LeaveDetectorService : BackgroundService
{
    private readonly DeviceStore _store;
    private readonly ILogger<LeaveDetectorService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _staleThreshold = TimeSpan.FromMinutes(2);

    public LeaveDetectorService(DeviceStore store, ILogger<LeaveDetectorService> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var marked = _store.MarkOfflineIfStale(_staleThreshold);
            if (marked > 0)
            {
                _logger.LogInformation("Marked {Count} devices offline due to stale last_seen.", marked);
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
