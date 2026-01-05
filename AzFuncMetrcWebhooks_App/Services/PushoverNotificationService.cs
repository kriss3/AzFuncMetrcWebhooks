namespace AzFuncMetrcWebhooks_App.Services;

public sealed class PushoverNotificationService(HttpClient httpClient)
{
	private readonly HttpClient _httpClient = httpClient;

    public async Task SendAsync(string title, string message)
	{
		var token = Environment.GetEnvironmentVariable("Pushover__AppToken");
		var user = Environment.GetEnvironmentVariable("Pushover__UserKey");

		if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(user))
			throw new InvalidOperationException("Missing Pushover__AppToken or Pushover__UserKey.");

		using var content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["token"] = token,
			["user"] = user,
			["title"] = title,
			["message"] = message
		});

		var resp = await _httpClient.PostAsync("https://api.pushover.net/1/messages.json", content);
		resp.EnsureSuccessStatusCode();
	}
}
