namespace CodeBrix.Redis.RedLock; //was previously: RedLockNet;

/// <summary>
/// How a lock attempt went across the instances it was attempted on: how many took the lock, how
/// many already held it under a different lock id, and how many could not be reached.
/// </summary>
public struct RedLockInstanceSummary
{
    /// <summary>
    /// Creates a summary from the per-instance counts of a single lock or extend attempt.
    /// </summary>
    /// <param name="acquired">The number of instances that took the lock.</param>
    /// <param name="conflicted">The number of instances already holding the lock under a different lock id.</param>
    /// <param name="error">The number of instances that could not be reached, or that failed the attempt.</param>
    public RedLockInstanceSummary(int acquired, int conflicted, int error)
    {
        this.Acquired = acquired;
        this.Conflicted = conflicted;
        this.Error = error;
    }

    /// <summary>
    /// The number of instances that took the lock.
    /// </summary>
    public readonly int Acquired;

    /// <summary>
    /// The number of instances already holding the lock under a different lock id.
    /// </summary>
    public readonly int Conflicted;

    /// <summary>
    /// The number of instances that could not be reached, or that failed the attempt.
    /// </summary>
    public readonly int Error;

    /// <summary>
    /// Returns the three instance counts in the form "Acquired: a, Conflicted: c, Error: e".
    /// </summary>
    /// <returns>A string describing the counts.</returns>
    public override string ToString()
    {
        return $"Acquired: {Acquired}, Conflicted: {Conflicted}, Error: {Error}";
    }
}
