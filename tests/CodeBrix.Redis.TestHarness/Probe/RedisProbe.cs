using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.Redis.Testing.Probe;

/// <summary>
/// The readiness questions the harness asks a Redis server, each one a single round trip over a
/// throwaway <see cref="RespConnection"/>.
/// </summary>
public static class RedisProbe
{
    /// <summary>Asks for <c>PING</c> and checks for <c>+PONG</c>.</summary>
    /// <param name="endpoint">The endpoint to probe.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the server answered PONG.</returns>
    public static async Task<bool> PingAsync(RedisEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        await using var connection = await RespConnection
            .ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        var reply = await connection.CallAsync(cancellationToken, "PING").ConfigureAwait(false);
        return IsPong(reply);
    }

    /// <summary>Authenticates with a password and then asks for <c>PING</c>.</summary>
    /// <param name="endpoint">The endpoint to probe.</param>
    /// <param name="password">The password the server was started with.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when AUTH succeeded and the server answered PONG.</returns>
    public static async Task<bool> AuthenticatedPingAsync(RedisEndpoint endpoint, string password,
        CancellationToken cancellationToken)
    {
        await using var connection = await RespConnection
            .ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        var auth = await connection.CallAsync(cancellationToken, "AUTH", password)
            .ConfigureAwait(false);
        if (auth.IsError)
        {
            return false;
        }

        var reply = await connection.CallAsync(cancellationToken, "PING").ConfigureAwait(false);
        return IsPong(reply);
    }

