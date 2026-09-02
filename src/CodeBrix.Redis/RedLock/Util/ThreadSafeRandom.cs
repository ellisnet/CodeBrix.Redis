using System;

namespace CodeBrix.Redis.RedLock.Util; //was previously: RedLockNet.SERedis.Util;

internal static class ThreadSafeRandom
{
    private static readonly Random GlobalRandom = new Random();

    [ThreadStatic]
    private static Random? localRandom;

    public static int Next()
    {
        return GetLocalRandom().Next();
    }

    public static int Next(int maxValue)
    {
        return GetLocalRandom().Next(maxValue);
    }

    public static int Next(int minValue, int maxValue)
    {
        return GetLocalRandom().Next(minValue, maxValue);
    }

    public static double NextDouble()
    {
        return GetLocalRandom().NextDouble();
    }

    public static void NextBytes(byte[] buffer)
    {
        GetLocalRandom().NextBytes(buffer);
    }

    private static Random GetLocalRandom()
    {
        var random = localRandom;

        if (random == null)
        {
            //the lock serialises access to GlobalRandom, which is not thread safe; localRandom is
            //[ThreadStatic], so it cannot have been filled in by another thread in the meantime
            lock (GlobalRandom)
            {
                var seed = GlobalRandom.Next();
                random = new Random(seed);
            }

            localRandom = random;
        }

        return random;
    }
}
