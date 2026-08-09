using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.Discovery;
using UnfoldedCircle.AdbTv.WebSocket;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddUnfoldedCircleServer<AdbWebSocketHandler, AdbMediaPlayerCommandId, AdbConfigurationService, AdbConfigurationItem>();
builder.Services.AddSingleton<AdbTvClientFactory>();
builder.Services.AddSingleton<AdbMdnsDiscovery>();
builder.Services.AddHostedService(static sp => sp.GetRequiredService<AdbMdnsDiscovery>());

var app = builder.Build();

app.UseUnfoldedCircleServer<AdbWebSocketHandler, AdbMediaPlayerCommandId, AdbConfigurationItem>();

await app.RunAsync();
