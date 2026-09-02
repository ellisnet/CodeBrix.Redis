using System.Linq;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class Info(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void single_section_success()
    {
        var resp = "$651\r\n# Server\r\nredis_version:8.6.0\r\nredis_git_sha1:00000000\r\nredis_git_dirty:1\r\nredis_build_id:a7d515010e105f80\r\nredis_mode:standalone\r\nos:Linux 6.17.0-14-generic x86_64\r\narch_bits:64\r\nmonotonic_clock:POSIX clock_gettime\r\nmultiplexing_api:epoll\r\natomicvar_api:c11-builtin\r\ngcc_version:14.2.0\r\nprocess_id:16\r\nprocess_supervised:no\r\nrun_id:b5ab1b382ec845e0a6989e550f36c187fdef3bc0\r\ntcp_port:3000\r\nserver_time_usec:1771514460547930\r\nuptime_in_seconds:13\r\nuptime_in_days:0\r\nhz:10\r\nconfigured_hz:10\r\nlru_clock:9906780\r\nexecutable:/data/redis-server\r\nconfig_file:/redis/work/node-0/redis.conf\r\nio_threads_active:0\r\nlistener0:name=tcp,bind=*,bind=-::*,port=3000\r\n\r\n";

        var result = Execute(resp, ResultProcessor.Info);

        Assert.NotNull(result);
        result.Should().ContainSingle();

        var serverSection = result.Single(g => g.Key == "Server");
        serverSection.Count().Should().Be(25);

        var versionPair = serverSection.First(kv => kv.Key == "redis_version");
        versionPair.Value.Should().Be("8.6.0");

        var portPair = serverSection.First(kv => kv.Key == "tcp_port");
        portPair.Value.Should().Be("3000");
    }

    [Fact]
    public void multiple_section_success()
    {
        var resp = "$978\r\n# Server\r\nredis_version:8.6.0\r\nredis_git_sha1:00000000\r\nredis_git_dirty:1\r\nredis_build_id:a7d515010e105f80\r\nredis_mode:standalone\r\nos:Linux 6.17.0-14-generic x86_64\r\narch_bits:64\r\nmonotonic_clock:POSIX clock_gettime\r\nmultiplexing_api:epoll\r\natomicvar_api:c11-builtin\r\ngcc_version:14.2.0\r\nprocess_id:16\r\nprocess_supervised:no\r\nrun_id:b5ab1b382ec845e0a6989e550f36c187fdef3bc0\r\ntcp_port:3000\r\nserver_time_usec:1771514577242937\r\nuptime_in_seconds:130\r\nuptime_in_days:0\r\nhz:10\r\nconfigured_hz:10\r\nlru_clock:9906897\r\nexecutable:/data/redis-server\r\nconfig_file:/redis/work/node-0/redis.conf\r\nio_threads_active:0\r\nlistener0:name=tcp,bind=*,bind=-::*,port=3000\r\n\r\n# Clients\r\nconnected_clients:1\r\ncluster_connections:0\r\nmaxclients:10000\r\nclient_recent_max_input_buffer:0\r\nclient_recent_max_output_buffer:0\r\nblocked_clients:0\r\ntracking_clients:0\r\npubsub_clients:0\r\nwatching_clients:0\r\nclients_in_timeout_table:0\r\ntotal_watched_keys:0\r\ntotal_blocking_keys:0\r\ntotal_blocking_keys_on_nokey:0\r\n\r\n";

        var result = Execute(resp, ResultProcessor.Info);

        Assert.NotNull(result);
        result.Length.Should().Be(2);

        var serverSection = result.Single(g => g.Key == "Server");
        serverSection.Count().Should().Be(25);

        var clientsSection = result.Single(g => g.Key == "Clients");
        clientsSection.Count().Should().Be(13);

        var connectedClients = clientsSection.First(kv => kv.Key == "connected_clients");
        connectedClients.Value.Should().Be("1");
    }

    [Fact]
    public void empty_string_success()
    {
        //Arrange
        var resp = "$0\r\n\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.Info);

        //Assert
        Assert.NotNull(result);
        result.Should().BeEmpty();
    }

    [Fact]
    public void null_bulk_string_success()
    {
        //Arrange
        var resp = "$-1\r\n";

        //Act
        var result = Execute(resp, ResultProcessor.Info);

        //Assert
        Assert.NotNull(result);
        result.Should().BeEmpty();
    }

    [Fact]
    public void no_section_header_uses_default_category()
    {
        var resp = "$26\r\nkey1:value1\r\nkey2:value2\r\n\r\n";
        var result = Execute(resp, ResultProcessor.Info);

        Assert.NotNull(result);
        result.Should().ContainSingle();

        var miscSection = result.Single(g => g.Key == "miscellaneous");
        miscSection.Count().Should().Be(2);
    }
}
