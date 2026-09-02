using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.RedLock.Configuration;
using CodeBrix.Redis.RedLock.Events;
using CodeBrix.Redis.RedLock.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeBrix.Redis.RedLock; //was previously: RedLockNet.SERedis;

/// <summary>
/// Creates <see cref="RedLock"/> instances over a fixed set of independent Redis instances, and owns
/// the connections to them. Create one per application and keep it for the life of the process.
/// </summary>
public class RedLockFactory : IDistributedLockFactory, IDisposable
{
    private readonly RedLockConfiguration configuration;
    private readonly ILoggerFactory loggerFactory;
    private readonly ICollection<RedisConnection> redisCaches;

    /// <summary>
    /// Raised when one of the factory's connections reports a configuration change, carrying the
    /// state of every endpoint so a listener can tell whether a quorum is still reachable.
    /// </summary>
    public event EventHandler<RedLockConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <summary>
    /// Create a RedLockFactory using a list of RedLockEndPoints (ConnectionMultiplexers will be internally managed by RedLock.net)
    /// </summary>
    /// <param name="endPoints">The Redis instances to run the algorithm across. One entry per independent instance.</param>
    /// <param name="loggerFactory">The factory used to create the loggers for the connections and the locks; optional.</param>
    /// <returns>A factory that owns the connections it creates.</returns>
    public static RedLockFactory Create(IList<RedLockEndPoint> endPoints, ILoggerFactory? loggerFactory = null)
    {
        return Create(endPoints, null, loggerFactory);
    }

    /// <summary>
    /// Create a RedLockFactory using a list of RedLockEndPoints (ConnectionMultiplexers will be internally managed by RedLock.net)
    /// </summary>
    /// <param name="endPoints">The Redis instances to run the algorithm across. One entry per independent instance.</param>
    /// <param name="retryConfiguration">How hard a lock attempt retries before giving up; <see langword="null"/> keeps the defaults.</param>
    /// <param name="loggerFactory">The factory used to create the loggers for the connections and the locks; optional.</param>
    /// <returns>A factory that owns the connections it creates.</returns>
    public static RedLockFactory Create(IList<RedLockEndPoint> endPoints, RedLockRetryConfiguration? retryConfiguration, ILoggerFactory? loggerFactory = null)
    {
        var configuration = new RedLockConfiguration(endPoints, loggerFactory)
        {
            RetryConfiguration = retryConfiguration
        };
        return new RedLockFactory(configuration);
    }

    /// <summary>
    /// Create a RedLockFactory using existing CodeBrix.Redis ConnectionMultiplexers
    /// </summary>
    /// <param name="existingMultiplexers">The already-connected multiplexers to run the algorithm across. One entry per independent instance.</param>
    /// <param name="loggerFactory">The factory used to create the loggers for the connections and the locks; optional.</param>
    /// <returns>A factory that leaves the supplied connections' lifetime to their owner.</returns>
    public static RedLockFactory Create(IList<RedLockMultiplexer> existingMultiplexers, ILoggerFactory? loggerFactory = null)
    {
        return Create(existingMultiplexers, null, loggerFactory);
    }

    /// <summary>
    /// Create a RedLockFactory using existing CodeBrix.Redis ConnectionMultiplexers
    /// </summary>
    /// <param name="existingMultiplexers">The already-connected multiplexers to run the algorithm across. One entry per independent instance.</param>
    /// <param name="retryConfiguration">How hard a lock attempt retries before giving up; <see langword="null"/> keeps the defaults.</param>
    /// <param name="loggerFactory">The factory used to create the loggers for the connections and the locks; optional.</param>
    /// <returns>A factory that leaves the supplied connections' lifetime to their owner.</returns>
    public static RedLockFactory Create(IList<RedLockMultiplexer> existingMultiplexers, RedLockRetryConfiguration? retryConfiguration, ILoggerFactory? loggerFactory = null)
    {
        var configuration = new RedLockConfiguration(
            new ExistingMultiplexersRedLockConnectionProvider
            {
                Multiplexers = existingMultiplexers
            },
            loggerFactory)
        {
            RetryConfiguration = retryConfiguration
        };

        return new RedLockFactory(configuration);
    }