    /// <summary>Checks that a password-protected server actually refuses an unauthenticated call.</summary>
    /// <param name="endpoint">The endpoint to probe.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the server answered with a NOAUTH error.</returns>
    public static async Task<bool> RequiresPasswordAsync(RedisEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        await using var connection = await RespConnection
            .ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        var reply = await connection.CallAsync(cancellationToken, "GET", "codebrix-harness-probe")
            .ConfigureAwait(false);
        return reply.IsError
            && reply.Text.StartsWith("NOAUTH", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Completes a TLS handshake against a known authority and then asks for <c>PING</c>.</summary>
    /// <param name="endpoint">The endpoint to probe.</param>
    /// <param name="targetHost">The name to present in SNI and match against the certificate.</param>
    /// <param name="certificateAuthority">The authority the server certificate must chain to.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the handshake succeeded and the server answered PONG.</returns>
    public static async Task<bool> SecurePingAsync(RedisEndpoint endpoint, string targetHost,
        X509Certificate2 certificateAuthority, CancellationToken cancellationToken)
    {
        await using var connection = await RespConnection
            .ConnectSecureAsync(endpoint.Host, endpoint.Port, targetHost, certificateAuthority,
                cancellationToken).ConfigureAwait(false);
        var reply = await connection.CallAsync(cancellationToken, "PING").ConfigureAwait(false);
        return IsPong(reply);
    }

    /// <summary>Reads one section of <c>INFO</c>.</summary>
    /// <param name="endpoint">The endpoint to probe.</param>
    /// <param name="section">The section name, for example <c>replication</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reply, whose <see cref="RespReply.Text"/> holds the payload.</returns>
    public static async Task<RespReply> InfoAsync(RedisEndpoint endpoint, string section,
        CancellationToken cancellationToken)
    {
        await using var connection = await RespConnection
            .ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        return await connection.CallAsync(cancellationToken, "INFO", section).ConfigureAwait(false);
    }

    /// <summary>Checks that a server reports itself as a replica whose link to its primary is up.</summary>
    /// <param name="endpoint">The replica to probe.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the role is slave and the link state is up.</returns>
    public static async Task<bool> IsOnlineReplicaAsync(RedisEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var info = await InfoAsync(endpoint, "replication", cancellationToken).ConfigureAwait(false);
        return string.Equals(info.InfoField("role"), "slave", StringComparison.Ordinal)
            && string.Equals(info.InfoField("master_link_status"), "up", StringComparison.Ordinal);
    }

    /// <summary>Checks that a primary reports at least the given number of connected replicas.</summary>
    /// <param name="endpoint">The primary to probe.</param>
    /// <param name="expectedReplicas">How many replicas must be attached.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the role is master and enough replicas are attached.</returns>
    public static async Task<bool> HasReplicasAsync(RedisEndpoint endpoint, int expectedReplicas,
        CancellationToken cancellationToken)
    {
        var info = await InfoAsync(endpoint, "replication", cancellationToken).ConfigureAwait(false);
        if (!string.Equals(info.InfoField("role"), "master", StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(info.InfoField("connected_slaves"), out var connected)
            && connected >= expectedReplicas;
    }

    /// <summary>Reads <c>CLUSTER INFO</c> and checks for <c>cluster_state:ok</c>.</summary>
    /// <param name="endpoint">The cluster node to probe.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the node reports the cluster as healthy.</returns>
    public static async Task<bool> IsClusterHealthyAsync(RedisEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        await using var connection = await RespConnection
            .ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        var reply = await connection.CallAsync(cancellationToken, "CLUSTER", "INFO")
            .ConfigureAwait(false);
        return string.Equals(reply.InfoField("cluster_state"), "ok", StringComparison.Ordinal)
            && string.Equals(reply.InfoField("cluster_slots_assigned"), "16384",
                StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks that every slot range in <c>CLUSTER SLOTS</c> lists at least one replica behind its
    /// primary. A node is added to a range's reply only once it has finished its initial sync, so a
    /// cluster can be <c>cluster_state:ok</c> and still report bare primaries for a few seconds
    /// after it forms - which is a different question from whether the replicas exist at all.
    /// </summary>
    /// <param name="endpoint">The cluster node to probe.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the reply holds at least one range and every range names a
    /// primary and at least one replica.
    /// </returns>
    public static async Task<bool> ClusterSlotsListReplicasAsync(RedisEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        await using var connection = await RespConnection
            .ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        var reply = await connection.CallAsync(cancellationToken, "CLUSTER", "SLOTS")
            .ConfigureAwait(false);

        if (reply.Kind != RespReplyKind.Array || reply.Items is not { Count: > 0 } ranges)
        {
            return false;
        }

        foreach (var range in ranges)
        {
            //start slot, end slot, the primary, then one entry per replica: fewer than four
            //entries means the primary is listed on its own.
            if (range.Kind != RespReplyKind.Array || range.Items is not { Count: >= 4 })
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the raw <c>CLUSTER INFO</c> payload, for a diagnostic message.</summary>
    /// <param name="endpoint">The cluster node to probe.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reply.</returns>
    public static async Task<RespReply> ClusterInfoAsync(RedisEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        await using var connection = await RespConnection
            .ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        return await connection.CallAsync(cancellationToken, "CLUSTER", "INFO").ConfigureAwait(false);
    }

    /// <summary>
    /// Checks that a sentinel is monitoring the named service and has found its replica and its
    /// fellow sentinels.
    /// </summary>
    /// <param name="endpoint">The sentinel to probe.</param>
    /// <param name="serviceName">The monitored service name, for example <c>myprimary</c>.</param>
    /// <param name="expectedReplicas">How many replicas the sentinel must have discovered.</param>
    /// <param name="expectedOtherSentinels">How many other sentinels it must have discovered.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the sentinel has a full picture of the service.</returns>
    public static async Task<bool> IsSentinelReadyAsync(RedisEndpoint endpoint, string serviceName,
        int expectedReplicas, int expectedOtherSentinels, CancellationToken cancellationToken)
    {
        await using var connection = await RespConnection
            .ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);

        var masters = await connection.CallAsync(cancellationToken, "SENTINEL", "MASTERS")
            .ConfigureAwait(false);
        if (masters.Kind != RespReplyKind.Array || masters.Items.Count == 0)
        {
            return false;
        }

        var found = false;
        foreach (var master in masters.Items)
        {
            if (string.Equals(FieldOf(master, "name"), serviceName, StringComparison.Ordinal))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
        }

        var replicas = await connection
            .CallAsync(cancellationToken, "SENTINEL", "REPLICAS", serviceName).ConfigureAwait(false);
        if (replicas.Kind != RespReplyKind.Array || replicas.Items.Count < expectedReplicas)
        {
            return false;
        }

        var sentinels = await connection
            .CallAsync(cancellationToken, "SENTINEL", "SENTINELS", serviceName).ConfigureAwait(false);
        return sentinels.Kind == RespReplyKind.Array
            && sentinels.Items.Count >= expectedOtherSentinels;
    }

    /// <summary>
    /// Reads one field out of a sentinel reply, which is a flat array of alternating names and
    /// values rather than a map.
    /// </summary>
    /// <param name="entry">The array reply describing one master, replica or sentinel.</param>
    /// <param name="fieldName">The field to read.</param>
    /// <returns>The value, or an empty string when the field is absent.</returns>
    public static string FieldOf(RespReply entry, string fieldName)
    {
        if (entry is null || entry.Kind != RespReplyKind.Array)
        {
            return string.Empty;
        }

        for (var index = 0; index + 1 < entry.Items.Count; index += 2)
        {
            if (string.Equals(entry.Items[index].Text, fieldName, StringComparison.Ordinal))
            {
                return entry.Items[index + 1].Text;
            }
        }

        return string.Empty;
    }

    private static bool IsPong(RespReply reply) =>
        reply.Kind == RespReplyKind.SimpleString
        && string.Equals(reply.Text, "PONG", StringComparison.OrdinalIgnoreCase);
}
