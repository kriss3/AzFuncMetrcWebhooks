using AzFuncMetrcWebhooks_App.Helpers;
using AzFuncMetrcWebhooks_App.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace AzFuncMetrcWebhooks_App.Functions;

public sealed class MetrcPackagesWebhookFunction(
    ILogger<MetrcPackagesWebhookFunction> log,
    MetrcWebhookValidator validator,
    PushoverNotificationService pushover)
{
	private readonly ILogger<MetrcPackagesWebhookFunction> _log = log;
	private readonly MetrcWebhookValidator _validator = validator;
	private readonly PushoverNotificationService _pushover = pushover;

	// Dedupe across invocations (per package Id + LastModified)
	private static readonly ConcurrentDictionary<string, byte> _seen = new();

    [Function("MetrcPackagesWebhook")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "metrc/packages/webhook")]
		HttpRequestData req)
	{
		// 1.Validate secret
		if (!_validator.IsValid(req))
			return req.CreateResponse(HttpStatusCode.OK);

		// 2. Read body using helper function
		var body = await MetrcPackagesWebhookPayloadHelper.ReadBodyAsync(req.Body);
		if (string.IsNullOrWhiteSpace(body))
			return req.CreateResponse(HttpStatusCode.OK);

		// 3. Parse ALL packages from payload (supports wrapper, array, or single object)
		var parsed = TryParsePackages(body, out var packages, out var dataCount);
		if (!parsed)
		{
			_log.LogWarning("MetrcPackagesWebhook: could not parse JSON payload.");
			return req.CreateResponse(HttpStatusCode.OK);
		}

		// 4. Dedupe per package (Id + LastModified). Track what is "fresh".
		var fresh = new List<PackageLite>(packages.Count);
		foreach (var p in packages)
		{
			// If Id or LastModified missing, don't dedupe (still show the log)
			if (string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.LastModified))
			{
				fresh.Add(p);
				continue;
			}

			var key = $"{p.Id}:{p.LastModified}";
			if (_seen.TryAdd(key, 0))
				fresh.Add(p);
		}

		// 5) ONE log line per webhook call: answers "batched vs per-package"
		// - payloadCount = how many package objects arrived in THIS request
		// - datacount    = Metrc wrapper value if present, else payloadCount
		var compact = string.Join(", ", packages.Select(p =>
			$"{p.Id}|{p.Label}|{(p.Quantity?.ToString() ?? "n/a")}"));

		_log.LogInformation(
			"MetrcPackagesWebhook datacount={datacount} payloadCount={payloadCount} fresh={freshCount} packages=[{packages}]",
			dataCount ?? packages.Count,
			packages.Count,
			fresh.Count,
			compact);

		// 6. Pushover: ONLY the 3 fields you care about, and only for fresh items
		if (fresh.Count > 0)
		{
			var msg = string.Join("\n", fresh.Select(p =>
				$"Id:{p.Id} Label:{p.Label} Qty:{(p.Quantity?.ToString() ?? "n/a")}"));

			try
			{
				await _pushover.SendAsync($"Metrc Packages ({fresh.Count})", msg);
			}
			catch (Exception ex)
			{
				_log.LogError(ex, "MetrcPackagesWebhook: Pushover send failed.");
			}
		}

		var ok = req.CreateResponse(HttpStatusCode.OK);
		await ok.WriteStringAsync("OK");
		return ok;
	}

	// -------------------------
	// Parsing helpers (private)
	// -------------------------

	private static bool TryParsePackages(string body, out List<PackageLite> packages, out int? dataCount)
	{
		packages = [];
		dataCount = null;

		try
		{
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;

			// Wrapper: { data: [...], datacount: n }
			if (root.ValueKind == JsonValueKind.Object &&
				root.TryGetProperty("data", out var data) &&
				data.ValueKind == JsonValueKind.Array)
			{
				if (root.TryGetProperty("datacount", out var dc) && dc.ValueKind == JsonValueKind.Number)
					dataCount = dc.GetInt32();

				foreach (var el in data.EnumerateArray())
					AddIfPackageObject(el, packages);

				return true;
			}

			// Raw array: [ ... ]
			if (root.ValueKind == JsonValueKind.Array)
			{
				foreach (var el in root.EnumerateArray())
					AddIfPackageObject(el, packages);

				return true;
			}

			// Single object
			AddIfPackageObject(root, packages);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void AddIfPackageObject(JsonElement el, List<PackageLite> list)
	{
		if (el.ValueKind != JsonValueKind.Object)
			return;

		list.Add(new PackageLite(
			Id: GetString(el, "Id") ?? string.Empty,
			Label: GetString(el, "Label") ?? string.Empty,
			Quantity: GetDecimal(el, "Quantity"),
			LastModified: GetString(el, "LastModified") ?? string.Empty
		));
	}

	private static string? GetString(JsonElement obj, string name) => 
		obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) 
		? v.ToString() 
		: null;

	private static decimal? GetDecimal(JsonElement obj, string name)
	{
		if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v))
			return null;

		if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
			return d;

		if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var ds))
			return ds;

		return null;
	}
}

public sealed record PackageLite(
	string Id,
	string Label,
	decimal? Quantity,
	string LastModified);
