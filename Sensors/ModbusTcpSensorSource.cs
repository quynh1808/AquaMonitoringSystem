using System.Buffers.Binary;
using System.Net.Sockets;
using AquacultureMonitoring.Configuration;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Sensors;

public sealed class ModbusTcpSensorSource : ISensorDataSource
{
    private readonly SensorMessageBus _bus;
    private readonly DataSourceHealthService _health;
    private readonly IOptionsMonitor<SensorConnectionOptions> _options;
    private readonly ILogger<ModbusTcpSensorSource> _logger;

    public ModbusTcpSensorSource(SensorMessageBus bus, DataSourceHealthService health,
        IOptionsMonitor<SensorConnectionOptions> options, ILogger<ModbusTcpSensorSource> logger)
    {
        _bus = bus;
        _health = health;
        _options = options;
        _logger = logger;
    }

    public string SourceName => "modbus-tcp";
    public bool IsEnabled => _options.CurrentValue.ActiveSources.Contains(SourceName, StringComparer.OrdinalIgnoreCase) &&
        _options.CurrentValue.ModbusTcp.Any(x => x.Enabled);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled) return;
        var tasks = _options.CurrentValue.ModbusTcp.Where(x => x.Enabled)
            .Select(x => PollConnectionAsync(x, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task PollConnectionAsync(ModbusTcpConnectionOptions connection, CancellationToken ct)
    {
        var sourceId = $"{SourceName}:{connection.Id}";
        ushort transactionId = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(connection.TimeoutMilliseconds);
                await client.ConnectAsync(connection.Host, connection.Port, timeout.Token);
                await _health.MarkConnectedAsync(sourceId, true, ct);
                using var stream = client.GetStream();

                while (client.Connected && !ct.IsCancellationRequested)
                {
                    foreach (var mapping in connection.Mappings)
                    {
                        var transaction = unchecked(++transactionId);
                        var request = ModbusCodec.BuildReadHoldingRegistersRequest(mapping.UnitId, mapping.RegisterAddress,
                            mapping.RegisterCount, transaction);
                        await stream.WriteAsync(request, ct);

                        var header = new byte[7];
                        await stream.ReadExactlyAsync(header, ct);
                        var receivedTransaction = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
                        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
                        var expectedLength = 3 + mapping.RegisterCount * 2;
                        if (receivedTransaction != transaction || length != expectedLength)
                            throw new IOException("Modbus TCP response không khớp transaction hoặc độ dài mong đợi.");

                        var pdu = new byte[length - 1];
                        await stream.ReadExactlyAsync(pdu, ct);
                        if (pdu[0] == (3 | 0x80)) throw new IOException($"Modbus exception code {pdu[1]}.");
                        var expectedByteCount = mapping.RegisterCount * 2;
                        if (pdu[0] != 3 || pdu[1] != expectedByteCount || pdu.Length != 2 + expectedByteCount)
                            throw new IOException("Modbus TCP response không hợp lệ.");
                        var value = ModbusCodec.Decode(pdu.AsSpan(2, expectedByteCount), mapping);
                        await _bus.PublishAsync(ToEnvelope(connection, mapping, value), ct);
                    }
                    await Task.Delay(Math.Max(100, connection.PollIntervalMilliseconds), ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi polling Modbus TCP {ConnectionId}.", connection.Id);
                await _health.MarkErrorAsync(sourceId, true, ex, ct);
                await Task.Delay(Math.Max(500, connection.PollIntervalMilliseconds), ct);
            }
        }
    }

    private static SensorEnvelope ToEnvelope(ModbusTcpConnectionOptions connection, ModbusRegisterMapping mapping, double value) => new()
    {
        SensorId = mapping.SensorId,
        PondId = mapping.PondId,
        Metric = mapping.Metric,
        Unit = mapping.Unit,
        Value = value,
        TimestampUtc = DateTime.UtcNow,
        SourceType = "modbus-tcp",
        SourceId = $"modbus-tcp:{connection.Id}",
        SourceAddress = $"modbus-tcp://{connection.Host}:{connection.Port}/unit/{mapping.UnitId}/holding/{mapping.RegisterAddress}",
        Quality = "good"
    };
}
