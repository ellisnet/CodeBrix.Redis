using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]

public class ClientKillTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task client_kill()
    {
        //Arrange
        SetExpectedAmbientFailureCount(-1);
        await using var otherConnection = Create(allowAdmin: true, shared: false, backlogPolicy: BacklogPolicy.FailFast, require: RedisFeatures.v7_4_0_rc1);
        var id = otherConnection.GetDatabase().Execute(RedisCommand.CLIENT.ToString(), RedisLiterals.ID);
        await using var conn = Create(allowAdmin: true, shared: false, backlogPolicy: BacklogPolicy.FailFast);
        var server = conn.GetServer(conn.GetEndPoints()[0]);

        //Act
        long result = server.ClientKill(id.AsInt64(), ClientType.Normal, null, true);

        //Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task client_kill_with_max_age()
    {
        //Arrange
        SetExpectedAmbientFailureCount(-1);
        await using var otherConnection = Create(allowAdmin: true, shared: false, backlogPolicy: BacklogPolicy.FailFast, require: RedisFeatures.v7_4_0_rc1);
        var id = otherConnection.GetDatabase().Execute(RedisCommand.CLIENT.ToString(), RedisLiterals.ID);
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        await using var conn = Create(allowAdmin: true, shared: false, backlogPolicy: BacklogPolicy.FailFast);
        var server = conn.GetServer(conn.GetEndPoints()[0]);
        var filter = new ClientKillFilter().WithId(id.AsInt64()).WithMaxAgeInSeconds(1).WithSkipMe(true);

        //Act
        long result = server.ClientKill(filter, CommandFlags.DemandMaster);

        //Assert
        result.Should().Be(1);
    }

    [Fact]
    public void test_client_kill_message_with_all_arguments()
    {
        long id = 101;
        ClientType type = ClientType.Normal;
        string userName = "user1";
        EndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1234);
        EndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse("198.0.0.1"), 6379);
        bool skipMe = true;
        long maxAge = 102;

        var filter = new ClientKillFilter().WithId(id).WithClientType(type).WithUsername(userName).WithEndpoint(endpoint).WithServerEndpoint(serverEndpoint).WithSkipMe(skipMe).WithMaxAgeInSeconds(maxAge);
        List<RedisValue> expected =
        [
            "KILL", "ID", "101", "TYPE", "normal", "USERNAME", "user1", "ADDR", "127.0.0.1:1234", "LADDR", "198.0.0.1:6379", "SKIPME", "yes", "MAXAGE", "102",
        ];
        filter.ToList(true).Should().Equal(expected);
    }
}
