using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Services;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Runtime;

public sealed class MongoBootstrapHostedService : IHostedService
{
    private readonly MongoDatabaseInitializer _initializer;
    private readonly FarmRepository _repository;
    private readonly FarmRuntimeState _runtime;
    private readonly UserAccountService _accounts;
    private readonly ProductionCycleService _productionCycles;
    private readonly SystemBehaviorOptions _behavior;
    private readonly ILogger<MongoBootstrapHostedService> _logger;

    public MongoBootstrapHostedService(
        MongoDatabaseInitializer initializer,
        FarmRepository repository,
        FarmRuntimeState runtime,
        UserAccountService accounts,
        ProductionCycleService productionCycles,
        IOptions<SystemBehaviorOptions> behavior,
        ILogger<MongoBootstrapHostedService> logger)
    {
        _initializer = initializer;
        _repository = repository;
        _runtime = runtime;
        _accounts = accounts;
        _productionCycles = productionCycles;
        _behavior = behavior.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _initializer.InitializeAsync(cancellationToken);
        await _accounts.EnsureBootstrapAdminAsync(cancellationToken);
        // Rebuild derived FCR/biomass values before exposing the in-memory dashboard projection.
        await _productionCycles.RecalculateAllAsync(cancellationToken);
        var snapshot = await _repository.LoadSnapshotAsync(_behavior.RuntimeReadingLimit, cancellationToken);
        _runtime.Initialize(snapshot);
        _logger.LogInformation("Đã nạp runtime state và tài khoản quản trị từ MongoDB.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
