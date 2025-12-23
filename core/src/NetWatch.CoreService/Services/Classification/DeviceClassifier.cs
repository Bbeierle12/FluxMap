using NetWatch.CoreService.Models;

namespace NetWatch.CoreService.Services.Classification;

public sealed class DeviceClassifier
{
    private readonly OuiVendorLookup _oui;

    public DeviceClassifier(OuiVendorLookup oui)
    {
        _oui = oui;
    }

    public ClassificationResult Classify(Device device, Observation observation)
    {
        var reasons = new List<string>();
        var vendor = device.Vendor ?? _oui.Lookup(observation.MacAddress ?? device.MacAddress);
        if (!string.IsNullOrWhiteSpace(vendor))
        {
            reasons.Add($"oui:{vendor}");
        }

        var type = InferType(device, observation, reasons, out var confidence);
        return new ClassificationResult(type, vendor, confidence, reasons);
    }

    public ClassificationResult Classify(Device device, IEnumerable<DeviceObservation> observations)
    {
        var reasons = new List<string>();
        var vendor = device.Vendor ?? _oui.Lookup(device.MacAddress);
        if (!string.IsNullOrWhiteSpace(vendor))
        {
            reasons.Add($"oui:{vendor}");
        }

        var bestType = device.TypeGuess;
        var bestConfidence = 0.1;
        foreach (var obs in observations)
        {
            var temp = new Observation
            {
                IpAddress = obs.IpAddress,
                MacAddress = obs.MacAddress,
                Hostname = obs.Hostname,
                Vendor = obs.Vendor,
                TypeHint = obs.TypeHint,
                ServiceHint = obs.ServiceHint
            };
            var result = InferType(device, temp, reasons, out var confidence);
            if (!string.IsNullOrWhiteSpace(result) && confidence > bestConfidence)
            {
                bestType = result;
                bestConfidence = confidence;
            }
        }

        return new ClassificationResult(bestType, vendor, bestConfidence, reasons);
    }

    private static string? InferType(Device device, Observation observation, List<string> reasons, out double confidence)
    {
        confidence = 0.1;
        var hostname = (observation.Hostname ?? device.Hostname ?? string.Empty).ToLowerInvariant();
        var service = (observation.ServiceHint ?? string.Empty).ToLowerInvariant();
        var typeHint = (observation.TypeHint ?? string.Empty).ToLowerInvariant();

        if (service.Contains("tcp/554") || service.Contains("rtsp"))
        {
            reasons.Add("service:rtsp");
            confidence = 0.7;
            return "camera";
        }
        if (service.Contains("tcp/9100") || hostname.Contains("printer"))
        {
            reasons.Add("service:printer");
            confidence = 0.7;
            return "printer";
        }
        if (service.Contains("tcp/445") || service.Contains("tcp/139"))
        {
            reasons.Add("service:smb");
            confidence = 0.5;
            return "computer";
        }
        if (service.Contains("tcp/3389"))
        {
            reasons.Add("service:rdp");
            confidence = 0.6;
            return "computer";
        }
        if (hostname.Contains("cam") || hostname.Contains("camera"))
        {
            reasons.Add("hostname:camera");
            confidence = 0.6;
            return "camera";
        }
        if (hostname.Contains("iphone") || hostname.Contains("ipad") || hostname.Contains("android"))
        {
            reasons.Add("hostname:mobile");
            confidence = 0.5;
            return "phone";
        }
        if (hostname.Contains("tv") || hostname.Contains("roku") || hostname.Contains("chromecast"))
        {
            reasons.Add("hostname:tv");
            confidence = 0.5;
            return "tv";
        }
        if (typeHint.Contains("gateway"))
        {
            reasons.Add("type:gateway");
            confidence = 0.6;
            return "router";
        }

        return device.TypeGuess;
    }
}

public sealed record ClassificationResult(string? TypeGuess, string? Vendor, double Confidence, IReadOnlyCollection<string> Reasons);
