using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class ClientInfo(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void single_client_success()
    {
        // CLIENT LIST returns a bulk string with newline-separated client information
        var content = "id=86 addr=172.17.0.1:40750 laddr=172.17.0.2:3000 fd=22 name= age=7 idle=0 flags=N db=0 sub=0 psub=0 ssub=0 multi=-1 watch=0 qbuf=26 qbuf-free=20448 argv-mem=10 multi-mem=0 rbs=1024 rbp=0 obl=0 oll=0 omem=0 tot-mem=22810 events=r cmd=client|list user=default redir=-1 resp=2 lib-name= lib-ver= io-thread=0 tot-net-in=48 tot-net-out=36 tot-cmds=0\n";
        var resp = $"${content.Length}\r\n{content}\r\n";

        var result = Execute(resp, CodeBrix.Redis.ClientInfo.Processor);

        Assert.NotNull(result);
        result.Should().ContainSingle();
        result[0].Id.Should().Be(86);
        (result[0].Address?.ToString()).Should().Be("172.17.0.1:40750");
        result[0].AgeSeconds.Should().Be(7);
        result[0].Database.Should().Be(0);
    }

    [Fact]
    public void multiple_clients_success()
    {
        // Two clients (newline-separated, using \n not \r\n)
        var line1 = "id=86 addr=172.17.0.1:40750 laddr=172.17.0.2:3000 fd=22 name= age=39 idle=32 flags=N db=0 sub=0 psub=0 ssub=0 multi=-1 watch=0 qbuf=0 qbuf-free=0 argv-mem=0 multi-mem=0 rbs=1024 rbp=0 obl=0 oll=0 omem=0 tot-mem=2304 events=r cmd=client|list user=default redir=-1 resp=2 lib-name= lib-ver= io-thread=0 tot-net-in=48 tot-net-out=390 tot-cmds=1\n";
        var line2 = "id=87 addr=172.17.0.1:60630 laddr=172.17.0.2:3000 fd=23 name= age=4 idle=0 flags=N db=0 sub=0 psub=0 ssub=0 multi=-1 watch=0 qbuf=26 qbuf-free=20448 argv-mem=10 multi-mem=0 rbs=1024 rbp=7 obl=0 oll=0 omem=0 tot-mem=22810 events=r cmd=client|list user=default redir=-1 resp=2 lib-name= lib-ver= io-thread=0 tot-net-in=40 tot-net-out=7 tot-cmds=1\n";
        var content = line1 + line2;
        var resp = $"${content.Length}\r\n{content}\r\n";

        var result = Execute(resp, CodeBrix.Redis.ClientInfo.Processor);

        Assert.NotNull(result);
        result.Length.Should().Be(2);
        result[0].Id.Should().Be(86);
        result[1].Id.Should().Be(87);
        (result[0].Address?.ToString()).Should().Be("172.17.0.1:40750");
        (result[1].Address?.ToString()).Should().Be("172.17.0.1:60630");
    }

    [Fact]
    public void empty_string_success()
    {
        // Empty bulk string
        var resp = "$0\r\n\r\n";

        var result = Execute(resp, CodeBrix.Redis.ClientInfo.Processor);

        Assert.NotNull(result);
        result.Should().BeEmpty();
    }

    [Fact]
    public void null_bulk_string_failure()
    {
        // Null bulk string should fail
        var resp = "$-1\r\n";

        ExecuteUnexpected(resp, CodeBrix.Redis.ClientInfo.Processor);
    }

    [Fact]
    public void not_bulk_string_failure()
    {
        // Simple string should fail
        var resp = "+OK\r\n";

        ExecuteUnexpected(resp, CodeBrix.Redis.ClientInfo.Processor);
    }

    [Fact]
    public void verbatim_string_success()
    {
        // Verbatim string with TXT encoding - should behave identically to bulk string
        // Format: ={len}\r\n{enc}:{payload}\r\n where enc is exactly 3 bytes (e.g., "TXT")
        var content = "id=86 addr=172.17.0.1:40750 laddr=172.17.0.2:3000 fd=22 name= age=7 idle=0 flags=N db=0 sub=0 psub=0 ssub=0 multi=-1 watch=0 qbuf=26 qbuf-free=20448 argv-mem=10 multi-mem=0 rbs=1024 rbp=0 obl=0 oll=0 omem=0 tot-mem=22810 events=r cmd=client|list user=default redir=-1 resp=2 lib-name= lib-ver= io-thread=0 tot-net-in=48 tot-net-out=36 tot-cmds=0\n";
        var totalLen = 4 + content.Length; // "TXT:" + content
        var resp = $"={totalLen}\r\nTXT:{content}\r\n";

        var result = Execute(resp, CodeBrix.Redis.ClientInfo.Processor);

        // ReadString() automatically strips the "TXT:" encoding prefix,
        // so the result should be identical to the bulk string test
        Assert.NotNull(result);
        result.Should().ContainSingle();
        result[0].Id.Should().Be(86);
        (result[0].Address?.ToString()).Should().Be("172.17.0.1:40750");
        result[0].AgeSeconds.Should().Be(7);
        result[0].Database.Should().Be(0);
    }
}
