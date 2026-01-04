using AzFuncMetrcWebhooks_App.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
	.ConfigureFunctionsWebApplication()
	.ConfigureServices(services =>
	{
		services.AddApplicationInsightsTelemetryWorkerService();
		services.ConfigureFunctionsApplicationInsights();

		services.AddHttpClient();
		services.AddSingleton<PushoverNotificationService>();
		services.AddSingleton<MetrcWebhookValidator>();
	})
	.Build();

host.Run();