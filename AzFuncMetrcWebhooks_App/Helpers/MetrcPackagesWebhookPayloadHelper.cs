using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzFuncMetrcWebhooks_App.Helpers;

public static class MetrcPackagesWebhookPayloadHelper
{

	public static async Task<string> ReadBodyAsync(Stream body)
	{
		using var reader = new StreamReader(body);
		return await reader.ReadToEndAsync();
	}

	public static PackageInfo BuildInfoOrFallback(string body)
	{
		var summary = TryBuildPackageSummary(body)
			?? "Received Metrc Packages webhook (could not parse fields yet).";

		// If we can’t parse key fields, keep existing behavior:
		// - still send pushover using summary
		// - dedupeKey stays null (so caller won’t dedupe)
		try
		{
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;

			var pkg = ExtractFirstPackage(root, out var dataCount);

			var id = GetString(pkg, "Id") ?? "(no Id)";
			var lastModified = GetString(pkg, "LastModified") ?? "(no LastModified)";
			var label = GetString(pkg, "Label") ?? "(no Label)";

			var dedupeKey = $"{id}:{lastModified}";

			// IMPORTANT: summary should be derived from the same pkg we extracted
			// so it matches the dedupe fields.
			summary = SummarizePackage(pkg, dataCount) ?? summary;

			return new PackageInfo(
				Summary: summary,
				DedupeKey: dedupeKey,
				Id: id,
				Label: label,
				LastModified: lastModified);
		}
		catch
		{
			return new PackageInfo(
				Summary: summary,
				DedupeKey: null,
				Id: "(unknown)",
				Label: "(unknown)",
				LastModified: "(unknown)");
		}
	}

	private static JsonElement ExtractFirstPackage(JsonElement root, out JsonElement? dataCount)
	{
		dataCount = null;

		// Case 1: wrapper template { "data": [ ... ], "datacount": n }
		if (root.ValueKind == JsonValueKind.Object &&
			root.TryGetProperty("data", out var data) &&
			data.ValueKind == JsonValueKind.Array &&
			data.GetArrayLength() > 0)
		{
			if (root.TryGetProperty("datacount", out var dc))
				dataCount = dc;

			return data[0];
		}

		// Case 2: raw array of packages
		if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
			return root[0];

		// Case 3: single package object
		return root;
	}

	private static string? TryBuildPackageSummary(string body)
	{
		try
		{
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;

			var pkg = ExtractFirstPackage(root, out var dataCount);
			return SummarizePackage(pkg, dataCount);
		}
		catch
		{
			return null;
		}
	}

	private static string? SummarizePackage(JsonElement pkg, JsonElement? dataCount)
	{
		if (pkg.ValueKind != JsonValueKind.Object)
			return null;

		var label = GetString(pkg, "Label") ?? "(n/a)";
		var id = GetString(pkg, "Id") ?? "(n/a)";
		var type = GetString(pkg, "PackageType") ?? "(n/a)";

		var qty = GetDecimal(pkg, "Quantity");
		var uom = GetString(pkg, "UnitOfMeasureAbbreviation");
		if (string.IsNullOrWhiteSpace(uom) || uom == "(n/a)")
			uom = GetString(pkg, "UnitOfMeasureName") ?? "(n/a)";

		var location = GetString(pkg, "SublocationName");
		if (string.IsNullOrWhiteSpace(location) || location == "(n/a)")
			location = GetString(pkg, "LocationName") ?? "(n/a)";

		var lab = GetString(pkg, "LabTestingState") ?? "(n/a)";
		var lastMod = GetString(pkg, "LastModified") ?? "(n/a)";

		var itemName = "(n/a)";
		if (pkg.TryGetProperty("Item", out var item) && item.ValueKind == JsonValueKind.Object)
			itemName = GetString(item, "Name") ?? "(n/a)";

		var flags =
			$"Hold:{YN(GetBool(pkg, "IsOnHold"))} " +
			$"Inv:{YN(GetBool(pkg, "IsOnInvestigation"))} " +
			$"Recall:{YN(GetBool(pkg, "IsOnRecallCombined"))} " +
			$"Finished:{YN(GetBool(pkg, "IsFinished"))}";

		var countText = dataCount.HasValue ? $"datacount={dataCount.Value}" : null;

		var qtyText = qty is null ? "(n/a)" : $"{qty:0.####} {uom}".Trim();

		return $@"{(countText is null ? "" : $"{countText}\n")}Label: {label} Id: {id} • Type: {type} Item: {itemName} Qty: {qtyText} Loc: {location} Lab: {lab} • {flags} LastModified: {lastMod}"
			.Trim();
	}

}

public sealed record PackageInfo(
		string Summary,
		string? DedupeKey,
		string Id,
		string Label,
		string LastModified);
