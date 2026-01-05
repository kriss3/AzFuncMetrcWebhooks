using AzFuncMetrcWebhooks_App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
	.ConfigureFunctionsWebApplication()
	.ConfigureServices(services =>
	{
		services.AddHttpClient<PushoverNotificationService>();
		services.AddSingleton<MetrcWebhookValidator>();
	})
	.Build();

host.Run();
