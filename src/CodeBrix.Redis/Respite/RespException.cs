using System;
using System.Diagnostics.CodeAnalysis;

namespace CodeBrix.Redis.Respite; //was previously: RESPite;

/// <summary>
/// Represents a RESP error message.
/// </summary>
[Experimental(Experiments.Respite, UrlFormat = Experiments.UrlFormat)]
public sealed class RespException(string message) : Exception(message)
{
}
