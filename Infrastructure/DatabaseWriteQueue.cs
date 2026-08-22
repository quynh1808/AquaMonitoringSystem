using System.Threading.Channels;

namespace AquacultureMonitoring.Infrastructure;

public interface IDatabaseWriteQueue
{
    ValueTask EnqueueAsync(Func<CancellationToken, Task> workItem, CancellationToken cancellationToken = default);
}

/// <summary>Bounded write queue shared by protocol workers; full capacity applies backpressure.</summary>
public sealed class DatabaseWriteQueue : BackgroundService, IDatabaseWriteQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _channel = Channel.CreateBounded<Func<CancellationToken, Task>>(
        new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });
    private readonly ILogger<DatabaseWriteQueue> _logger;

    public DatabaseWriteQueue(ILogger<DatabaseWriteQueue> logger) => _logger = logger;
    public ValueTask EnqueueAsync(Func<CancellationToken, Task> workItem, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(workItem, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try { await workItem(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Không thể ghi một thay đổi vào PostgreSQL."); }
        }
    }
}
