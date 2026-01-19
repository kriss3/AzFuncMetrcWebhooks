using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzFuncMetrcWebhooks_App.Models;

public sealed record MetrcPackagesPayload(
	int DataCount,
	IReadOnlyList<MetrcPackageEvent> Packages);

public sealed record MetrcPackageEvent(
	string Id,
	string Label,
	decimal? Quantity,
	string LastModified);
