using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NetWatch.CoreService.Services.Connectors;

public sealed class CredentialVault
{
    private readonly string _path;
    private readonly ILogger<CredentialVault> _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, StoredCredential> _creds = new(StringComparer.OrdinalIgnoreCase);

    public CredentialVault(string path, ILogger<CredentialVault> logger)
    {
        _path = path;
        _logger = logger;
        Load();
    }

    public IReadOnlyCollection<CredentialInfo> List()
    {
        lock (_lock)
        {
            return _creds.Values
                .Select(c => new CredentialInfo(c.Id, c.Name, c.Purpose, c.CreatedAtUtc))
                .ToList()
                .AsReadOnly();
        }
    }

    public CredentialInfo Create(string name, string purpose, string secret)
    {
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var encrypted = Protect(secret);
        var stored = new StoredCredential
        {
            Id = id,
            Name = name,
            Purpose = purpose,
            CreatedAtUtc = now,
            EncryptedValue = Convert.ToBase64String(encrypted)
        };

        lock (_lock)
        {
            _creds[id] = stored;
            Save();
        }

        return new CredentialInfo(id, name, purpose, now);
    }

    public bool TryGetSecret(string? id, out string secret)
    {
        secret = string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        lock (_lock)
        {
            if (!_creds.TryGetValue(id, out var stored))
            {
                return false;
            }

            var bytes = Convert.FromBase64String(stored.EncryptedValue);
            secret = Unprotect(bytes);
            return true;
        }
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            if (!_creds.Remove(id))
            {
                return false;
            }

            Save();
            return true;
        }
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
            var items = JsonSerializer.Deserialize<List<StoredCredential>>(json);
            if (items is null)
            {
                return;
            }

            foreach (var item in items)
            {
                _creds[item.Id] = item;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load credentials store.");
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_creds.Values, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save credentials store.");
        }
    }

    private static byte[] Protect(string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        return ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
    }

    private static string Unprotect(byte[] secret)
    {
        var bytes = ProtectedData.Unprotect(secret, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    private sealed class StoredCredential
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string EncryptedValue { get; set; } = string.Empty;
    }
}

public sealed record CredentialInfo(string Id, string Name, string Purpose, DateTime CreatedAtUtc);
