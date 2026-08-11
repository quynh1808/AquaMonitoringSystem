using System.Security.Authentication;
using System.Text.Json;
using AquacultureMonitoring.Configuration;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace AquacultureMonitoring.Sensors;

public sealed class MqttSensorSource : ISensorDataSource
{
    private readonly SensorMessageBus _bus;
    private readonly DataSourceHealthService _health;
    private readonly IOptionsMonitor<SensorConnectionOptions> _options;
    private readonly ILogger<MqttSensorSource> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public MqttSensorSource(
        SensorMessageBus bus,
        DataSourceHealthService health,
        IOptionsMonitor<SensorConnectionOptions> options,
        ILogger<MqttSensorSource> logger)
    {
        _bus = bus;
        _health = health;
        _options = options;
        _logger = logger;
    }

    public string SourceName => "mqtt";
    public bool IsEnabled => _options.CurrentValue.Mqtt.Enabled &&
        _options.CurrentValue.ActiveSources.Contains(SourceName, StringComparer.OrdinalIgnoreCase);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            var settings = _options.CurrentValue.Mqtt;
            try
            {
                var factory = new MqttClientFactory();
                using var client = factory.CreateMqttClient();
                client.ApplicationMessageReceivedAsync += async args =>
                {
                    var payload = args.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<SensorEnvelope>(payload, _jsonOptions)
                            ?? throw new JsonException("MQTT payload không đúng SensorEnvelope.");
                        envelope.SourceType = SourceName;
                        envelope.SourceId = SourceName;
                        envelope.SourceAddress = $"mqtt://{settings.Host}:{settings.Port}/{args.ApplicationMessage.Topic}";
                        envelope.RawPayload = payload;
                        if (envelope.TimestampUtc == default) envelope.TimestampUtc = DateTime.UtcNow;
                        await _bus.PublishAsync(envelope, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Ứng dụng đang dừng.
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Bỏ qua MQTT payload không hợp lệ tại topic {Topic}.", args.ApplicationMessage.Topic);
                    }
                };

                var builder = new MqttClientOptionsBuilder()
                    .WithClientId(settings.ClientId)
                    .WithTcpServer(settings.Host, settings.Port)
                    .WithCleanSession();

                if (!string.IsNullOrWhiteSpace(settings.Username))
                    builder.WithCredentials(settings.Username, settings.Password);

                if (settings.UseTls)
                {
                    builder.WithTlsOptions(tls =>
                    {
                        tls.WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13);
                        if (settings.AllowUntrustedCertificates)
                            tls.WithCertificateValidationHandler(_ => true);
                    });
                }

                await client.ConnectAsync(builder.Build(), cancellationToken);
                var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(settings.TopicFilter)
                    .Build();
                await client.SubscribeAsync(subscribeOptions, cancellationToken);
                await _health.MarkConnectedAsync(SourceName, true, cancellationToken);
                _logger.LogInformation("MQTT đã subscribe {TopicFilter} tại {Host}:{Port}.", settings.TopicFilter, settings.Host, settings.Port);

                while (client.IsConnected && !cancellationToken.IsCancellationRequested)
                    await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nguồn MQTT bị ngắt; sẽ thử kết nối lại.");
                await _health.MarkErrorAsync(SourceName, true, ex, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.ReconnectDelaySeconds)), cancellationToken);
            }
        }
    }
}
