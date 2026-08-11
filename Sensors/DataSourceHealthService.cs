using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;

namespace AquacultureMonitoring.Sensors;

public sealed class DataSourceHealthService
{
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _repository;

    public DataSourceHealthService(FarmRuntimeState runtime, FarmRepository repository)
    {
        _runtime = runtime;
        _repository = repository;
    }

    public async Task SetEnabledAsync(string sourceName, bool enabled, CancellationToken ct)
    {
        var health = GetOrCreate(sourceName, enabled);
        health.IsEnabled = enabled;
        if (!enabled) health.IsConnected = false;
        health.UpdatedAtUtc = DateTime.UtcNow;
        _runtime.UpsertDataSourceHealth(health);
        await _repository.UpsertSourceHealthAsync(health, ct);
    }

    public async Task MarkConnectedAsync(string sourceName, bool enabled, CancellationToken ct)
    {
        var health = GetOrCreate(sourceName, enabled);
        health.IsEnabled = enabled;
        health.IsConnected = true;
        health.LastConnectedAtUtc = DateTime.UtcNow;
        health.LastError = null;
        health.UpdatedAtUtc = DateTime.UtcNow;
        _runtime.UpsertDataSourceHealth(health);
        await _repository.UpsertSourceHealthAsync(health, ct);
    }

    public async Task RecordMessageAsync(string sourceName, CancellationToken ct)
    {
        var health = GetOrCreate(sourceName, true);
        health.IsConnected = true;
        health.MessagesReceived++;
        health.LastMessageAtUtc = DateTime.UtcNow;
        health.UpdatedAtUtc = DateTime.UtcNow;
        _runtime.UpsertDataSourceHealth(health);
        await _repository.UpsertSourceHealthAsync(health, ct);
    }

    public async Task MarkErrorAsync(string sourceName, bool enabled, Exception exception, CancellationToken ct)
    {
        var health = GetOrCreate(sourceName, enabled);
        health.IsEnabled = enabled;
        health.IsConnected = false;
        health.LastError = exception.Message;
        health.UpdatedAtUtc = DateTime.UtcNow;
        _runtime.UpsertDataSourceHealth(health);
        await _repository.UpsertSourceHealthAsync(health, ct);
    }

    private DataSourceHealth GetOrCreate(string sourceName, bool enabled)
    {
        return _runtime.DataSources.FirstOrDefault(x => x.Id == sourceName) ?? new DataSourceHealth
        {
            Id = sourceName,
            SourceName = sourceName,
            IsEnabled = enabled
        };
    }
}
