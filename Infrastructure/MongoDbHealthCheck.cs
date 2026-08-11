using AquacultureMonitoring.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using MongoDB.Bson;
namespace AquacultureMonitoring.Infrastructure;

public sealed class MongoDbHealthCheck : IHealthCheck
{
    private readonly MongoContext _context;

    public MongoDbHealthCheck(MongoContext context) => _context = context;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB hoạt động.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Không kết nối được MongoDB.", ex);
        }
    }
}
