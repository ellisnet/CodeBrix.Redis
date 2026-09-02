using System;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

/// <summary>
/// Additional options for expiration-bearing commands.
/// </summary>
[Flags]
public enum ExpirationFlags
{
    /// <summary>
    /// No options specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Apply the expiration only if no expiration already exists.
    /// </summary>
    ExpireIfNotExists = 1 << 0,
}
