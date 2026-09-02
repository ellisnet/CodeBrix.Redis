namespace CodeBrix.Redis.Testing.Probe;

/// <summary>
/// The RESP reply types the harness probe understands. This is RESP2 only: the probe never sends
/// <c>HELLO</c>, so a server never answers it in RESP3.
/// </summary>
public enum RespReplyKind
{
    /// <summary>No reply was read. Used only for a default-valued <see cref="RespReply"/>.</summary>
    None = 0,

    /// <summary>A simple string, written on the wire as <c>+OK</c>.</summary>
    SimpleString = 1,

    /// <summary>An error, written on the wire as <c>-ERR something</c>.</summary>
    Error = 2,

    /// <summary>An integer, written on the wire as <c>:42</c>.</summary>
    Integer = 3,

    /// <summary>A bulk string, written on the wire as <c>$5\r\nhello\r\n</c>.</summary>
    BulkString = 4,

    /// <summary>An array, written on the wire as <c>*2\r\n</c> followed by its elements.</summary>
    Array = 5,

    /// <summary>A null bulk string or null array, written on the wire as <c>$-1</c> or <c>*-1</c>.</summary>
    Null = 6,
}