    /// <summary>
    /// Create a RedLockFactory using the specified configuration
    /// </summary>
    /// <param name="configuration">Where the Redis instances are, where to log, and how hard to retry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    public RedLockFactory(RedLockConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration), "Configuration must not be null");
        //was previously: configuration.LoggerFactory ?? new LoggerFactory();
        //NullLoggerFactory is the no-op equivalent of a provider-less LoggerFactory and lives in
        //Microsoft.Extensions.Logging.Abstractions, so the concrete Microsoft.Extensions.Logging
        //package that upstream referenced for this one expression is not needed here.
        this.loggerFactory = configuration.LoggerFactory ?? NullLoggerFactory.Instance;
        this.redisCaches = configuration.ConnectionProvider.CreateRedisConnections();

        SubscribeToConnectionEvents();
    }

    private void SubscribeToConnectionEvents()
    {
        foreach (var cache in this.redisCaches)
        {
            cache.ConnectionMultiplexer.ConfigurationChanged += MultiplexerConfigurationChanged;
        }
    }

    private void UnsubscribeFromConnectionEvents()
    {
        foreach (var cache in this.redisCaches)
        {
            cache.ConnectionMultiplexer.ConfigurationChanged -= MultiplexerConfigurationChanged;
        }
    }

    private void MultiplexerConfigurationChanged(object? sender, CodeBrix.Redis.EndPointEventArgs args)
    {
        RaiseConfigurationChanged();
    }

    /// <inheritdoc />
    public IRedLock CreateLock(string resource, TimeSpan expiryTime)
    {
        return RedLock.Create(
            this.loggerFactory.CreateLogger<RedLock>(),
            redisCaches,
            resource,
            expiryTime,
            retryConfiguration: configuration.RetryConfiguration);
    }

    /// <inheritdoc />
    public async Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime)
    {
        return await RedLock.CreateAsync(
            this.loggerFactory.CreateLogger<RedLock>(),
            redisCaches,
            resource,
            expiryTime,
            retryConfiguration: configuration.RetryConfiguration).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IRedLock CreateLock(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime, CancellationToken? cancellationToken = null)
    {
        return RedLock.Create(
            this.loggerFactory.CreateLogger<RedLock>(),
            redisCaches,
            resource,
            expiryTime,
            waitTime,
            retryTime,
            configuration.RetryConfiguration,
            cancellationToken ?? CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime, CancellationToken? cancellationToken = null)
    {
        return await RedLock.CreateAsync(
            this.loggerFactory.CreateLogger<RedLock>(),
            redisCaches,
            resource,
            expiryTime,
            waitTime,
            retryTime,
            configuration.RetryConfiguration,
            cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        UnsubscribeFromConnectionEvents();

        this.configuration.ConnectionProvider.DisposeConnections();
    }

    /// <summary>
    /// Raises <see cref="ConfigurationChanged"/> with the current state of every endpoint of every instance.
    /// </summary>
    protected virtual void RaiseConfigurationChanged()
    {
        if (ConfigurationChanged == null)
        {
            return;
        }

        var connections = new List<Dictionary<EndPoint, RedLockConfigurationChangedEventArgs.RedLockEndPointStatus>>();

        foreach (var cache in this.redisCaches)
        {
            var endPointStatuses = new Dictionary<EndPoint, RedLockConfigurationChangedEventArgs.RedLockEndPointStatus>();

            foreach (var endPoint in cache.ConnectionMultiplexer.GetEndPoints())
            {
                var server = cache.ConnectionMultiplexer.GetServer(endPoint);

                endPointStatuses.Add(endPoint, new RedLockConfigurationChangedEventArgs.RedLockEndPointStatus(endPoint, server.IsConnected, server.IsReplica));
            }

            connections.Add(endPointStatuses);
        }

        ConfigurationChanged?.Invoke(this, new RedLockConfigurationChangedEventArgs(connections));
    }
}
