using System.Diagnostics.CodeAnalysis;

namespace CodeBrix.Redis.Respite.Buffers; //was previously: RESPite.Buffers;

/// <summary>
/// Receives notification from a <see cref="CycleBuffer"/> when a page has been filled.
/// </summary>
[Experimental(Experiments.Respite, UrlFormat = Experiments.UrlFormat)]
public interface ICycleBufferCallback
{
    /// <summary>
    /// Notify that a page is available; this means that a consumer that wants
    /// unflushed data can activate when pages are rotated, allowing large
    /// payloads to be written concurrent with write.
    /// </summary>
    void PageComplete();
}
