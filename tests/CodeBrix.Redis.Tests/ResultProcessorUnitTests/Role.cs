using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class RoleTests(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void role_master_no_replicas()
    {
        // 1) "master"
        // 2) (integer) 3129659
        // 3) (empty array)
        var resp = "*3\r\n$6\r\nmaster\r\n:3129659\r\n*0\r\n";
        var processor = ResultProcessor.Role;
        var result = Execute(resp, processor);

        result.Should().NotBeNull();
        var master = Assert.IsType<Role.Master>(result);
        master.Value.Should().Be("master");
        master.ReplicationOffset.Should().Be(3129659);
        master.Replicas.Should().NotBeNull();
        master.Replicas.Should().BeEmpty();
    }

    [Fact]
    public void role_master_with_replicas()
    {
        // 1) "master"
        // 2) (integer) 3129659
        // 3) 1) 1) "127.0.0.1"
        //       2) "9001"
        //       3) "3129242"
        //    2) 1) "127.0.0.1"
        //       2) "9002"
        //       3) "3129543"
        var resp = "*3\r\n" +
                   "$6\r\nmaster\r\n" +
                   ":3129659\r\n" +
                   "*2\r\n" +
                   "*3\r\n$9\r\n127.0.0.1\r\n$4\r\n9001\r\n$7\r\n3129242\r\n" +
                   "*3\r\n$9\r\n127.0.0.1\r\n$4\r\n9002\r\n$7\r\n3129543\r\n";
        var processor = ResultProcessor.Role;
        var result = Execute(resp, processor);

        result.Should().NotBeNull();
        var master = Assert.IsType<Role.Master>(result);
        master.Value.Should().Be("master");
        master.ReplicationOffset.Should().Be(3129659);
        master.Replicas.Should().NotBeNull();
        master.Replicas.Count.Should().Be(2);

        var replicas = new System.Collections.Generic.List<Role.Master.Replica>(master.Replicas);
        replicas[0].Ip.Should().Be("127.0.0.1");
        replicas[0].Port.Should().Be(9001);
        replicas[0].ReplicationOffset.Should().Be(3129242);

        replicas[1].Ip.Should().Be("127.0.0.1");
        replicas[1].Port.Should().Be(9002);
        replicas[1].ReplicationOffset.Should().Be(3129543);
    }

    [Theory]
    [InlineData("slave")]
    [InlineData("replica")]
    public void role_replica_connected(string roleType)
    {
        // 1) "slave" (or "replica")
        // 2) "127.0.0.1"
        // 3) (integer) 9000
        // 4) "connected"
        // 5) (integer) 3167038
        var resp = $"*5\r\n${roleType.Length}\r\n{roleType}\r\n$9\r\n127.0.0.1\r\n:9000\r\n$9\r\nconnected\r\n:3167038\r\n";
        var processor = ResultProcessor.Role;
        var result = Execute(resp, processor);

        result.Should().NotBeNull();
        var replica = Assert.IsType<Role.Replica>(result);
        replica.Value.Should().Be(roleType);
        replica.MasterIp.Should().Be("127.0.0.1");
        replica.MasterPort.Should().Be(9000);
        replica.State.Should().Be("connected");
        replica.ReplicationOffset.Should().Be(3167038);
    }

    [Theory]
    [InlineData("connect")]
    [InlineData("connecting")]
    [InlineData("sync")]
    [InlineData("connected")]
    [InlineData("none")]
    [InlineData("handshake")]
    public void role_replica_various_states(string state)
    {
        //Arrange
        var resp = $"*5\r\n$5\r\nslave\r\n$9\r\n127.0.0.1\r\n:9000\r\n${state.Length}\r\n{state}\r\n:3167038\r\n";
        var processor = ResultProcessor.Role;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().NotBeNull();
        var replica = Assert.IsType<Role.Replica>(result);
        replica.State.Should().Be(state);
    }

    [Fact]
    public void role_sentinel()
    {
        // 1) "sentinel"
        // 2) 1) "resque-master"
        //    2) "html-fragments-master"
        //    3) "stats-master"
        //    4) "metadata-master"
        var resp = "*2\r\n" +
                   "$8\r\nsentinel\r\n" +
                   "*4\r\n" +
                   "$13\r\nresque-master\r\n" +
                   "$21\r\nhtml-fragments-master\r\n" +
                   "$12\r\nstats-master\r\n" +
                   "$15\r\nmetadata-master\r\n";
        var processor = ResultProcessor.Role;
        var result = Execute(resp, processor);

        result.Should().NotBeNull();
        var sentinel = Assert.IsType<Role.Sentinel>(result);
        sentinel.Value.Should().Be("sentinel");
        sentinel.MonitoredMasters.Should().NotBeNull();
        sentinel.MonitoredMasters.Count.Should().Be(4);

        var masters = new System.Collections.Generic.List<string?>(sentinel.MonitoredMasters);
        masters[0].Should().Be("resque-master");
        masters[1].Should().Be("html-fragments-master");
        masters[2].Should().Be("stats-master");
        masters[3].Should().Be("metadata-master");
    }

    [Theory]
    [InlineData("unknown", false)] // Short value - tests TryGetSpan path
    [InlineData("unknown", true)]
    [InlineData("long_value_to_test_buffer_size", true)] // Streaming scalar - tests Buffer path (TryGetSpan fails on non-contiguous)
    public void role_unknown(string roleName, bool streaming)
    {
        //Arrange
        var resp = streaming
            ? $"*1\r\n$?\r\n;{roleName.Length}\r\n{roleName}\r\n;6\r\n_extra\r\n;0\r\n" // force an extra chunk
            : $"*1\r\n${roleName.Length}\r\n{roleName}\r\n";
        var processor = ResultProcessor.Role;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().NotBeNull();
        var unknown = Assert.IsType<Role.Unknown>(result);
        unknown.Value.Should().Be(roleName + (streaming ? "_extra" : ""));
    }

    [Fact]
    public void role_empty_array_returns_null()
    {
        //Arrange
        var resp = "*0\r\n";
        var processor = ResultProcessor.Role;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }
}
