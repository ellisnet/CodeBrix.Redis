using System;
using System.Buffers;
using System.Threading.Tasks;

namespace CodeBrix.Redis.Availability; //was previously: StackExchange.Redis.Availability;

public abstract partial class HealthCheckProbe
{
    /// <summary>
    /// Verify that a string can be successfully set and retrieved.
    /// </summary>
    public static HealthCheckProbe StringSet => StringSetProbe.Instance;

    internal sealed class StringSetProbe : KeyWriteHealthCheckProbe
    {
        public static StringSetProbe Instance { get; } = new();
        private StringSetProbe() { }

        public override async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, IDatabaseAsync database, RedisKey key)
        {
            // note we use the lock API here because that can selectively choose between appropriate strategies for
            // different server versions, including DELEX
            const int LEN = 16;
            var pooled = ArrayPool<byte>.Shared.Rent(LEN);
            Random.Shared.NextBytes(pooled.AsSpan(0, LEN));
            var payload = (RedisValue)pooled.AsMemory(0, LEN);
            try
            {
                // write a value to the db
                await database.LockTakeAsync(
                    key: key,
                    value: payload,
                    expiry: context.ProbeTimeout,
                    flags: CommandFlags.FireAndForget).ForAwait();

                // release from the db if matches (otherwise, we have no clue what happened, so: leave alone)
                var success = await database.LockReleaseAsync(key, payload).ForAwait();
                return success ? HealthCheckResult.Healthy : HealthCheckResult.Unhealthy;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pooled);
            }
        }
    }
}
