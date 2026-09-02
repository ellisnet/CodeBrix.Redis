using System.Threading.Tasks;

namespace CodeBrix.Redis; //was previously: StackExchange.Redis;

internal partial class RedisServer
{
    public ClusterSlotsResult? ClusterSlots(CommandFlags flags = CommandFlags.None)
        => ExecuteSync(Message.Create(-1, flags, RedisCommand.CLUSTER, RedisLiterals.SLOTS), ClusterSlotsResult.Processor);

    public Task<ClusterSlotsResult?> ClusterSlotsAsync(CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(Message.Create(-1, flags, RedisCommand.CLUSTER, RedisLiterals.SLOTS), ClusterSlotsResult.Processor);
}
