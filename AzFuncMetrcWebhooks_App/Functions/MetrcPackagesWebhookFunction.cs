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
		[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "metrc/packages/webhook")]
		HttpRequestData req)
	{
		if (!_validator.IsValid(req))
		{
			_log.LogWarning("Rejected webhook: invalid secret.");
			return req.CreateResponse(HttpStatusCode.Unauthorized);
		}

		_log.LogWarning("WEBHOOK HIT - starting body read");
		var body = await new StreamReader(req.Body).ReadToEndAsync();

		_log.LogInformation("Metrc webhook received. ContentType={ct} Length={len}", req.Headers.TryGetValues("Content-Type", out var ct) ? ct.FirstOrDefault() : "(none)", body.Length);
		_log.LogWarning("WEBHOOK BODY LEN: {len}", body.Length);
		// Log first 1000 chars to inspect the shape of the result
		var preview = body.Length <= 1000 ? body : body[..1000];

		_log.LogInformation("Payload preview (first 1000 chars): {preview}", preview);
		_log.LogWarning("WEBHOOK BODY PREVIEW: {preview}", body.Length <= 500 ? body : body[..500]);

		// Extract a meaningful summary even if payload is wrapped
		var summary = TryBuildPackageSummary(body) ?? "Received Metrc Packages webhook (could not parse fields yet).";

		await _pushover.SendAsync("Metrc Packages Webhook", summary);

		var ok = req.CreateResponse(HttpStatusCode.OK);
		await ok.WriteStringAsync("OK");
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
