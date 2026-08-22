using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Services;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Runtime;

/// <summary>Prepares PostgreSQL before protocol workers consume messages and initialize runtime state.</summary>
public sealed class PostgreSqlBootstrapHostedService(
    PostgreSqlDatabaseInitializer initializer,
    FarmRepository repository,
    FarmRuntimeState runtime,
    UserAccountService accounts,
    ProductionCycleService productionCycles,
    IOptions<SystemBehaviorOptions> behavior,
    ILogger<PostgreSqlBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await initializer.InitializeAsync(cancellationToken);
        await accounts.EnsureBootstrapAdminAsync(cancellationToken);
        await productionCycles.RecalculateAllAsync(cancellationToken);
        runtime.Initialize(await repository.LoadSnapshotAsync(behavior.Value.RuntimeReadingLimit, cancellationToken));
        logger.LogInformation("Đã nạp runtime state và tài khoản quản trị từ PostgreSQL.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
