using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.Redis.Testing.Probe;

/// <summary>
/// A raw RESP2 connection to a Redis server, used only for readiness probing.
/// </summary>
/// <remarks>
/// The harness deliberately does NOT use a Redis client for this. It cannot reference
/// CodeBrix.Redis - the harness has to work while that library is mid-port - and it may not take a
/// third-party client. Talking RESP over a socket directly is a few dozen lines and keeps the
/// harness usable no matter what state the library is in. It is also independent of the container
/// image's contents, which a <c>docker exec redis-cli</c> probe is not.
/// </remarks>
public sealed class RespConnection : IAsyncDisposable, IDisposable
{
    private readonly Socket _socket;
    private readonly Stream _stream;
    private readonly byte[] _buffer = new byte[8192];
    private int _bufferStart;
    private int _bufferEnd;
    private bool _disposed;

    private RespConnection(Socket socket, Stream stream)
    {
        _socket = socket;
        _stream = stream;
    }

    /// <summary>Gets a value indicating whether the connection is encrypted.</summary>
    public bool IsSecure => _stream is SslStream;

    /// <summary>Opens a plain-text connection.</summary>
    /// <param name="host">The host to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The open connection; the caller disposes it.</returns>
    public static async Task<RespConnection> ConnectAsync(string host, int port,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            return new RespConnection(socket, new NetworkStream(socket, ownsSocket: false));
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a TLS connection, validating the server against one specific certificate authority.
    /// </summary>
    /// <param name="host">The host to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="targetHost">The name to present in SNI and to match against the certificate.</param>
    /// <param name="certificateAuthority">
    /// The authority the server certificate must chain to, or null to accept any certificate. Passing
    /// the harness certificate authority is what makes a TLS test a real test of the chain.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The open connection; the caller disposes it.</returns>
    public static async Task<RespConnection> ConnectSecureAsync(string host, int port,
        string targetHost, X509Certificate2 certificateAuthority,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        SslStream ssl = null;
        try
        {
            await socket.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            var network = new NetworkStream(socket, ownsSocket: false);
            ssl = certificateAuthority is null
                ? new SslStream(network, leaveInnerStreamOpen: false,
                    (sender, certificate, chain, errors) => true)
                : new SslStream(network, leaveInnerStreamOpen: false,
                    BuildValidationCallback(certificateAuthority));

            await ssl.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = string.IsNullOrWhiteSpace(targetHost) ? host : targetHost,
                },
                cancellationToken).ConfigureAwait(false);

            return new RespConnection(socket, ssl);
        }
        catch
        {
            ssl?.Dispose();
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Sends one command and reads its reply.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="arguments">The command and its arguments, for example <c>INFO</c>, <c>replication</c>.</param>
    /// <returns>The decoded reply.</returns>
    public async Task<RespReply> CallAsync(CancellationToken cancellationToken,
        params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length == 0)
        {
            throw new ArgumentException("A command needs at least one argument.", nameof(arguments));
        }

        var request = new StringBuilder();
        request.Append('*').Append(arguments.Length.ToString(CultureInfo.InvariantCulture))
            .Append("\r\n");
        foreach (var argument in arguments)
        {
            var value = argument ?? string.Empty;
            request.Append('$')
                .Append(Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture))
                .Append("\r\n").Append(value).Append("\r\n");
        }

        var payload = Encoding.UTF8.GetBytes(request.ToString());
        await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        return await ReadReplyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
        _socket.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stream.DisposeAsync().ConfigureAwait(false);
        _socket.Dispose();
    }

    private static RemoteCertificateValidationCallback BuildValidationCallback(
        X509Certificate2 certificateAuthority) =>
        (sender, certificate, chain, errors) =>
        {
            if (certificate is null)
            {
                return false;
            }

            using var policy = new X509Chain();
            policy.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            policy.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            policy.ChainPolicy.CustomTrustStore.Add(certificateAuthority);
            using var presented = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
            return policy.Build(presented);
        };

    private async Task<RespReply> ReadReplyAsync(CancellationToken cancellationToken)
    {
        var line = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line.Length == 0)
        {
            throw new IOException("The server sent an empty RESP line.");
        }

        var prefix = line[0];
        var body = line.Substring(1);
        switch (prefix)
        {
            case '+':
                return RespReply.SimpleString(body);
            case '-':
                return RespReply.Error(body);
            case ':':
                return RespReply.Integer64(ParseLength(body));
            case '$':
                var bulkLength = ParseLength(body);
                if (bulkLength < 0)
                {
                    return RespReply.Null();
                }

                var payload = await ReadExactlyAsync((int)bulkLength + 2, cancellationToken)
                    .ConfigureAwait(false);
                return RespReply.BulkString(Encoding.UTF8.GetString(payload, 0, (int)bulkLength));
            case '*':
                var count = ParseLength(body);
                if (count < 0)
                {
                    return RespReply.Null();
                }

                var items = new List<RespReply>((int)count);
                for (var index = 0L; index < count; index++)
                {
                    items.Add(await ReadReplyAsync(cancellationToken).ConfigureAwait(false));
                }

                return RespReply.ArrayOf(items);
            default:
                throw new IOException("Unexpected RESP prefix '" + prefix + "'.");
        }
    }

    private static long ParseLength(string text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new IOException("Unexpected RESP length '" + text + "'.");

    private async Task<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        while (true)
        {
            if (_bufferStart == _bufferEnd)
            {
                await FillAsync(cancellationToken).ConfigureAwait(false);
            }

            var next = _buffer[_bufferStart++];
            if (next == (byte)'\n')
            {
                if (line.Length > 0 && line[line.Length - 1] == '\r')
                {
                    line.Length -= 1;
                }

                return line.ToString();
            }

            line.Append((char)next);
        }
    }

    private async Task<byte[]> ReadExactlyAsync(int count, CancellationToken cancellationToken)
    {
        var result = new byte[count];
        var written = 0;
        while (written < count)
        {
            if (_bufferStart == _bufferEnd)
            {
                await FillAsync(cancellationToken).ConfigureAwait(false);
            }

            var available = Math.Min(count - written, _bufferEnd - _bufferStart);
            Buffer.BlockCopy(_buffer, _bufferStart, result, written, available);
            _bufferStart += available;
            written += available;
        }

        return result;
    }

    private async Task FillAsync(CancellationToken cancellationToken)
    {
        _bufferStart = 0;
        _bufferEnd = await _stream.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
        if (_bufferEnd <= 0)
        {
            throw new IOException("The server closed the connection while a reply was expected.");
        }
    }
}
