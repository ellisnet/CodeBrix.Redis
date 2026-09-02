using System;

namespace CodeBrix.Redis.Testing;

/// <summary>
/// Thrown when the harness itself cannot do its job: a container that will not start, a topology
/// that will not form, a configuration file that did not travel to the output folder.
/// </summary>
public sealed class HarnessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="HarnessException"/> class.</summary>
    public HarnessException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HarnessException"/> class.</summary>
    /// <param name="message">What went wrong, and what to do about it.</param>
    public HarnessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HarnessException"/> class.</summary>
    /// <param name="message">What went wrong, and what to do about it.</param>
    /// <param name="innerException">The underlying failure.</param>
    public HarnessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
