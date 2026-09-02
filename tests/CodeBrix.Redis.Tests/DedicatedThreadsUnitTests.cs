using CodeBrix.Redis.Respite.Streams;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// The <c>DedicatedThreads</c> feature flag, which takes this library's reader and writer off the global
/// thread-pool and onto threads it owns.
/// </summary>
/// <remarks>
/// Exercised through <see cref="PhysicalConnection.ResolveWriteMode"/> rather than by connecting: the flag is
/// process-wide static, so a test that set it and then connected would be racing every other test in the run.
/// The policy is the part worth pinning anyway - that sync-mode is what owns the threads is a fact about
/// <c>SwitchableBufferedStreamWriter</c>, and is covered by <c>BufferedStreamWriterTests</c>.
/// </remarks>
public class DedicatedThreadsUnitTests
{
    [Theory]
    [InlineData(ConnectionType.Interactive)]
    [InlineData(ConnectionType.Subscription)]
    public void without_the_flag_nothing_changes(ConnectionType connectionType)
    {
        var expected = connectionType is ConnectionType.Subscription
            ? BufferedStreamWriter.WriteMode.Async
            : BufferedStreamWriter.WriteMode.Default;

        PhysicalConnection.ResolveWriteMode(connectionType, BufferedStreamWriter.WriteMode.Default, dedicatedThreads: false).Should().Be(expected);
    }

    [Fact]
    public void with_the_flag_interactive_connections_own_their_threads()
        => PhysicalConnection.ResolveWriteMode(ConnectionType.Interactive, BufferedStreamWriter.WriteMode.Default, dedicatedThreads: true).Should().Be(BufferedStreamWriter.WriteMode.Sync);

    /// <summary>
    /// Pub/sub keeps its own rule: the flag is not a licence to reverse a deliberate policy.
    /// </summary>
    [Fact]
    public void with_the_flag_subscriptions_are_unaffected()
        => PhysicalConnection.ResolveWriteMode(ConnectionType.Subscription, BufferedStreamWriter.WriteMode.Default, dedicatedThreads: true).Should().Be(BufferedStreamWriter.WriteMode.Async);

    /// <summary>
    /// The flag promotes an <em>unstated</em> preference, so an explicit choice still wins - otherwise enabling
    /// it under support guidance would silently undo whatever had been configured to get that far.
    /// </summary>
    /// <remarks>
    /// Takes the test project's public <see cref="WriteMode"/> mirror rather than the internal enum, which a
    /// public test signature cannot name (CS0051) - the same reason that mirror exists at all.
    /// </remarks>
    [Theory]
    [InlineData(WriteMode.Async)]
    [InlineData(WriteMode.Pipe)]
    [InlineData(WriteMode.Sync)]
    public void with_the_flag_an_explicit_mode_is_kept(WriteMode configured)
    {
        var expected = (BufferedStreamWriter.WriteMode)configured;
        PhysicalConnection.ResolveWriteMode(ConnectionType.Interactive, expected, dedicatedThreads: true).Should().Be(expected);
    }

    /// <summary>
    /// The flag is reachable by name, case-insensitively, exactly as <c>preventthreadtheft</c> is - which is
    /// how it will actually be typed into an application's startup under support guidance.
    /// </summary>
    [Fact]
    public void the_flag_is_settable_by_name()
    {
        ConnectionMultiplexer.GetFeatureFlag("DedicatedThreads").Should().BeFalse();
        try
        {
            ConnectionMultiplexer.SetFeatureFlag("dedicatedthreads", true);
            ConnectionMultiplexer.GetFeatureFlag("DedicatedThreads").Should().BeTrue();
        }
        finally
        {
            ConnectionMultiplexer.SetFeatureFlag("DedicatedThreads", false);
        }

        ConnectionMultiplexer.GetFeatureFlag("DedicatedThreads").Should().BeFalse();
    }
}
