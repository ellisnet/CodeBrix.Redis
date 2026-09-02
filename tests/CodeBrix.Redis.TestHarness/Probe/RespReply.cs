using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CodeBrix.Redis.Testing.Probe;

/// <summary>
/// One decoded RESP2 reply. Deliberately minimal: the harness only needs enough of the protocol to
/// decide whether a server is ready, so nothing here tries to be a Redis client.
/// </summary>
public sealed class RespReply
{
    private static readonly IReadOnlyList<RespReply> NoItems = [];

    private RespReply(RespReplyKind kind, string text, long integer, IReadOnlyList<RespReply> items)
    {
        Kind = kind;
        Text = text;
        Integer = integer;
        Items = items ?? NoItems;
    }

    /// <summary>Gets the reply's type.</summary>
    public RespReplyKind Kind { get; }

    /// <summary>
    /// Gets the reply's text for a simple string, an error or a bulk string; an empty string for
    /// every other kind.
    /// </summary>
    public string Text { get; }

    /// <summary>Gets the reply's value for an integer reply; zero for every other kind.</summary>
    public long Integer { get; }

    /// <summary>Gets the elements of an array reply; an empty list for every other kind.</summary>
    public IReadOnlyList<RespReply> Items { get; }

    /// <summary>Gets a value indicating whether this reply is an error.</summary>
    public bool IsError => Kind == RespReplyKind.Error;

    /// <summary>Creates a simple-string reply.</summary>
    /// <param name="text">The text after the leading <c>+</c>.</param>
    /// <returns>The reply.</returns>
    public static RespReply SimpleString(string text) =>
        new RespReply(RespReplyKind.SimpleString, text ?? string.Empty, 0L, null);

    /// <summary>Creates an error reply.</summary>
    /// <param name="text">The text after the leading <c>-</c>.</param>
    /// <returns>The reply.</returns>
    public static RespReply Error(string text) =>
        new RespReply(RespReplyKind.Error, text ?? string.Empty, 0L, null);

    /// <summary>Creates an integer reply.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The reply.</returns>
    public static RespReply Integer64(long value) =>
        new RespReply(RespReplyKind.Integer, string.Empty, value, null);

    /// <summary>Creates a bulk-string reply.</summary>
    /// <param name="text">The payload, decoded as UTF-8.</param>
    /// <returns>The reply.</returns>
    public static RespReply BulkString(string text) =>
        new RespReply(RespReplyKind.BulkString, text ?? string.Empty, 0L, null);

    /// <summary>Creates an array reply.</summary>
    /// <param name="items">The elements.</param>
    /// <returns>The reply.</returns>
    public static RespReply ArrayOf(IReadOnlyList<RespReply> items) =>
        new RespReply(RespReplyKind.Array, string.Empty, 0L, items);

    /// <summary>Creates the null reply, which is <c>$-1</c> or <c>*-1</c> on the wire.</summary>
    /// <returns>The reply.</returns>
    public static RespReply Null() => new RespReply(RespReplyKind.Null, string.Empty, 0L, null);

    /// <summary>
    /// Reads one field out of an <c>INFO</c>-style payload, where each line is <c>name:value</c>.
    /// </summary>
    /// <param name="fieldName">The field name, without the colon.</param>
    /// <returns>The field's value, or an empty string when the field is absent.</returns>
    public string InfoField(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(Text))
        {
            return string.Empty;
        }

        var prefix = fieldName + ":";
        foreach (var line in Text.Split('\n'))
        {
            var trimmed = line.Trim('\r', ' ');
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return trimmed.Substring(prefix.Length);
            }
        }

        return string.Empty;
    }

    /// <summary>Renders the reply in a form fit for a failure message.</summary>
    /// <returns>A short description of the reply.</returns>
    public override string ToString()
    {
        switch (Kind)
        {
            case RespReplyKind.SimpleString:
                return "+" + Text;
            case RespReplyKind.Error:
                return "-" + Text;
            case RespReplyKind.Integer:
                return ":" + Integer.ToString(CultureInfo.InvariantCulture);
            case RespReplyKind.BulkString:
                return Text.Length > 200 ? Text.Substring(0, 200) + "..." : Text;
            case RespReplyKind.Null:
                return "(nil)";
            case RespReplyKind.Array:
                var builder = new StringBuilder("[");
                for (var index = 0; index < Items.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(", ");
                    }

                    if (index == 8)
                    {
                        builder.Append("...");
                        break;
                    }

                    builder.Append(Items[index].ToString());
                }

                return builder.Append(']').ToString();
            default:
                return "(none)";
        }
    }
}
