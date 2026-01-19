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

	public static IReadOnlyList<PackageInfo> BuildInfos(string body)
	{
		using var doc = JsonDocument.Parse(body);
		var root = doc.RootElement;

		JsonElement? dataCount = null;
		var packages = new List<JsonElement>();

		// Metrc wrapper: { data: [...], datacount: n }
		if (root.ValueKind == JsonValueKind.Object &&
			root.TryGetProperty("data", out var data) &&
			data.ValueKind == JsonValueKind.Array)
		{
			if (root.TryGetProperty("datacount", out var dc))
				dataCount = dc;

			packages.AddRange(data.EnumerateArray());
		}
		// Raw array
		else if (root.ValueKind == JsonValueKind.Array)
		{
			packages.AddRange(root.EnumerateArray());
		}
		// Single object
		else
		{
			packages.Add(root);
		}

		var infos = new List<PackageInfo>(packages.Count);

		foreach (var pkg in packages)
		{
			if (pkg.ValueKind != JsonValueKind.Object)
				continue;

			var id = GetString(pkg, "Id") ?? string.Empty;
			var label = GetString(pkg, "Label") ?? string.Empty;
			var lastModified = GetString(pkg, "LastModified") ?? string.Empty;

			// ONLY the three fields you care about in summary:
			var qty = GetDecimal(pkg, "Quantity");
			var summary = $"Qty:{(qty?.ToString() ?? "n/a")}";

			var dedupeKey =
				!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(lastModified)
					? $"{id}:{lastModified}"
					: null;

			infos.Add(new PackageInfo(summary, dedupeKey, id, label, lastModified));
		}

		return infos;
	}

	private static List<JsonElement> ExtractPackages(JsonElement root, out JsonElement? dataCount)
	{
		dataCount = null;
		var list = new List<JsonElement>();

		if (root.ValueKind == JsonValueKind.Object &&
			root.TryGetProperty("data", out var data) &&
			data.ValueKind == JsonValueKind.Array)
		{
			if (root.TryGetProperty("datacount", out var dc))
				dataCount = dc;

			list.AddRange(data.EnumerateArray());
			return list;
		}

		if (root.ValueKind == JsonValueKind.Array)
		{
			list.AddRange(root.EnumerateArray());
			return list;
		}

		list.Add(root);
		return list;
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

public record PackageInfo(
	string Summary,
	string? DedupeKey,
	string Id,
	string Label,
	string LastModified
);


