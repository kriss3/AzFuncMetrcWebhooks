using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.WebRequestMethods;

namespace AzFuncMetrcWebhooks_App.Services;

public sealed class PushoverNotificationService
{
	private readonly HttpClient _httpClient;

	public PushoverNotificationService(HttpClient httpClient) => _httpClient = httpClient;

	public async Task SendAsync(string title, string message)
	{
		var token = Environment.GetEnvironmentVariable("Pushover__AppToken");
		var user = Environment.GetEnvironmentVariable("Pushover__UserKey");
	}
}
