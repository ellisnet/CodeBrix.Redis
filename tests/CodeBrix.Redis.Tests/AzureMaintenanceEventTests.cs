using System;
using System.Globalization;
using System.Net;
using CodeBrix.Redis.Maintenance;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class AzureMaintenanceEventTests(ITestOutputHelper output) : TestBase(output)
{
    [Theory]
    [InlineData("NotificationType|NodeMaintenanceStarting|StartTimeInUTC|2021-03-02T23:26:57|IsReplica|False|IPAddress||SSLPort|15001|NonSSLPort|13001", AzureNotificationType.NodeMaintenanceStarting, "2021-03-02T23:26:57", false, null, 15001, 13001)]
    [InlineData("NotificationType|NodeMaintenanceFailover|StartTimeInUTC||IsReplica|False|IPAddress||SSLPort|15001|NonSSLPort|13001", AzureNotificationType.NodeMaintenanceFailoverComplete, null, false, null, 15001, 13001)]
    [InlineData("NotificationType|NodeMaintenanceFailover|StartTimeInUTC||IsReplica|True|IPAddress||SSLPort|15001|NonSSLPort|13001", AzureNotificationType.NodeMaintenanceFailoverComplete, null, true, null, 15001, 13001)]
    [InlineData("NotificationType|NodeMaintenanceStarting|StartTimeInUTC|2021-03-02T23:26:57|IsReplica|j|IPAddress||SSLPort|char|NonSSLPort|char", AzureNotificationType.NodeMaintenanceStarting, "2021-03-02T23:26:57", false, null, 0, 0)]
    [InlineData("NotificationType|NodeMaintenanceStarting|somejunkkey|somejunkvalue|StartTimeInUTC|2021-03-02T23:26:57|IsReplica|False|IPAddress||SSLPort|15999|NonSSLPort|139991", AzureNotificationType.NodeMaintenanceStarting, "2021-03-02T23:26:57", false, null, 15999, 139991)]
    [InlineData("NotificationType|NodeMaintenanceStarting|somejunkkey|somejunkvalue|StartTimeInUTC|2021-03-02T23:26:57|IsReplica|False|IPAddress|127.0.0.1|SSLPort|15999|NonSSLPort|139991", AzureNotificationType.NodeMaintenanceStarting, "2021-03-02T23:26:57", false, "127.0.0.1", 15999, 139991)]
    [InlineData("NotificationType|NodeMaintenanceScaleComplete|somejunkkey|somejunkvalue|StartTimeInUTC|2021-03-02T23:26:57|IsReplica|False|IPAddress|127.0.0.1|SSLPort|15999|NonSSLPort|139991", AzureNotificationType.NodeMaintenanceScaleComplete, "2021-03-02T23:26:57", false, "127.0.0.1", 15999, 139991)]
    [InlineData("NotificationTypeNodeMaintenanceStartingsomejunkkeysomejunkvalueStartTimeInUTC2021-03-02T23:26:57IsReplicaFalseIPAddress127.0.0.1SSLPort15999NonSSLPort139991", AzureNotificationType.Unknown, null, false, null, 0, 0)]
    [InlineData("NotificationType|", AzureNotificationType.Unknown, null, false, null, 0, 0)]
    [InlineData("NotificationType|NodeMaintenanceStarting1", AzureNotificationType.Unknown, null, false, null, 0, 0)]
    [InlineData("1|2|3", AzureNotificationType.Unknown, null, false, null, 0, 0)]
    [InlineData("StartTimeInUTC|", AzureNotificationType.Unknown, null, false, null, 0, 0)]
    [InlineData("IsReplica|", AzureNotificationType.Unknown, null, false, null, 0, 0)]
    [InlineData("SSLPort|", AzureNotificationType.Unknown, null, false, null, 0, 0)]
    [InlineData("NonSSLPort |", AzureNotificationType.Unknown, null, false, null, 0, 0)]
    [InlineData("StartTimeInUTC|thisisthestart", AzureNotificationType.Unknown, null, false, null, 0, 0)]
    [InlineData(null, AzureNotificationType.Unknown, null, false, null, 0, 0)]
    public void test_azure_maintenance_event_strings(string? message, AzureNotificationType expectedEventType, string? expectedStart, bool expectedIsReplica, string? expectedIP, int expectedSSLPort, int expectedNonSSLPort)
    {
        DateTime? expectedStartTimeUtc = null;
        if (expectedStart != null && DateTime.TryParseExact(expectedStart, "s", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime startTimeUtc))
        {
            expectedStartTimeUtc = DateTime.SpecifyKind(startTimeUtc, DateTimeKind.Utc);
        }
        _ = IPAddress.TryParse(expectedIP, out IPAddress? expectedIPAddress);

        var azureMaintenance = new AzureMaintenanceEvent(message);

        azureMaintenance.NotificationType.Should().Be(expectedEventType);
        azureMaintenance.StartTimeUtc.Should().Be(expectedStartTimeUtc);
        azureMaintenance.IsReplica.Should().Be(expectedIsReplica);
        azureMaintenance.IPAddress.Should().Be(expectedIPAddress);
        azureMaintenance.SslPort.Should().Be(expectedSSLPort);
        azureMaintenance.NonSslPort.Should().Be(expectedNonSSLPort);
    }
}
