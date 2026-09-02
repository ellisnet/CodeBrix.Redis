using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Redis.Availability;
using CodeBrix.Redis.Respite;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

public partial class ConnectionMultiplexer
{
    /// <summary>
    /// Creates a new <see cref="IConnectionMultiplexer"/> instance that manages connections to multiple
    /// redundant configurations, based on their availability and relative <see cref="ConnectionGroupMember.Weight"/>.
    /// </summary>
    /// <param name="members">The initial configurations to connect to.</param>
    /// <param name="options">Additional options for configuring this group.</param>
    /// <param name="log">The <see cref="TextWriter"/> to log to.</param>
    [Experimental(Experiments.GeoRedundantFailover, UrlFormat = Experiments.UrlFormat)]
    public static Task<IConnectionGroup> ConnectGroupAsync(
        ConnectionGroupMember[] members,
        MultiGroupOptions? options = null,
        TextWriter? log = null)
    {
        // create a defensive copy of the array; we don't want callers being able to radically swap things!
        members = (ConnectionGroupMember[])members.Clone();
        return MultiGroupMultiplexer.ConnectAsync(members, options ?? MultiGroupOptions.Default, log);
    }

    /// <summary>
    /// Creates a new <see cref="IConnectionMultiplexer"/> instance that manages connections to multiple
    /// redundant configurations, based on their availability and relative <see cref="ConnectionGroupMember.Weight"/>.
    /// </summary>
    /// <param name="member0">An initial configuration to connect to.</param>
    /// <param name="member1">An additional initial configuration to connect to.</param>
    /// <param name="options">Additional options for configuring this group.</param>
    /// <param name="log">The <see cref="TextWriter"/> to log to.</param>
    [Experimental(Experiments.GeoRedundantFailover, UrlFormat = Experiments.UrlFormat)]
    public static Task<IConnectionGroup> ConnectGroupAsync(
        ConnectionGroupMember member0,
        ConnectionGroupMember member1,
        MultiGroupOptions? options = null,
        TextWriter? log = null)
    {
        return MultiGroupMultiplexer.ConnectAsync([member0, member1], options ?? MultiGroupOptions.Default, log);
    }
}
