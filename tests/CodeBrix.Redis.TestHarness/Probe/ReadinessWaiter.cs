//Adapted from RedisSetupTool.DockerManagement in the CodeBrix.Docker samples.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.Redis.Testing.Probe;

/// <summary>
/// Polls a condition until it holds or a deadline passes. Every readiness wait in the harness goes
/// through this, so a topology that fails to come up reports what it was waiting for and what the
/// last failure was rather than hanging.
/// </summary>
public static class ReadinessWaiter
{
    /// <summary>The interval between polls when a caller does not name one.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(400);

    /// <summary>Polls until the condition holds.</summary>
    /// <param name="description">
    /// What is being waited for, phrased to follow "waiting for" in a failure message.
    /// </param>
    /// <param name="timeout">How long to keep trying.</param>
    /// <param name="condition">
    /// The condition. An exception thrown from it counts as "not yet" and its message is kept for
    /// the timeout report; a connection refused while a server is still starting is exactly that.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>How long the wait took.</returns>
    /// <exception cref="TimeoutException">The condition did not hold before the deadline.</exception>
    public static async Task<TimeSpan> WaitAsync(string description, TimeSpan timeout,
        Func<CancellationToken, Task<bool>> condition, CancellationToken cancellationToken) =>
        await WaitAsync(description, timeout, DefaultPollInterval, condition, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Polls until the condition holds, at a caller-chosen interval.</summary>
    /// <param name="description">
    /// What is being waited for, phrased to follow "waiting for" in a failure message.
    /// </param>
    /// <param name="timeout">How long to keep trying.</param>
    /// <param name="pollInterval">How long to wait between attempts.</param>
    /// <param name="condition">The condition; see the other overload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>How long the wait took.</returns>
    /// <exception cref="TimeoutException">The condition did not hold before the deadline.</exception>
    public static async Task<TimeSpan> WaitAsync(string description, TimeSpan timeout,
        TimeSpan pollInterval, Func<CancellationToken, Task<bool>> condition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var clock = Stopwatch.StartNew();
        string lastFailure = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await condition(cancellationToken).ConfigureAwait(false))
                {
                    return clock.Elapsed;
                }

                lastFailure = null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastFailure = exception.GetType().Name + ": " + exception.Message;
            }

            if (clock.Elapsed >= timeout)
            {
                throw new TimeoutException(
                    "Timed out after "
                    + timeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)
                    + " s waiting for " + description + "."
                    + (lastFailure is null ? string.Empty : " Last failure: " + lastFailure));
            }

            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
