using System.Diagnostics.CodeAnalysis;
using CodeBrix.Redis.Respite;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

/// <summary>
/// Specifies how elements should be aggregated when combining sorted sets.
/// </summary>
public enum Aggregate
{
    /// <summary>
    /// The values of the combined elements are added.
    /// </summary>
    Sum,

    /// <summary>
    /// The least value of the combined elements is used.
    /// </summary>
    Min,

    /// <summary>
    /// The greatest value of the combined elements is used.
    /// </summary>
    Max,

    /// <summary>
    /// The number of combined element scores is used.
    /// </summary>
    Count,
}
