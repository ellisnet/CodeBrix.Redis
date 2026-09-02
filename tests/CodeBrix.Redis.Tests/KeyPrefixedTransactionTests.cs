using System.Text;
using System.Threading.Tasks;
using CodeBrix.Redis.KeyspaceIsolation;
using CodeBrix.TestMocks.Mocking;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(nameof(SubstituteDependentCollection))]
public sealed class KeyPrefixedTransactionTests
{
    private readonly Mock<ITransaction> mock;
    private readonly KeyPrefixedTransaction prefixed;

    public KeyPrefixedTransactionTests()
    {
        mock = new Mock<ITransaction>();
        prefixed = new KeyPrefixedTransaction(mock.Object, Encoding.UTF8.GetBytes("prefix:"));
    }

    [Fact]
    public void add_condition_hash_equal()
    {
        prefixed.AddCondition(Condition.HashEqual("key", "field", "value"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key Hash > field == value" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_hash_not_equal()
    {
        prefixed.AddCondition(Condition.HashNotEqual("key", "field", "value"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key Hash > field != value" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_hash_exists()
    {
        prefixed.AddCondition(Condition.HashExists("key", "field"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key Hash > field exists" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_hash_not_exists()
    {
        prefixed.AddCondition(Condition.HashNotExists("key", "field"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key Hash > field does not exists" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_key_exists()
    {
        prefixed.AddCondition(Condition.KeyExists("key"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key exists" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_key_not_exists()
    {
        prefixed.AddCondition(Condition.KeyNotExists("key"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key does not exists" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_string_equal()
    {
        prefixed.AddCondition(Condition.StringEqual("key", "value"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key == value" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_string_not_equal()
    {
        prefixed.AddCondition(Condition.StringNotEqual("key", "value"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key != value" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_sorted_set_equal()
    {
        prefixed.AddCondition(Condition.SortedSetEqual("key", "member", "score"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key SortedSet > member == score" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_sorted_set_not_equal()
    {
        prefixed.AddCondition(Condition.SortedSetNotEqual("key", "member", "score"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key SortedSet > member != score" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_sorted_set_score_exists()
    {
        prefixed.AddCondition(Condition.SortedSetScoreExists("key", "score"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key not contains 0 members with score: score" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_sorted_set_score_not_exists()
    {
        prefixed.AddCondition(Condition.SortedSetScoreNotExists("key", "score"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key contains 0 members with score: score" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_sorted_set_score_count_exists()
    {
        prefixed.AddCondition(Condition.SortedSetScoreExists("key", "score", "count"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key contains count members with score: score" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public void add_condition_sorted_set_score_count_not_exists()
    {
        prefixed.AddCondition(Condition.SortedSetScoreNotExists("key", "score", "count"));
        mock.Verify(x => x.AddCondition(It.Is<Condition>(value => "prefix:key not contains count members with score: score" == value.ToString())), Times.AtLeastOnce());
    }

    [Fact]
    public async Task execute_async()
    {
        await prefixed.ExecuteAsync(CommandFlags.None);
        mock.Verify(x => x.ExecuteAsync(CommandFlags.None), Times.Once());
    }

    [Fact]
    public void execute()
    {
        prefixed.Execute(CommandFlags.None);
        mock.Verify(x => x.Execute(CommandFlags.None), Times.Once());
    }
}
