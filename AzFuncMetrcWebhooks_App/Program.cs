using AzFuncMetrcWebhooks_App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFunctionsWorkerDefaults();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<PushoverNotificationService>();

builder.Services.AddSingleton<MetrcWebhookValidator>();

var host = builder.Build();
host.Run();