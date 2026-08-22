using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Runtime;

/// <summary>
/// Applies explicit retention. Expired operational logs are removed daily; audit and production
/// records are intentionally excluded because they are part of the permanent traceability record.
/// </summary>
public sealed class PostgreSqlRetentionWorker(
    IDbContextFactory<AquaDbContext> factory,
    IOptions<SystemBehaviorOptions> behavior,
    ILogger<PostgreSqlRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(stoppingToken);
                var readingCutoff = DateTime.UtcNow.AddDays(-behavior.Value.ReadingRetentionDays);
                var readings = await db.SensorReadings.Where(x => x.Timestamp < readingCutoff).ExecuteDeleteAsync(stoppingToken);
                var logs = await db.ConnectionLogs.Where(x => x.ExpiresAtUtc < DateTime.UtcNow).ExecuteDeleteAsync(stoppingToken);
                var health = await db.SensorHealthEvents.Where(x => x.ExpiresAtUtc < DateTime.UtcNow).ExecuteDeleteAsync(stoppingToken);
                var quarantine = await db.QuarantinedNodeMessages.Where(x => x.ExpiresAtUtc < DateTime.UtcNow).ExecuteDeleteAsync(stoppingToken);
                if (readings + logs + health + quarantine > 0)
                    logger.LogInformation("PostgreSQL retention đã xóa {Count} bản ghi hết hạn.", readings + logs + health + quarantine);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "PostgreSQL retention thất bại; sẽ thử lại ở chu kỳ kế tiếp."); }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
