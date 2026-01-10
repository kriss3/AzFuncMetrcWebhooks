using AzFuncMetrcWebhooks_App.Helpers;
using AzFuncMetrcWebhooks_App.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace AzFuncMetrcWebhooks_App.Functions;

public sealed class MetrcPackagesWebhookFunction
{
	private readonly ILogger<MetrcPackagesWebhookFunction> _log;
	private readonly MetrcWebhookValidator _validator;
	private readonly PushoverNotificationService _pushover;
	private static readonly ConcurrentDictionary<string, byte> _seen = new();

	public MetrcPackagesWebhookFunction(
		ILogger<MetrcPackagesWebhookFunction> log,
		MetrcWebhookValidator validator,
		PushoverNotificationService pushover)
	{
		_log = log;
		_validator = validator;
		_pushover = pushover;
	}

	[Function("MetrcPackagesWebhook")]
	public async Task<HttpResponseData> Run(
	[HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "metrc/packages/webhook")]
	HttpRequestData req)
	{
		WebhookLogHelper.Hit(_log);


		// LOG #1: entry + url
		_log.LogWarning("WEBHOOK HIT: {method} {url}", req.Method, req.Url);
		_log.LogWarning(	"ENV MetrcWebhook__Secret present: {present}",
			!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MetrcWebhook__Secret")));
		
		var isValid = _validator.IsValid(req);
		_log.LogWarning("VALIDATOR RESULT: {isValid}", isValid);

		if (!isValid)
		{
			_log.LogWarning("Ignoring request: missing/invalid secret. url={url}", req.Url);
			return req.CreateResponse(HttpStatusCode.OK);
		}

		_log.LogWarning("BODY READ: starting");
		string body;
		using (var reader = new StreamReader(req.Body))
		{
			body = await reader.ReadToEndAsync();
		}

		_log.LogWarning("BODY READ: done. BodyLength={len}", body.Length);

		if (string.IsNullOrWhiteSpace(body))
		{
			_log.LogWarning("Ignoring request: body is empty/whitespace.");
			return req.CreateResponse(HttpStatusCode.OK);
		}

		var contentType = req.Headers.TryGetValues("Content-Type", out var ct) ? ct.FirstOrDefault() : "(none)";
		_log.LogWarning("CONTENT-TYPE: {ct}", contentType);

		var preview = body.Length <= 500 ? body : body[..500];
		_log.LogWarning("BODY PREVIEW (first 500 chars): {preview}", preview);

		var summary = TryBuildPackageSummary(body)
			?? "Received Metrc Packages webhook (could not parse fields yet).";

		try
		{
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;

			JsonElement pkg =
				root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
					? data[0]
					: root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
						? root[0]
						: root;

			var id = pkg.ValueKind == JsonValueKind.Object && pkg.TryGetProperty("Id", out var idEl) ? idEl.ToString() : "(no Id)";
			var lastModified = pkg.ValueKind == JsonValueKind.Object && pkg.TryGetProperty("LastModified", out var lmEl) ? lmEl.ToString() : "(no LastModified)";
			var label = pkg.ValueKind == JsonValueKind.Object && pkg.TryGetProperty("Label", out var labEl) ? labEl.ToString() : "(no Label)";

			var dedupeKey = $"{id}:{lastModified}";

			if (!_seen.TryAdd(dedupeKey, 0))
			{
				_log.LogWarning("DEDUPED: already processed {key}", dedupeKey);
				return req.CreateResponse(HttpStatusCode.OK);
			}

			_log.LogWarning("PAYLOAD KEY FIELDS: Id={id} Label={label} LastModified={lm}", id, label, lastModified);
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Could not parse payload key fields.");
		}

		try
		{
			await _pushover.SendAsync("Metrc Packages Webhook", summary);
			_log.LogWarning("PUSHOVER: sent OK");
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Pushover send failed.");
		}

		var ok = req.CreateResponse(HttpStatusCode.OK);
		await ok.WriteStringAsync($"OK");
		return ok;
	}


	private static string? TryBuildPackageSummary(string body)
	{
		try
		{
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;

			// Case 1: wrapper template { "data": [ ... ], "datacount": n }
			if (root.ValueKind == JsonValueKind.Object &&
				root.TryGetProperty("data", out var data) &&
				data.ValueKind == JsonValueKind.Array &&
				data.GetArrayLength() > 0)
			{
				return SummarizePackage(data[0], root.TryGetProperty("datacount", out var dc) ? dc : (JsonElement?)null);
			}

			// Case 2: raw array of packages
			if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
			{
				return SummarizePackage(root[0], null);
			}

			// Case 3: single package object
			if (root.ValueKind == JsonValueKind.Object)
			{
				return SummarizePackage(root, null);
			}

			return null;
		}
		catch
		{
			return null;
		}
	}

	private static string SummarizePackage(JsonElement pkg, JsonElement? dataCount)
	{
		string GetString(JsonElement obj, string name)
			=> obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) ? v.ToString() : "(n/a)";

		decimal? GetDecimal(JsonElement obj, string name)
		{
			if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return null;
			if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
			if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var ds)) return ds;
			return null;
		}

		bool? GetBool(JsonElement obj, string name)
		{
			if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return null;
			if (v.ValueKind == JsonValueKind.True) return true;
			if (v.ValueKind == JsonValueKind.False) return false;
			return null;
		}

		string label = GetString(pkg, "Label");
		string id = GetString(pkg, "Id");
		string type = GetString(pkg, "PackageType");

		var qty = GetDecimal(pkg, "Quantity");
		var uom = GetString(pkg, "UnitOfMeasureAbbreviation");
		if (uom == "(n/a)") uom = GetString(pkg, "UnitOfMeasureName");

		string location = GetString(pkg, "SublocationName");
		if (location == "(n/a)") location = GetString(pkg, "LocationName");

		string lab = GetString(pkg, "LabTestingState");
		string lastMod = GetString(pkg, "LastModified");

		// Item.Name (nested)
		string itemName = "(n/a)";
		if (pkg.ValueKind == JsonValueKind.Object &&
			pkg.TryGetProperty("Item", out var item) &&
			item.ValueKind == JsonValueKind.Object)
		{
			itemName = GetString(item, "Name");
		}

		var flags =
			$"Hold:{YN(GetBool(pkg, "IsOnHold"))} " +
			$"Inv:{YN(GetBool(pkg, "IsOnInvestigation"))} " +
			$"Recall:{YN(GetBool(pkg, "IsOnRecallCombined"))} " +
			$"Finished:{YN(GetBool(pkg, "IsFinished"))}";

		var countText = dataCount.HasValue ? $"datacount={dataCount.Value}" : null;

		return $@"{(countText is null 
			? "" 
			: $"{countText}\n")} Label: {label} Id: {id} • Type: {type} Item: {itemName} Qty: {(qty is null ? "(n/a)" : $"{qty:0.####} {uom}".Trim())} Loc: {location} Lab: {lab} • {flags} LastModified: {lastMod}".Trim();
	}

	private static string YN(bool? v) => v == true ? "Y" : "N";
}
