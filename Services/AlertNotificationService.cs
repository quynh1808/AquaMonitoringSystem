using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Services;

/// <summary>Sends alert webhooks without coupling sensor ingestion to a specific provider.</summary>
public sealed class AlertNotificationService
{
	private readonly IHttpClientFactory _clients;

	private readonly IOptionsMonitor<AlertNotificationOptions> _options;

	private readonly FarmRepository _repository;

	private readonly ILogger<AlertNotificationService> _logger;

	public AlertNotificationService(IHttpClientFactory clients, IOptionsMonitor<AlertNotificationOptions> options, FarmRepository repository, ILogger<AlertNotificationService> logger)
	{
		_clients = clients;
		_options = options;
		_repository = repository;
		_logger = logger;
	}

	public async Task SendAsync(AlertItem alert, CancellationToken ct)
	{
		AlertNotificationOptions currentValue = _options.CurrentValue;
		if (!currentValue.Enabled)
		{
			return;
		}
		AlertNotification delivery = new AlertNotification
		{
			AlertId = alert.Id,
			PondId = alert.PondId,
			Channel = "webhook",
			Recipient = SafeRecipient(currentValue.WebhookUrl),
			Status = "sending",
			AttemptCount = 1
		};
		try
		{
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
			timeout.CancelAfter(TimeSpan.FromSeconds(currentValue.TimeoutSeconds));
			(await _clients.CreateClient("AlertNotificationService").PostAsJsonAsync(currentValue.WebhookUrl, new { alert.Id, alert.PondId, alert.PondName, alert.Metric, alert.Value, alert.Severity, alert.Message, alert.ProposedSolution, alert.Timestamp }, timeout.Token)).EnsureSuccessStatusCode();
			delivery.Status = "sent";
			delivery.SentAtUtc = DateTime.UtcNow;
		}
		catch (Exception ex) when (!(ex is OperationCanceledException) || !ct.IsCancellationRequested)
		{
			delivery.Status = "failed";
			delivery.Error = ex.Message;
			_logger.LogError(ex, "Không gửi được webhook cho alert {AlertId}.", alert.Id);
		}
		await _repository.InsertAlertNotificationAsync(delivery, ct);
	}

	private static string SafeRecipient(string value)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out var result) || result is null)
		{
			return "configured-webhook";
		}
		return result.GetLeftPart(UriPartial.Path);
	}
}
