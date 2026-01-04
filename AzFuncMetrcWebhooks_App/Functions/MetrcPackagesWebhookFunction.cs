using AzFuncMetrcWebhooks_App.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AzFuncMetrcWebhooks_App.Functions;

public sealed class MetrcPackagesWebhookFunction
{
	private readonly ILogger<MetrcPackagesWebhookFunction> _log;
	private readonly MetrcWebhookValidator _validator;
	private readonly PushoverNotificationService _pushover;

	public MetrcPackagesWebhookFunction(
		ILogger<MetrcPackagesWebhookFunction> log,
		MetrcWebhookValidator validator,
		PushoverNotificationService pushover)
	{
		_log = log;
		_validator = validator;
		_pushover = pushover;
	}

	[Function("MetrcPackagesWebhook")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "metrc/packages/webhook")]
		HttpRequestData req)
	{
	}

	private static string? TryBuildPackageSummary(string body)
	{ }

	private static string SummarizePackage(JsonElement pkg, JsonElement? dataCount)
	{
	}
}
