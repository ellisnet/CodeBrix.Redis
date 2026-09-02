using System;

namespace CodeBrix.Redis.Testing;

/// <summary>
/// The environment gate on the container-backed test tier.
/// </summary>
/// <remarks>
/// <para>
/// Three tiers of test exist in this repository, in increasing cost: the protocol suite, which
/// needs nothing; the in-process test server, which needs no external process; and this tier, which
/// starts real Redis servers in Docker containers. A contributor without a Docker daemon must still
/// get a green run from the first two, so every test that touches the harness asks
/// <see cref="IsEnabled"/> first and skips with <see cref="DisabledReason"/> when it is off.
/// </para>
/// <para>
/// In xUnit v3 that reads as <c>Assert.Skip(ContainerTier.DisabledReason)</c>, or as
/// <c>Skip.When(!ContainerTier.IsEnabled, ContainerTier.DisabledReason)</c>.
/// </para>
/// </remarks>
public static class ContainerTier
{
    /// <summary>
    /// The environment variable that enables the tier: <c>CODEBRIX_REDIS_RUN_CONTAINER_TESTS</c>.
    /// </summary>
    public const string EnvironmentVariableName = "CODEBRIX_REDIS_RUN_CONTAINER_TESTS";

    /// <summary>
    /// The environment variable that overrides the Redis container image the harness starts:
    /// <c>CODEBRIX_REDIS_TEST_IMAGE</c>.
    /// </summary>
    public const string ImageEnvironmentVariableName = "CODEBRIX_REDIS_TEST_IMAGE";

    /// <summary>
    /// The environment variable that overrides the Envoy container image the proxy topology starts:
    /// <c>CODEBRIX_REDIS_TEST_PROXY_IMAGE</c>.
    /// </summary>
    public const string ProxyImageEnvironmentVariableName = "CODEBRIX_REDIS_TEST_PROXY_IMAGE";

    static ContainerTier()
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        IsEnabled = string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        DisabledReason = IsEnabled
            ? string.Empty
            : "The container-backed test tier is off. Set " + EnvironmentVariableName
              + "=1 and make sure a Docker daemon is reachable to run tests that start real Redis"
              + " servers in containers.";
    }

    /// <summary>
    /// Gets a value indicating whether the container tier is enabled for this process. It is on when
    /// <see cref="EnvironmentVariableName"/> is set to <c>1</c>, <c>true</c> or <c>yes</c>.
    /// </summary>
    public static bool IsEnabled { get; }

    /// <summary>
    /// Gets the sentence a test passes to <c>Assert.Skip</c> when the tier is off, or an empty
    /// string when it is on.
    /// </summary>
    public static string DisabledReason { get; }
}
