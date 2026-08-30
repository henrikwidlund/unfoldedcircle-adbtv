using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.Discovery;
using UnfoldedCircle.AdbTv.WebSocket;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddUnfoldedCircleServer<AdbWebSocketHandler, AdbMediaPlayerCommandId, AdbConfigurationService, AdbGlobalConfiguration, AdbConfigurationItem>();
builder.Services.AddSingleton<AdbTvClientFactory>();
builder.Services.AddSingleton<AdbMdnsDiscovery>();
builder.Services.AddHostedService(static sp => sp.GetRequiredService<AdbMdnsDiscovery>());

var app = builder.Build();

app.UseUnfoldedCircleServer<AdbWebSocketHandler, AdbMediaPlayerCommandId, AdbGlobalConfiguration, AdbConfigurationItem>();

await app.RunAsync();
