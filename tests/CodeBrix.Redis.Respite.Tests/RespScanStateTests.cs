using System.Diagnostics.CodeAnalysis;
using CodeBrix.Redis.Respite.Messages;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Respite.Tests; //was previously: RESPite.Tests;

// (upstream calls this class RespScannerTests; renamed here so the file and the type name the class
// under test, as the CodeBrix test conventions require)
public class RespScanStateTests
{
    [Fact]
    public void scan_null()
    {
        //Arrange
        RespScanState scanner = default;

        //Act
        var read = scanner.TryRead("_\r\n"u8, out var consumed);

        //Assert
        read.Should().BeTrue();
        consumed.Should().Be(3);
        scanner.TotalBytes.Should().Be(3);
        scanner.Prefix.Should().Be(RespPrefix.Null);
    }
}
