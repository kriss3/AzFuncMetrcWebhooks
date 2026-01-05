using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzFuncMetrcWebhooks_App.Services;

public sealed class MetrcWebhookValidator
{
	private readonly string? _expectedSecret;

	public MetrcWebhookValidator(IConfiguration config)
	{
		_expectedSecret = config["MetrcWebhook__Secret"]; // do NOT throw here
	}

	public bool IsValid(HttpRequestData req)
	{
		if (string.IsNullOrWhiteSpace(_expectedSecret))
			return false;

		// simplest possible parse; avoids System.Web dependency
		var query = req.Url.Query; // like "?secret=abc"
		if (string.IsNullOrWhiteSpace(query))
			return false;

		// quick parse for "secret="
		var parts = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
		foreach (var p in parts)
		{
			var kv = p.Split('=', 2);
			if (kv.Length == 2 && kv[0] == "secret")
				return string.Equals(Uri.UnescapeDataString(kv[1]), _expectedSecret, StringComparison.Ordinal);
		}

		return false;
	}
}
