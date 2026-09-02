using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CodeBrix.Redis.RedLock.Configuration; //was previously: RedLockNet.SERedis.Configuration;

/// <summary>
/// Everything a <see cref="RedLockFactory"/> needs: where the Redis instances are (or how to reach
/// ones that are already connected), where to log, and how hard to retry a lock attempt.
/// </summary>
public class RedLockConfiguration
{
    /// <summary>
    /// Creates a configuration that connects to, and owns, a connection per supplied endpoint.
    /// </summary>
    /// <param name="endPoints">The Redis instances to run the algorithm across. One entry per independent instance.</param>
    /// <param name="loggerFactory">The factory used to create the loggers for the connections and the locks; optional.</param>
    public RedLockConfiguration(IList<RedLockEndPoint> endPoints, ILoggerFactory? loggerFactory = null)
    {
        this.ConnectionProvider = new InternallyManagedRedLockConnectionProvider(loggerFactory)
        {
            EndPoints = endPoints
        };
        this.LoggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a configuration that takes its connections from the supplied provider.
    /// </summary>
    /// <param name="connectionProvider">Supplies the connections the algorithm runs across.</param>
    /// <param name="loggerFactory">The factory used to create the loggers for the connections and the locks; optional.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connectionProvider"/> is <see langword="null"/>.</exception>
    public RedLockConfiguration(RedLockConnectionProvider connectionProvider, ILoggerFactory? loggerFactory = null)
    {
        this.ConnectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider), "Connection provider must not be null");
        this.LoggerFactory = loggerFactory;
    }

    /// <summary>
    /// Supplies the connections the algorithm runs across, and owns their lifetime.
    /// </summary>
    public RedLockConnectionProvider ConnectionProvider { get; }

    /// <summary>
    /// The factory used to create the loggers for the connections and the locks, or <see langword="null"/>
    /// when nothing is to be logged.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; }

    /// <summary>
    /// How many times, and how far apart, a lock attempt is retried before it gives up. Defaults to
    /// the built-in settings when <see langword="null"/>.
    /// </summary>
    public RedLockRetryConfiguration? RetryConfiguration { get; set; }
}
