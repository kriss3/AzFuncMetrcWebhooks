using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace AzFuncMetrcWebhooks_App.Services;

public sealed class MetrcWebhookValidator
{
	public static bool IsValid(HttpRequestData req)
	{
		// Read at request time (not at startup)
		var expectedSecret = Environment.GetEnvironmentVariable("MetrcWebhook__Secret");

		if (string.IsNullOrWhiteSpace(expectedSecret))
			return false;

		var query = req.Url.Query;
		if (string.IsNullOrWhiteSpace(query))
			return false;

		var parts = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);

		foreach (var p in parts)
		{
			var kv = p.Split('=', 2);
			if (kv.Length == 2 &&
				string.Equals(kv[0], "secret", StringComparison.OrdinalIgnoreCase))
			{
				var actual = Uri.UnescapeDataString(kv[1]);
				return string.Equals(actual, expectedSecret, StringComparison.Ordinal);
			}
		}

		return false;
	}
}
