using System;

namespace CodeBrix.Redis.RedLock; //was previously: RedLockNet;

/// <summary>
/// A distributed lock held over a single named resource. Disposing the lock releases it on every
/// instance it was taken on; while it is held it is extended automatically in the background.
/// </summary>
public interface IRedLock : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The name of the resource the lock is for.
    /// </summary>
    string Resource { get; }

    /// <summary>
    /// The unique identifier assigned to this lock.
    /// </summary>
    string LockId { get; }

    /// <summary>
    /// Whether the lock has been acquired.
    /// </summary>
    bool IsAcquired { get; }

    /// <summary>
    /// The status of the lock.
    /// </summary>
    RedLockStatus Status { get; }

    /// <summary>
    /// Details of the number of instances the lock was able to be acquired in.
    /// </summary>
    RedLockInstanceSummary InstanceSummary { get; }

    /// <summary>
    /// The number of times the lock has been extended.
    /// </summary>
    int ExtendCount { get; }
}
