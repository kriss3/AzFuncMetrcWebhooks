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
	private readonly MetrcWebhookValidator _validator;
	private readonly PushoverNotificationService _pushover;
	private static readonly ConcurrentDictionary<string, byte> _seen = new();

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
	[HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "metrc/packages/webhook")]
	HttpRequestData req)
	{
		var isValid = _validator.IsValid(req);
		_log.LogWarning("VALIDATOR RESULT: {isValid}", isValid);

		if (!isValid)
		{
			return req.CreateResponse(HttpStatusCode.OK);
		}

		string body;
		using (var reader = new StreamReader(req.Body))
		{
			body = await reader.ReadToEndAsync();
		}

		if (string.IsNullOrWhiteSpace(body))
		{
			return req.CreateResponse(HttpStatusCode.OK);
		}



	}

	private static string YN(bool? v) => v == true ? "Y" : "N";
}
