namespace UnfoldedCircle.AdbTv.Cancellation;

internal static class CancellationExtensions
{
    extension(Task)
    {
        /// <summary>
        /// Calls Task.Delay but swallows <see cref="OperationCanceledException"/> if the <paramref name="cancellationToken"/> is canceled.
        /// </summary>
        /// <param name="millisecondsDelay">The number of milliseconds to wait before completing the returned task, or -1 to wait indefinitely.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        public static async Task SafeDelay(int millisecondsDelay, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(millisecondsDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Swallow cancellation exceptions
            }
        }
    }
}