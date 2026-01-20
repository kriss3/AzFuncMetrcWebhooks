using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public sealed class MetrcWebhookInspectFunction
{
	private readonly ILogger<MetrcWebhookInspectFunction> _logger;

	public MetrcWebhookInspectFunction(ILogger<MetrcWebhookInspectFunction> logger)
	{
		_logger = logger;
	}

	[Function("MetrcWebhookInspect")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Anonymous, "post", "put")]
		HttpRequestData req)
	{
		// 1️.PROVE METRC CALLED YOU
		_logger.LogWarning("METRC WEBHOOK RECEIVED");

		// 2️.LOG REQUEST INFO
		_logger.LogWarning("Method: {method}", req.Method);
		_logger.LogWarning("Url: {url}", req.Url);

		
	}
}
