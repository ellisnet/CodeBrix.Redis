using System.Text;
using CodeBrix.Redis.KeyspaceIsolation;
using CodeBrix.TestMocks.Mocking;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(nameof(SubstituteDependentCollection))]
public sealed class KeyPrefixedBatchTests
{
    private readonly Mock<IBatch> mock;
    private readonly KeyPrefixedBatch prefixed;

    public KeyPrefixedBatchTests()
    {
        mock = new Mock<IBatch>();
        prefixed = new KeyPrefixedBatch(mock.Object, Encoding.UTF8.GetBytes("prefix:"));
    }

    [Fact]
    public void execute()
    {
        prefixed.Execute();
        mock.Verify(x => x.Execute(), Times.Once());
    }
}
