using AzFuncMetrcWebhooks_App.Models;
using System.Text.Json;

namespace AzFuncMetrcWebhooks_App.Helpers;

public static class MetrcPackagesWebhookPayloadHelper
{
	public static async Task<string> ReadBodyAsync(Stream body)
	{
		using var reader = new StreamReader(body);
		return await reader.ReadToEndAsync();
	}

	public static MetrcPackagesPayload ParsePackages(string body)
	{
		using var doc = JsonDocument.Parse(body);
		var root = doc.RootElement;

		List<JsonElement> packageElements = new();
		int dataCount;

		if (root.ValueKind == JsonValueKind.Object &&
			root.TryGetProperty("data", out var data) &&
			data.ValueKind == JsonValueKind.Array)
		{
			packageElements.AddRange(data.EnumerateArray());
			dataCount = root.TryGetProperty("datacount", out var dc)
				? dc.GetInt32()
				: packageElements.Count;
		}
		else if (root.ValueKind == JsonValueKind.Array)
		{
			packageElements.AddRange(root.EnumerateArray());
			dataCount = packageElements.Count;
		}
		else
		{
			packageElements.Add(root);
			dataCount = 1;
		}

		var packages = new List<MetrcPackageEvent>(packageElements.Count);

		foreach (var pkg in packageElements)
		{
			if (pkg.ValueKind != JsonValueKind.Object)
				continue;

			packages.Add(new MetrcPackageEvent(
				Id: GetString(pkg, "Id") ?? "(no-id)",
				Label: GetString(pkg, "Label") ?? "(no-label)",
				Quantity: GetDecimal(pkg, "Quantity"),
				LastModified: GetString(pkg, "LastModified") ?? "(no-lastmodified)"
			));
		}

		return new MetrcPackagesPayload(dataCount, packages);
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

	private static string? GetString(JsonElement obj, string name)
		=> obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) ? v.ToString() : null;

	private static decimal? GetDecimal(JsonElement obj, string name)
	{
		if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return null;
		if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
		if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var ds)) return ds;
		return null;
	}

	private static bool? GetBool(JsonElement obj, string name)
	{
		if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return null;
		if (v.ValueKind == JsonValueKind.True) return true;
		if (v.ValueKind == JsonValueKind.False) return false;
		return null;
	}

	private static string YN(bool? v) => v == true ? "Y" : "N";



}


