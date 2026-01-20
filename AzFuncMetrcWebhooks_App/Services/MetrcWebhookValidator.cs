using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public sealed class MetrcWebhookInspectFunction(ILogger<MetrcWebhookInspectFunction> logger)
{
	private readonly ILogger<MetrcWebhookInspectFunction> _logger = logger;

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

		// 3️.READ AND LOG RAW BODY (THIS IS WHAT YOU CARE ABOUT)
		string body;
		using (var reader = new StreamReader(req.Body, Encoding.UTF8))
		{
			body = await reader.ReadToEndAsync();
		}

		_logger.LogWarning("RAW PAYLOAD START");
		_logger.LogWarning(body);
		_logger.LogWarning("RAW PAYLOAD END");

		// 4.ALWAYS RETURN 200 OK
		return req.CreateResponse(HttpStatusCode.OK);

	}
}
