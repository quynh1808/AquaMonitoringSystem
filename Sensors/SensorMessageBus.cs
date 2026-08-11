using System.Threading.Channels;

namespace AquacultureMonitoring.Sensors;

/// <summary>
/// Bounded ingestion channel. Publishers wait when the consumer is behind so telemetry is not
/// silently discarded; upstream protocol clients can then apply their own retry/reconnect policy.
/// </summary>
public sealed class SensorMessageBus
{
    private readonly Channel<SensorEnvelope> _channel = Channel.CreateBounded<SensorEnvelope>(
        new BoundedChannelOptions(20000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask PublishAsync(SensorEnvelope envelope, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(envelope, cancellationToken);

    public IAsyncEnumerable<SensorEnvelope> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
