using Cytrus.Exceptions;
using Microsoft.Extensions.Logging;

namespace Cytrus.Cdn;

public sealed partial class RetryPolicy(CdnOptions options, ILogger<RetryPolicy> logger)
{
    public async Task<T> ExecuteAsync<T>(
        Func<int, CancellationToken, Task<T>> action,
        string operation,
        CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < options.MaxAttempts && IsTransient(ex, cancellationToken))
            {
                var delay = ComputeDelay(attempt);
                LogTransientFailureOnOperationAttemptAttemptMaxRetryingInDelay(operation, attempt, options.MaxAttempts, delay, ex);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        return ex switch
        {
            TransientCdnException => true,
            HttpRequestException => true,
            IOException => true,
            TaskCanceledException => true,
            _ => false
        };
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        var exp = options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var capped = Math.Min(exp, options.RetryMaxDelay.TotalMilliseconds);
        var jittered = Random.Shared.NextDouble() * capped;
        return TimeSpan.FromMilliseconds(jittered);
    }

    [LoggerMessage(LogLevel.Warning, "Transient failure on {Operation} (attempt {Attempt}/{Max}); retrying in {Delay}.")]
    partial void LogTransientFailureOnOperationAttemptAttemptMaxRetryingInDelay(string operation, int attempt, int max, TimeSpan delay, Exception exception);
}
