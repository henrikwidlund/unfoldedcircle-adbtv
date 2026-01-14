using System.Diagnostics;

namespace UnfoldedCircle.AdbTv.BackgroundServices;

public sealed class AdbBackgroundService(ILogger<AdbBackgroundService> logger) : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _keepAliveCancellationTokenSource = new();
    private readonly ILogger<AdbBackgroundService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
        await StartOrStop(true, linkedCancellationToken.Token);

        await CheckAdbMdnsAsync();

        _ = Task.Factory.StartNew(() => _ = KeepAdbServerRunning(), TaskCreationOptions.LongRunning);
    }

    private async Task CheckAdbMdnsAsync()
    {
        using var adbProcess = new Process();
        adbProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "adb",
            Arguments = "mdns check",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        adbProcess.Start();

        string output = await adbProcess.StandardOutput.ReadToEndAsync(_keepAliveCancellationTokenSource.Token);
        string error = await adbProcess.StandardError.ReadToEndAsync(_keepAliveCancellationTokenSource.Token);

        await adbProcess.WaitForExitAsync(_keepAliveCancellationTokenSource.Token);

        _logger.LogInformation("adb mdns check output: {Output}", output);
        if (!string.IsNullOrWhiteSpace(error))
            _logger.LogWarning("adb mdns check error: {Error}", error);
    }

    private async Task KeepAdbServerRunning()
    {
        using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!_keepAliveCancellationTokenSource.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(_keepAliveCancellationTokenSource.Token))
            await StartOrStop(true, _keepAliveCancellationTokenSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
        await StartOrStop(false, linkedCancellationToken.Token);
    }

    private static async Task StartOrStop(bool start, CancellationToken cancellationToken)
    {
        using var adbProcess = new Process();
        adbProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "adb",
            Arguments = start ? "start-server" : "kill-server",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        adbProcess.Start();

        await adbProcess.WaitForExitAsync(cancellationToken);
    }

    public void Dispose() => _keepAliveCancellationTokenSource.Dispose();
}