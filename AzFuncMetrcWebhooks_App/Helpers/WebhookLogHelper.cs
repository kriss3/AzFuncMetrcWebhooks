using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzFuncMetrcWebhooks_App.Helpers;

public static class WebhookLogHelper
{
	public static RequestLogInfo BuildRequestInfo(
		string functionName,
		string method,
		Uri url,
		IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers,
		string body,
		int previewChars = 1000)
	{
		var contentType = TryGetHeader(headers, "Content-Type") ?? "(none)";
		var correlationId =
			TryGetHeader(headers, "x-correlation-id")
			?? TryGetHeader(headers, "x-request-id")
			?? null;

		var preview = string.IsNullOrEmpty(body)
			? ""
			: (body.Length <= previewChars ? body : body[..previewChars]);

		return new RequestLogInfo(
			FunctionName: functionName,
			Method: method,
			Url: url.ToString(),
			ContentType: contentType,
			BodyLength: body?.Length ?? 0,
			BodyPreview: preview,
			CorrelationId: correlationId);
	}

	public static IDisposable BeginScope(ILogger logger, RequestLogInfo info)
	{
		// Everything inside this scope will automatically include these properties (structured logging)
		var scope = new Dictionary<string, object?>
		{
			["fn"] = info.FunctionName,
			["method"] = info.Method,
			["url"] = info.Url,
			["contentType"] = info.ContentType,
			["bodyLen"] = info.BodyLength,
			["corrId"] = info.CorrelationId
		};

		return logger.BeginScope(scope);
	}

	public static void Hit(ILogger logger)
		=> logger.LogInformation("Webhook hit.");

	public static void Rejected(ILogger logger, string reason)
		=> logger.LogWarning("Webhook rejected: {reason}", reason);

}

public sealed record RequestLogInfo(
		string FunctionName,
		string Method,
		string Url,
		string ContentType,
		int BodyLength,
		string BodyPreview,
		string? CorrelationId);
