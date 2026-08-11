using System.IO.Ports;
using AquacultureMonitoring.Configuration;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Sensors;

public sealed class ModbusRtuSensorSource : ISensorDataSource
{
    private readonly SensorMessageBus _bus;
    private readonly DataSourceHealthService _health;
    private readonly IOptionsMonitor<SensorConnectionOptions> _options;
    private readonly ILogger<ModbusRtuSensorSource> _logger;

    public ModbusRtuSensorSource(SensorMessageBus bus, DataSourceHealthService health,
        IOptionsMonitor<SensorConnectionOptions> options, ILogger<ModbusRtuSensorSource> logger)
    {
        _bus = bus;
        _health = health;
        _options = options;
        _logger = logger;
    }

    public string SourceName => "modbus-rtu";
    public bool IsEnabled => _options.CurrentValue.ActiveSources.Contains(SourceName, StringComparer.OrdinalIgnoreCase) &&
        _options.CurrentValue.ModbusRtu.Any(x => x.Enabled);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled) return;
        await Task.WhenAll(_options.CurrentValue.ModbusRtu.Where(x => x.Enabled)
            .Select(x => PollPortAsync(x, cancellationToken)));
    }

    private async Task PollPortAsync(ModbusRtuConnectionOptions connection, CancellationToken ct)
    {
        var sourceId = $"{SourceName}:{connection.Id}";
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var port = new SerialPort(connection.PortName, connection.BaudRate,
                    Enum.Parse<Parity>(connection.Parity, true), connection.DataBits,
                    Enum.Parse<StopBits>(connection.StopBits, true))
                {
                    ReadTimeout = connection.TimeoutMilliseconds,
                    WriteTimeout = connection.TimeoutMilliseconds
                };
                port.Open();
                await _health.MarkConnectedAsync(sourceId, true, ct);

                while (port.IsOpen && !ct.IsCancellationRequested)
                {
                    foreach (var mapping in connection.Mappings)
                    {
                        port.DiscardInBuffer();
                        var request = ModbusCodec.BuildRtuReadHoldingRegistersRequest(mapping.UnitId,
                            mapping.RegisterAddress, mapping.RegisterCount);
                        await port.BaseStream.WriteAsync(request, ct);
                        await port.BaseStream.FlushAsync(ct);

                        var responseLength = 5 + mapping.RegisterCount * 2;
                        var response = new byte[responseLength];
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeout.CancelAfter(connection.TimeoutMilliseconds);
                        await port.BaseStream.ReadExactlyAsync(response, timeout.Token);
                        ValidateRtuResponse(response, mapping);
                        var value = ModbusCodec.Decode(response.AsSpan(3, response[2]), mapping);
                        await _bus.PublishAsync(new SensorEnvelope
                        {
                            SensorId = mapping.SensorId,
                            PondId = mapping.PondId,
                            Metric = mapping.Metric,
                            Unit = mapping.Unit,
                            Value = value,
                            TimestampUtc = DateTime.UtcNow,
                            SourceType = SourceName,
                            SourceId = sourceId,
                            SourceAddress = $"modbus-rtu://{connection.PortName}/unit/{mapping.UnitId}/holding/{mapping.RegisterAddress}",
                            Quality = "good"
                        }, ct);
                    }
                    await Task.Delay(Math.Max(100, connection.PollIntervalMilliseconds), ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi polling Modbus RTU {ConnectionId} trên {PortName}.", connection.Id, connection.PortName);
                await _health.MarkErrorAsync(sourceId, true, ex, ct);
                await Task.Delay(Math.Max(500, connection.PollIntervalMilliseconds), ct);
            }
        }
    }

    private static void ValidateRtuResponse(byte[] response, ModbusRegisterMapping mapping)
    {
        if (response[0] != mapping.UnitId || response[1] != 3 || response[2] != mapping.RegisterCount * 2)
            throw new IOException("Modbus RTU response không hợp lệ.");
        var expected = ModbusCodec.ComputeCrc(response.AsSpan(0, response.Length - 2));
        var received = (ushort)(response[^2] | response[^1] << 8);
        if (expected != received) throw new IOException("Modbus RTU CRC không đúng.");
    }
}
