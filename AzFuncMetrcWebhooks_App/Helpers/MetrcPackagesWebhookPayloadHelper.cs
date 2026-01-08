using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzFuncMetrcWebhooks_App.Helpers;

public static class MetrcPackagesWebhookPayloadHelper
{
	public static async Task<string> ReadBodyAsync(Stream body)
	{
		using var reader = new StreamReader(body);
		return await reader.ReadToEndAsync();
	}
}

public sealed record PackageInfo(
		string Summary,
		string? DedupeKey,
		string Id,
		string Label,
		string LastModified);
