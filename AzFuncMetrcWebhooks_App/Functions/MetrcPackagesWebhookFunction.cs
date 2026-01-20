using AzFuncMetrcWebhooks_App.Helpers;
using AzFuncMetrcWebhooks_App.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace AzFuncMetrcWebhooks_App.Functions;

public sealed class MetrcPackagesWebhookFunction
{
	private readonly ILogger<MetrcPackagesWebhookFunction> _log;
	private readonly MetrcPackagesWebhookService _service;

	public MetrcPackagesWebhookFunction(
		ILogger<MetrcPackagesWebhookFunction> log,
		MetrcPackagesWebhookService service)
	{
		_log = log;
		_service = service;
	}

	[Function("MetrcPackagesWebhook")]
	public Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "metrc/packages/webhook")]
		HttpRequestData req)
		=> _service.ProcessAsync(req, _log);
}
