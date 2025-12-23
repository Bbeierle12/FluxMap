using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace NetWatch.CoreService.Services;

public sealed class EventHub
{
    private readonly Channel<StreamMessage> _channel = Channel.CreateUnbounded<StreamMessage>();

    public IAsyncEnumerable<StreamMessage> Stream(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public void Publish(StreamMessage message)
    {
        _channel.Writer.TryWrite(message);
    }

    public void PublishDevice(object device)
    {
        Publish(new StreamMessage("device", JsonSerializer.Serialize(device)));
    }

    public void PublishEvent(object evt)
    {
        Publish(new StreamMessage("event", JsonSerializer.Serialize(evt)));
    }
}

public sealed record StreamMessage(string Type, string Data);
