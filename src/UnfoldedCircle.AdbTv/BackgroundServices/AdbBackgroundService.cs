using System.Diagnostics;

namespace UnfoldedCircle.AdbTv.BackgroundServices;

public sealed class AdbBackgroundService : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var adbProcess = new Process();
        adbProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "adb",
            Arguments = "start-server",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        adbProcess.Start();

        await adbProcess.WaitForExitAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var adbProcess = new Process();
        adbProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "adb",
            Arguments = "kill-server",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        adbProcess.Start();

        await adbProcess.WaitForExitAsync(cancellationToken);
    }
}