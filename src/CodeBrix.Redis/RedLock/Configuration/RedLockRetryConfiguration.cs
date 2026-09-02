using System;

namespace CodeBrix.Redis.RedLock.Configuration; //was previously: RedLockNet.SERedis.Configuration;

/// <summary>
/// How many times a lock attempt is retried before it gives up, and how long it waits between tries.
/// </summary>
public class RedLockRetryConfiguration
{
    /// <summary>
    /// Creates a retry configuration. Either value may be omitted to keep the built-in default.
    /// </summary>
    /// <param name="retryCount">How many attempts are made to reach a quorum; must be at least 1. Defaults to 3.</param>
    /// <param name="retryDelayMs">The upper bound, in milliseconds, of the random wait between attempts; must be at least 10. Defaults to 400.</param>
    /// <exception cref="ArgumentException"><paramref name="retryCount"/> is below 1, or <paramref name="retryDelayMs"/> is below 10.</exception>
    public RedLockRetryConfiguration(int? retryCount = null, int? retryDelayMs = null)
    {
        if (retryCount.HasValue && retryCount < 1)
        {
            throw new ArgumentException("Retry count must be at least 1", nameof(retryCount));
        }

        if (retryDelayMs.HasValue && retryDelayMs < 10)
        {
            throw new ArgumentException("Retry delay must be at least 10 ms", nameof(retryDelayMs));
        }

        RetryCount = retryCount;
        RetryDelayMs = retryDelayMs;
    }

    /// <summary>
    /// How many attempts are made to reach a quorum, or <see langword="null"/> for the built-in default.
    /// </summary>
    public int? RetryCount { get; }

    /// <summary>
    /// The upper bound, in milliseconds, of the random wait between attempts, or <see langword="null"/>
    /// for the built-in default.
    /// </summary>
    public int? RetryDelayMs { get; }
}
