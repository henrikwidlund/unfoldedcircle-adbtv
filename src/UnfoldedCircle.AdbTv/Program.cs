using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.BackgroundServices;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.WebSocket;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddUnfoldedCircleServer<AdbWebSocketHandler, AdbConfigurationService, AdbConfigurationItem>(static options => options.DisableEntityIdPrefixing = true);
builder.Services.AddSingleton<AdbTvClientFactory>();

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")?.Equals("true", StringComparison.OrdinalIgnoreCase) != true)
    builder.Services.AddHostedService<AdbBackgroundService>();

var app = builder.Build();

app.UseUnfoldedCircleServer<AdbWebSocketHandler, AdbConfigurationItem>();

await app.RunAsync();
