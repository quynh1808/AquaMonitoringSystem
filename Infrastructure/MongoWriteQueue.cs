using System.Threading.Channels;

namespace AquacultureMonitoring.Infrastructure;

public interface IMongoWriteQueue
{
    /// <summary>
    /// Applies backpressure when the bounded queue is full instead of silently dropping a write.
    /// </summary>
    ValueTask EnqueueAsync(Func<CancellationToken, Task> workItem, CancellationToken cancellationToken = default);
}

public sealed class MongoWriteQueue : BackgroundService, IMongoWriteQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _channel = Channel.CreateBounded<Func<CancellationToken, Task>>(
        new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly ILogger<MongoWriteQueue> _logger;

    public MongoWriteQueue(ILogger<MongoWriteQueue> logger) => _logger = logger;

    public ValueTask EnqueueAsync(Func<CancellationToken, Task> workItem, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(workItem, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await workItem(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể ghi một thay đổi vào MongoDB.");
            }
        }
    }
}
