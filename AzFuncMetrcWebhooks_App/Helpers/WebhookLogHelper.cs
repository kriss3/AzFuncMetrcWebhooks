using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzFuncMetrcWebhooks_App.Helpers;

public static class WebhookLogHelper
{
}

public sealed record RequestLogInfo(
		string FunctionName,
		string Method,
		string Url,
		string ContentType,
		int BodyLength,
		string BodyPreview,
		string? CorrelationId);
