using AzFuncMetrcWebhooks_App.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AzFuncMetrcWebhooks_App.Functions;

public sealed class MetrcPackagesWebhookFunction
{
	private readonly ILogger<MetrcPackagesWebhookFunction> _log;
	private readonly MetrcWebhookValidator _validator;
	private readonly PushoverNotificationService _pushover;

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
		const string BuildStamp = "2026-01-05T00:15Z"; // change this each deploy
		_log.LogWarning("BUILD STAMP: {stamp}", BuildStamp);

		// LOG #1: entry + url
		_log.LogWarning("WEBHOOK HIT: {method} {url}", req.Method, req.Url);
		_log.LogWarning(	"ENV MetrcWebhook__Secret present: {present}",
			!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MetrcWebhook__Secret"))
);

		// LOG #2: show whether expected secret exists in Azure (WITHOUT printing it)
		// Add ExpectedSecretIsConfigured property to validator as shown below
		_log.LogWarning("VALIDATOR: ExpectedSecretConfigured={configured}", _validator.ExpectedSecretIsConfigured);

		// LOG #3: run validator and log result
		var isValid = _validator.IsValid(req);
		_log.LogWarning("VALIDATOR RESULT: {isValid}", isValid);

		if (!isValid)
		{
			// LOG #4: make this Warning so it shows in traces
			_log.LogWarning("Ignoring request: missing/invalid secret. url={url}", req.Url);
			return req.CreateResponse(HttpStatusCode.OK);
		}

		// LOG #5: about to read body
		_log.LogWarning("BODY READ: starting");

		string body;
		using (var reader = new StreamReader(req.Body))
		{
			body = await reader.ReadToEndAsync();
		}

		// LOG #6: body length AFTER reading (this is the only length that matters)
		_log.LogWarning("BODY READ: done. BodyLength={len}", body.Length);

		if (string.IsNullOrWhiteSpace(body))
		{
			_log.LogWarning("Ignoring request: body is empty/whitespace.");
			return req.CreateResponse(HttpStatusCode.OK);
		}

		// LOG #7: content-type (warning so you see it)
		var contentType = req.Headers.TryGetValues("Content-Type", out var ct) ? ct.FirstOrDefault() : "(none)";
		_log.LogWarning("CONTENT-TYPE: {ct}", contentType);

		// Preview (keep to Warning so you actually see it)
		var preview = body.Length <= 500 ? body : body[..500];
		_log.LogWarning("BODY PREVIEW (first 500 chars): {preview}", preview);

		var summary = TryBuildPackageSummary(body)
			?? "Received Metrc Packages webhook (could not parse fields yet).";

		// LOG #8: before pushover
		_log.LogWarning("PUSHOVER: sending. SummaryLength={len}", summary.Length);

		try
		{
			await _pushover.SendAsync("Metrc Packages Webhook", summary);
			// LOG #9: after pushover success
			_log.LogWarning("PUSHOVER: sent OK");
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Pushover send failed.");
		}

		var ok = req.CreateResponse(HttpStatusCode.OK);
		await ok.WriteStringAsync($"OK {BuildStamp}");
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
