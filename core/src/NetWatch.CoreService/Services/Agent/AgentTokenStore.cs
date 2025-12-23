using System.Text.Json;

namespace NetWatch.CoreService.Services.Agent;

public sealed class AgentTokenStore
{
    private readonly string _path;
    private readonly ILogger<AgentTokenStore> _logger;
    private readonly object _lock = new();
    private readonly List<AgentToken> _tokens = new();

    public AgentTokenStore(string path, ILogger<AgentTokenStore> logger)
    {
        _path = path;
        _logger = logger;
        Load();
    }

    public IReadOnlyCollection<AgentToken> List()
    {
        lock (_lock)
        {
            return _tokens.Select(t => t).ToList().AsReadOnly();
        }
    }

    public IReadOnlyCollection<string> GetTokenValues()
    {
        lock (_lock)
        {
            return _tokens.Select(t => t.Token).ToList().AsReadOnly();
        }
    }

    public AgentToken Add(string name)
    {
        var token = new AgentToken
        {
            Token = GenerateToken(),
            Name = string.IsNullOrWhiteSpace(name) ? "agent" : name.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        lock (_lock)
        {
            _tokens.Add(token);
            Save();
        }

        return token;
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var items = JsonSerializer.Deserialize<List<AgentToken>>(json);
            if (items is null)
            {
                return;
            }

            _tokens.Clear();
            _tokens.AddRange(items);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load agent tokens store.");
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_tokens, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save agent tokens store.");
        }
    }

    private static string GenerateToken()
    {
        var bytes = new byte[24];
        Random.Shared.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class AgentToken
{
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
