using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public sealed class PingFunction
{
	[Function("Ping")]
	public static async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ping")]
		HttpRequestData req)
	{
		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteStringAsync("OK - Azure Function Isolated is live");
		return response;
	}
}
