using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzFuncMetrcWebhooks_App.Services;

public sealed class MetrcPackagesWebhookInspectorService
{
	private readonly MetrcWebhookValidator _validator;
	private readonly ILogger<MetrcPackagesWebhookInspectorService> _log;
}
