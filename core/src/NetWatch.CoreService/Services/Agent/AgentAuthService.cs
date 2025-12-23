using System.Security.Cryptography;
using System.Text;

namespace NetWatch.CoreService.Services.Agent;

public sealed class AgentAuthService
{
    private readonly IConfiguration _config;
    private readonly AgentTokenStore _tokenStore;

    public AgentAuthService(IConfiguration config, AgentTokenStore tokenStore)
    {
        _config = config;
        _tokenStore = tokenStore;
    }

    public bool Validate(HttpRequest request, string body)
    {
        var tokens = new List<string>();
        var configTokens = _config.GetSection("Agent:Tokens").Get<string[]>();
        if (configTokens is not null)
        {
            tokens.AddRange(configTokens.Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        var single = _config.GetValue<string>("Agent:Token");
        if (!string.IsNullOrWhiteSpace(single))
        {
            tokens.Add(single);
        }

        tokens.AddRange(_tokenStore.GetTokenValues());

        var requiresToken = tokens.Count > 0;
        if (requiresToken)
        {
            if (!request.Headers.TryGetValue("X-NetWatch-Token", out var provided) ||
                !tokens.Contains(provided.ToString(), StringComparer.Ordinal))
            {
                return false;
            }
        }

        var hmacSecret = _config.GetValue<string>("Agent:HmacSecret");
        var requiresHmac = !string.IsNullOrWhiteSpace(hmacSecret);
        if (requiresHmac)
        {
            if (!request.Headers.TryGetValue("X-NetWatch-Timestamp", out var timestamp) ||
                !request.Headers.TryGetValue("X-NetWatch-Signature", out var signature))
            {
                return false;
            }

            if (!ValidateTimestamp(timestamp.ToString(), _config.GetValue<int?>("Agent:MaxSkewSeconds") ?? 300))
            {
                return false;
            }

            var payload = $"{request.Method}\n{request.Path}\n{timestamp}\n{body}";
            var expected = ComputeHmac(hmacSecret!, payload);
            if (!FixedTimeEquals(expected, signature.ToString()))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateTimestamp(string value, int maxSkewSeconds)
    {
        if (!long.TryParse(value, out var ts))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var skew = Math.Abs(now - ts);
        return skew <= maxSkewSeconds;
    }

    private static string ComputeHmac(string secret, string message)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(message);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string expectedHex, string providedHex)
    {
        if (expectedHex.Length != providedHex.Length)
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(expectedHex);
        var b = Encoding.UTF8.GetBytes(providedHex);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
