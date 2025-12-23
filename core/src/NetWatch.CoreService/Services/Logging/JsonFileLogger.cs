using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NetWatch.CoreService.Services.Logging;

public sealed class JsonFileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly LogLevel _minLevel;
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private readonly object _lock = new();

    public JsonFileLoggerProvider(string path, LogLevel minLevel, long maxBytes, int maxFiles)
    {
        _path = path;
        _minLevel = minLevel;
        _maxBytes = maxBytes;
        _maxFiles = maxFiles;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public ILogger CreateLogger(string categoryName) => new JsonFileLogger(_path, _minLevel, _maxBytes, _maxFiles, categoryName, _lock);

    public void Dispose()
    {
    }
}

public sealed class JsonFileLogger : ILogger
{
    private readonly string _path;
    private readonly LogLevel _minLevel;
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private readonly string _category;
    private readonly object _lock;

    public JsonFileLogger(string path, LogLevel minLevel, long maxBytes, int maxFiles, string category, object writeLock)
    {
        _path = path;
        _minLevel = minLevel;
        _maxBytes = maxBytes;
        _maxFiles = maxFiles;
        _category = category;
        _lock = writeLock;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var entry = new LogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = logLevel.ToString(),
            Category = _category,
            EventId = eventId.Id,
            Message = formatter(state, exception),
            Exception = exception?.ToString()
        };

        var json = JsonSerializer.Serialize(entry);
        lock (_lock)
        {
            RotateIfNeeded();
            File.AppendAllText(_path, json + Environment.NewLine);
        }
    }

    private void RotateIfNeeded()
    {
        if (_maxBytes <= 0)
        {
            return;
        }

        var info = new FileInfo(_path);
        if (info.Exists && info.Length >= _maxBytes)
        {
            var dir = Path.GetDirectoryName(_path) ?? ".";
            var file = Path.GetFileNameWithoutExtension(_path);
            var ext = Path.GetExtension(_path);
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var rotated = Path.Combine(dir, $"{file}.{stamp}{ext}");
            File.Move(_path, rotated, true);
            CleanupOldFiles(dir, file, ext);
        }
    }

    private void CleanupOldFiles(string dir, string file, string ext)
    {
        if (_maxFiles <= 0)
        {
            return;
        }

        var files = Directory.GetFiles(dir, $"{file}.*{ext}")
            .OrderByDescending(f => f)
            .ToList();

        for (var i = _maxFiles; i < files.Count; i++)
        {
            try
            {
                File.Delete(files[i]);
            }
            catch
            {
            }
        }
    }

    private sealed class LogEntry
    {
        public DateTime TimestampUtc { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
    }
}
