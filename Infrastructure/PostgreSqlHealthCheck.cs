using AquacultureMonitoring.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AquacultureMonitoring.Infrastructure;

/// <summary>Readiness check that opens a pooled connection and executes PostgreSQL SELECT 1.</summary>
public sealed class PostgreSqlHealthCheck(IDbContextFactory<AquaDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL hoạt động.")
                : HealthCheckResult.Unhealthy("PostgreSQL không trả lời truy vấn kiểm tra.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Không kết nối được PostgreSQL.", ex);
        }
    }
}
