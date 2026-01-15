using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.BackgroundServices;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.WebSocket;

Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", AppContext.BaseDirectory);
var builder = WebApplication.CreateSlimBuilder(args);

builder.AddUnfoldedCircleServer<AdbWebSocketHandler, AdbConfigurationService, AdbConfigurationItem>(static options => options.DisableEntityIdPrefixing = true);
builder.Services.AddSingleton<AdbTvClientFactory>();

builder.Services.AddHostedService<AdbBackgroundService>();

var app = builder.Build();

app.UseUnfoldedCircleServer<AdbWebSocketHandler, AdbConfigurationItem>();

await app.RunAsync();
