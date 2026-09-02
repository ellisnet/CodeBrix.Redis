using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;

namespace CodeBrix.Redis.RedLock.Configuration; //was previously: RedLockNet.SERedis.Configuration;

/// <summary>
/// One independent Redis instance for the Redlock algorithm, and the settings used to connect to it.
/// </summary>
public class RedLockEndPoint
{
    /// <summary>
    /// Construct a RedLockEndPoint instance with no endpoints; add them to <see cref="EndPoints"/>
    /// or assign <see cref="EndPoint"/> before use.
    /// </summary>
    public RedLockEndPoint()
    {
        EndPoints = new List<EndPoint>();
    }

    /// <summary>
    /// Construct a RedLockEndPoint instance using a single endpoint.
    /// </summary>
    /// <param name="endPoint">The address of the redis server.</param>
    public RedLockEndPoint(EndPoint endPoint)
        : this()
    {
        this.EndPoint = endPoint;
    }

    /// <summary>
    /// Construct a RedLockEndPoint instance using a list of endpoints.
    /// Can be used for connecting to replicated master/slaves.
    /// These servers will all be considered a single entity as far as the RedLock algorithm is concerned.
    /// </summary>
    /// <param name="endPoints">The addresses of the redis servers; <see langword="null"/> is treated as an empty list.</param>
    public RedLockEndPoint(IList<EndPoint>? endPoints)
    {
        this.EndPoints = endPoints ?? new List<EndPoint>();
    }

    /// <summary>
    /// Creates a RedLockEndPoint for a single redis server address.
    /// </summary>
    /// <param name="endPoint">The address of the redis server.</param>
    public static implicit operator RedLockEndPoint(EndPoint endPoint)
    {
        return new RedLockEndPoint(endPoint);
    }

    /// <summary>
    /// Creates a RedLockEndPoint for a set of redis server addresses that are treated as one instance.
    /// </summary>
    /// <param name="endPoints">The addresses of the redis servers.</param>
    public static implicit operator RedLockEndPoint(List<EndPoint> endPoints)
    {
        return new RedLockEndPoint(endPoints);
    }

    /// <summary>
    /// The endpoint for the redis connection.
    /// </summary>
    public EndPoint? EndPoint
    {
        get => EndPoints.FirstOrDefault();
        //assigning null clears the list rather than storing a null entry, which could only fail later
        set => EndPoints = value is null ? new List<EndPoint>() : new List<EndPoint> {value};
    }

    /// <summary>
    /// The endpoints for the redis connection. Can be used for connecting to replicated master/slaves.
    /// These servers will all be considered a single entity as far as the RedLock algorithm is concerned.
    /// See http://redis.io/topics/distlock#why-failover-based-implementations-are-not-enough
    /// </summary>
    public IList<EndPoint> EndPoints { get; private set; }

    /// <summary>
    /// Whether to use SSL for the redis connection.
    /// </summary>
    public bool Ssl { get; set; }

    /// <summary>
    /// The allowed SSL/TLS protocols for the redis connection.
    /// Defaults to a value chosen by .NET if not specified.
    /// </summary>
    public SslProtocols? SslProtocols { get; set; }

    /// <summary>
    /// The password for the redis connection.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// The connection timeout for the redis connection.
    /// Defaults to 100ms if not specified.
    /// </summary>
    public int? ConnectionTimeout { get; set; }

    /// <summary>
    /// The sync timeout for the redis connection.
    /// Defaults to 1000ms if not specified.
    /// </summary>
    public int? SyncTimeout { get; set; }

    /// <summary>
    /// The database to use with this redis connection.
    /// Defaults to 0 if not specified.
    /// </summary>
    public int? RedisDatabase { get; set; }

    /// <summary>
    /// The string format for keys created in redis, must include {0}.
    /// Defaults to "redlock:{0}" if not specified.
    /// </summary>
    public string? RedisKeyFormat { get; set; }

    /// <summary>
    /// The number of seconds between config change checks
    /// Defaults to 10 seconds if not specified.
    /// </summary>
    public int? ConfigCheckSeconds { get; set; }
}
