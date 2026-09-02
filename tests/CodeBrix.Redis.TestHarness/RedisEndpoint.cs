using System.Globalization;

namespace CodeBrix.Redis.Testing;

/// <summary>
/// One host and port a test connects to.
/// </summary>
/// <param name="Host">The host, always reachable from the machine running the tests.</param>
/// <param name="Port">The TCP port.</param>
public readonly record struct RedisEndpoint(string Host, int Port)
{
    /// <summary>Gets the endpoint in the <c>host:port</c> form a connection string wants.</summary>
    public string HostAndPort =>
        Host + ":" + Port.ToString(CultureInfo.InvariantCulture);

    /// <summary>Renders the endpoint as <c>host:port</c>.</summary>
    /// <returns>The endpoint in <c>host:port</c> form.</returns>
    public override string ToString() => HostAndPort;
}
