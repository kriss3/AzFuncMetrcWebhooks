using AzFuncMetrcWebhooks_App.Helpers;
using Microsoft.Azure.Functions.Worker.Http;
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

	public MetrcPackagesWebhookInspectorService(
		MetrcWebhookValidator validator,
		ILogger<MetrcPackagesWebhookInspectorService> log)
	{
		_validator = validator;
		_log = log;
	}

	public async Task<HttpResponseData> InspectAsync(HttpRequestData req)
	{
		// 1) Always prove request arrived (even if secret invalid)
		_log.LogWarning("METRC WEBHOOK ARRIVED: {method} {url}", req.Method, req.Url);

		// 2) Read body once
		var body = await MetrcPackagesWebhookPayloadHelper.ReadBodyAsync(req.Body);

		_log.LogWarning("RAW BODY: length={len}", body?.Length ?? 0);

		// preview only (keeps logs manageable)
		var preview = string.IsNullOrEmpty(body)
			? ""
			: (body.Length <= 2000 ? body : body[..2000]);

		_log.LogWarning("RAW BODY PREVIEW (first 2000 chars): {preview}", preview);

	}
}
