using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

/// <summary>
/// A SocketManager monitors multiple sockets for availability of data; this is done using
/// the Socket.Select API and a dedicated reader-thread, which allows for fast responses
/// even when the system is under ambient load.
/// </summary>
[Obsolete("SocketManager is no longer used by CodeBrix.Redis")]
public sealed partial class SocketManager : IDisposable
{
    /// <summary>
    /// Gets the name of this SocketManager instance.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates a new <see cref="SocketManager"/> instance.
    /// </summary>
    /// <param name="name">The name for this <see cref="SocketManager"/>.</param>
    public SocketManager(string name)
        : this(name, 0, SocketManagerOptions.None) { }

    /// <summary>
    /// Creates a new <see cref="SocketManager"/> instance.
    /// </summary>
    /// <param name="name">The name for this <see cref="SocketManager"/>.</param>
    /// <param name="useHighPrioritySocketThreads">Whether this <see cref="SocketManager"/> should use high priority sockets.</param>
    public SocketManager(string name, bool useHighPrioritySocketThreads)
        : this(name, 0, UseHighPrioritySocketThreads(useHighPrioritySocketThreads)) { }

    /// <summary>
    /// Creates a new (optionally named) <see cref="SocketManager"/> instance.
    /// </summary>
    /// <param name="name">The name for this <see cref="SocketManager"/>.</param>
    /// <param name="workerCount">the number of dedicated workers for this <see cref="SocketManager"/>.</param>
    /// <param name="useHighPrioritySocketThreads">Whether this <see cref="SocketManager"/> should use high priority sockets.</param>
    public SocketManager(string name, int workerCount, bool useHighPrioritySocketThreads)
        : this(name, workerCount, UseHighPrioritySocketThreads(useHighPrioritySocketThreads)) { }

    private static SocketManagerOptions UseHighPrioritySocketThreads(bool value)
        => value ? SocketManagerOptions.UseHighPrioritySocketThreads : SocketManagerOptions.None;

    /// <summary>
    /// Additional options for configuring the socket manager.
    /// </summary>
    [Flags]
    public enum SocketManagerOptions
    {
        /// <summary>
        /// No additional options.
        /// </summary>
        None = 0,

        /// <summary>
        /// Whether the <see cref="SocketManager"/> should use high priority sockets.
        /// </summary>
        UseHighPrioritySocketThreads = 1 << 0,

        /// <summary>
        /// Use the regular thread-pool for all scheduling.
        /// </summary>
        UseThreadPool = 1 << 1,
    }

    /// <summary>
    /// Creates a new (optionally named) <see cref="SocketManager"/> instance.
    /// </summary>
    /// <param name="name">The name for this <see cref="SocketManager"/>.</param>
    /// <param name="workerCount">The number of dedicated workers for this <see cref="SocketManager"/>.</param>
    /// <param name="options">Options to use when creating the socket manager.</param>
    public SocketManager(string? name = null, int workerCount = 0, SocketManagerOptions options = SocketManagerOptions.None)
    {
        if (name.IsNullOrWhiteSpace()) name = GetType().Name;
        Name = name;
        _ = workerCount;
        _ = options;
    }

    /// <summary>
    /// Default / shared socket manager using a dedicated thread-pool.
    /// </summary>
    public static SocketManager Shared => ThreadPool;

    /// <summary>
    /// Shared socket manager using the main thread-pool.
    /// </summary>
    public static SocketManager ThreadPool { get; } = new("ThreadPoolSocketManager", options: SocketManagerOptions.UseThreadPool);

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => Name;

    /// <summary>
    /// Releases all resources associated with this instance.
    /// </summary>
    public void Dispose() { }
}

// These three helpers are upstream's, and upstream declares them on SocketManager - a type it has marked
// [Obsolete] - so its one live caller (PhysicalConnection.CreateSocket) names an obsolete type and needs a
// CS0618 pragma. This repository carries no suppressions, so they live on this non-obsolete internal class
// instead. They were already internal static utilities with no dependency on SocketManager's own state, so
// nothing moved except the name in front of the dot; SocketManager itself is otherwise unchanged, including
// its [Obsolete] attribute and its whole public surface.
internal static class SocketFactory
{
    internal static Socket CreateSocket(EndPoint endpoint, bool tcpKeepAlive)
    {
        var addressFamily = endpoint.AddressFamily;
        var protocolType = addressFamily == AddressFamily.Unix ? ProtocolType.Unspecified : ProtocolType.Tcp;

        var socket = addressFamily == AddressFamily.Unspecified
            ? new Socket(SocketType.Stream, protocolType)
            : new Socket(addressFamily, SocketType.Stream, protocolType);
        TrySetNoDelay(socket);
        if (tcpKeepAlive) TryEnableTcpKeepAlive(socket, endpoint);
        return socket;
    }

    internal static bool TrySetNoDelay(Socket socket)
    {
        try
        {
            if (socket.AddressFamily is not AddressFamily.Unix)
            {
                socket.NoDelay = true;
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message, nameof(Socket));
        }

        return false;
    }

    internal static bool TryEnableTcpKeepAlive(Socket socket, EndPoint endPoint)
    {
        // TCP keep-alive; there's a clue in the name
        if (socket.ProtocolType is not ProtocolType.Tcp) return false;

        switch (endPoint)
        {
            case DnsEndPoint:
            case IPEndPoint:
                // fine
                break;
            default:
                // don't enable on unexpected endpoint types (unix domain sockets, for example)
                return false;
        }

        try
        {
            // enable TCP keep-alive (best effort only)
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return false;
        }
    }
}
