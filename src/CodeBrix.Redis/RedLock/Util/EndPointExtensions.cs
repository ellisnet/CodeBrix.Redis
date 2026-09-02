using System.Net;

namespace CodeBrix.Redis.RedLock.Util; //was previously: RedLockNet.SERedis.Util;

internal static class EndPointExtensions
{
    internal static string GetFriendlyName(this EndPoint? endPoint)
    {
        //ConnectionFailedEventArgs.EndPoint is nullable in the client core, so this has to cope
        if (endPoint == null)
        {
            return "(unknown)";
        }

        var dnsEndPoint = endPoint as DnsEndPoint;

        if (dnsEndPoint != null)
        {
            return $"{dnsEndPoint.Host}:{dnsEndPoint.Port}";
        }

        var ipEndPoint = endPoint as IPEndPoint;

        if (ipEndPoint != null)
        {
            return $"{ipEndPoint.Address}:{ipEndPoint.Port}";
        }

        return endPoint.ToString() ?? string.Empty;
    }
}
