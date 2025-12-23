using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Security;

public sealed class RiskScorer
{
    public RiskResult Score(Device device, IEnumerable<DeviceObservation> observations)
    {
        var reasons = new List<string>();
        var score = 0.0;

        foreach (var obs in observations)
        {
            var service = (obs.ServiceHint ?? string.Empty).ToLowerInvariant();
            if (service.Contains("tcp/23"))
            {
                score += 0.4;
                reasons.Add("telnet-open");
            }
            if (service.Contains("tcp/3389"))
            {
                score += 0.3;
                reasons.Add("rdp-open");
            }
            if (service.Contains("tcp/445"))
            {
                score += 0.2;
                reasons.Add("smb-open");
            }
            if (service.Contains("tcp/80"))
            {
                score += 0.1;
                reasons.Add("http-open");
            }
        }

        var type = (device.TypeGuess ?? string.Empty).ToLowerInvariant();
        if (type == "camera")
        {
            score += 0.1;
            reasons.Add("camera-device");
        }

        score = Math.Min(1.0, score);
        return new RiskResult
        {
            Score = score,
            Level = score >= 0.7 ? "high" : score >= 0.4 ? "medium" : "low",
            Reasons = reasons.Distinct().ToList()
        };
    }
}

public sealed class RiskResult
{
    public double Score { get; set; }
    public string Level { get; set; } = "low";
    public List<string> Reasons { get; set; } = new();
}
