using System.Globalization;

namespace NetWatch.CoreService.Services.Classification;

public sealed class OuiVendorLookup
{
    private readonly Dictionary<string, string> _map;

    public OuiVendorLookup(string? path)
    {
        _map = Load(path);
    }

    public string? Lookup(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac))
        {
            return null;
        }

        var normalized = Normalize(mac);
        if (normalized.Length < 6)
        {
            return null;
        }

        var oui = normalized[..6];
        return _map.TryGetValue(oui, out var vendor) ? vendor : null;
    }

    private static Dictionary<string, string> Load(string? path)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return map;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("OUI", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(',', 2);
            if (parts.Length < 2)
            {
                continue;
            }

            var oui = Normalize(parts[0]);
            var vendor = parts[1].Trim();
            if (oui.Length >= 6 && !string.IsNullOrWhiteSpace(vendor))
            {
                map[oui[..6]] = vendor;
            }
        }

        return map;
    }

    private static string Normalize(string input)
    {
        var chars = input.Where(c => Uri.IsHexDigit(c)).Select(c => char.ToUpperInvariant(c)).ToArray();
        return new string(chars);
    }
}
