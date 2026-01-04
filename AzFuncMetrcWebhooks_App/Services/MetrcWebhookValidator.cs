using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzFuncMetrcWebhooks_App.Services;

public sealed class MetrcWebhookValidator
{
	public bool IsValid(HttpRequestData req) 
	{
		var expected = Environment.GetEnvironmentVariable("MetrcWebhook__Secret");
		if (string.IsNullOrWhiteSpace(expected))
			return false;

		// Header check: X-Metrc-Webhook-Secret
		if (req.Headers.TryGetValues("X-Metrc-Webhook-Secret", out var values))
		{
			var provided = values.FirstOrDefault();
			if (string.Equals(provided, expected, StringComparison.Ordinal))
				return true;
		}

		// Query string check: ?secret=...
		var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
		var q = query.Get("secret");
		return string.Equals(q, expected, StringComparison.Ordinal);
	}
}
