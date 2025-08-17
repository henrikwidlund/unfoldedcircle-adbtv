using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.BackgroundServices;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.WebSocket;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddUnfoldedCircleServer<AdbWebSocketHandler, AdbConfigurationService, AdbConfigurationItem>(static options => options.DisableEntityIdPrefixing = true);
builder.Services.AddSingleton<AdbTvClientFactory>();

builder.Services.AddHostedService<AdbBackgroundService>();

var app = builder.Build();

var configurationFile = Path.Combine(app.Configuration["UC_CONFIG_HOME"] ?? string.Empty, "configured_entities.json");
if (File.Exists(configurationFile))
    app.Logger.LogTrace("Configuration File Dump: {ConfigurationFileDump}", await File.ReadAllLinesAsync(configurationFile));

app.UseUnfoldedCircleServer<AdbWebSocketHandler, AdbConfigurationItem>();

await app.RunAsync();
