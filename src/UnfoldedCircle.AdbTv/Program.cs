using UnfoldedCircle.AdbTv.AdbTv;
using UnfoldedCircle.AdbTv.Configuration;
using UnfoldedCircle.AdbTv.WebSocket;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddUnfoldedCircleServer<AdbWebSocketHandler, AdbMediaPlayerCommandId, AdbConfigurationService, AdbConfigurationItem>();
builder.Services.AddSingleton<AdbTvClientFactory>();

var app = builder.Build();

app.UseUnfoldedCircleServer<AdbWebSocketHandler, AdbMediaPlayerCommandId, AdbConfigurationItem>();

await app.RunAsync();
