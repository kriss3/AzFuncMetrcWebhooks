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
		
	}
}
