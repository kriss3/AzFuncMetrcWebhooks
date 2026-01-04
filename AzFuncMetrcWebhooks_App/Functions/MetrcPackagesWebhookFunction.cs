using AzFuncMetrcWebhooks_App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

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
}
