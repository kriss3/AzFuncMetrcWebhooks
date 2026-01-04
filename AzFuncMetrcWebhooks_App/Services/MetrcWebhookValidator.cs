using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzFuncMetrcWebhooks_App.Services;

public sealed class MetrcWebhookValidator
{
	public bool IsValid(HttpRequestData req) 
	{
		bool result = false;
		var expected = Environment.GetEnvironmentVariable("MetrcWebhook__Secret");
		if (string.IsNullOrWhiteSpace(expected))
			return false;

		// 1) Header check: X-Metrc-Webhook-Secret
		if (req.Headers.TryGetValues("X-Metrc-Webhook-Secret", out var values))
		{
			var provided = values.FirstOrDefault();
			if (string.Equals(provided, expected, StringComparison.Ordinal))
				return true;
		}

		return result;
	}
}
