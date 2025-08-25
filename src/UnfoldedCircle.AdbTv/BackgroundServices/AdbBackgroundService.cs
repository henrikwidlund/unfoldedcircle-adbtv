using AdvancedSharpAdbClient;

namespace UnfoldedCircle.AdbTv.BackgroundServices;

public sealed class AdbBackgroundService : IHostedService
{
    private readonly CancellationTokenSource _keepAliveCancellationTokenSource = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
        await StartOrStop(true, linkedCancellationToken.Token);

        _ = Task.Factory.StartNew(() => _ = KeepAdbServerRunning(), TaskCreationOptions.LongRunning);
    }

    private async Task KeepAdbServerRunning()
    {
        using var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!_keepAliveCancellationTokenSource.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(_keepAliveCancellationTokenSource.Token))
            await StartOrStop(true, _keepAliveCancellationTokenSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
        await StartOrStop(false, linkedCancellationToken.Token);
    }

    private static Task StartOrStop(bool start, CancellationToken cancellationToken)
    {
        return start
            ? AdbServer.Instance.StartServerAsync("adb",false, cancellationToken)
            : AdbServer.Instance.StopServerAsync(cancellationToken);
    }
}