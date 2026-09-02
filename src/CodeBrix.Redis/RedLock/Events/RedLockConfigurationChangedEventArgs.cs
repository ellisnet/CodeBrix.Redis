using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace CodeBrix.Redis.RedLock.Events; //was previously: RedLockNet.SERedis.Events;

/// <summary>
/// Raised when one of the factory's connections reports a configuration change, carrying the state of
/// every endpoint at that moment so a listener can tell whether a quorum is still reachable.
/// </summary>
public class RedLockConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// The state of a single endpoint at the moment the configuration changed.
    /// </summary>
    public class RedLockEndPointStatus
    {
        /// <summary>
        /// The address of the endpoint.
        /// </summary>
        public EndPoint EndPoint { get; }

        /// <summary>
        /// Whether the endpoint was connected.
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// Whether the endpoint was a replica rather than a primary.
        /// </summary>
        public bool IsSlave { get; }

        /// <summary>
        /// Records the state of a single endpoint.
        /// </summary>
        /// <param name="endPoint">The address of the endpoint.</param>
        /// <param name="isConnected">Whether the endpoint was connected.</param>
        /// <param name="isSlave">Whether the endpoint was a replica rather than a primary.</param>
        public RedLockEndPointStatus(EndPoint endPoint, bool isConnected, bool isSlave)
        {
            this.EndPoint = endPoint;
            this.IsConnected = isConnected;
            this.IsSlave = isSlave;
        }
    }

    /// <summary>
    /// One dictionary per Redlock instance, holding the state of every endpoint that instance is reached through.
    /// </summary>
    public ICollection<Dictionary<EndPoint, RedLockEndPointStatus>> EndPointConnections { get; }

    /// <summary>
    /// Records the state of every endpoint of every instance.
    /// </summary>
    /// <param name="connections">One dictionary per Redlock instance, holding the state of that instance's endpoints.</param>
    public RedLockConfigurationChangedEventArgs(ICollection<Dictionary<EndPoint, RedLockEndPointStatus>> connections)
    {
        this.EndPointConnections = connections;
    }

    /// <summary>
    /// The number of instances that must be held for a lock to be valid.
    /// </summary>
    public int Quorum => this.InstancesCount / 2 + 1;

    /// <summary>
    /// Whether enough instances have a connected primary to reach <see cref="Quorum"/>.
    /// </summary>
    public bool HasQuorum => this.InstancesWithConnectedMastersCount >= Quorum;

    /// <summary>
    /// The number of Redlock instances.
    /// </summary>
    public int InstancesCount => this.EndPointConnections.Count;

    /// <summary>
    /// The number of instances with at least one connected, non-replica endpoint.
    /// </summary>
    public int InstancesWithConnectedMastersCount
    {
        get
        {
            var result = 0;

            foreach (var instance in this.EndPointConnections)
            {
                if (instance.Any(x => x.Value.IsConnected && !x.Value.IsSlave))
                {
                    result++;
                }
            }

            return result;
        }
    }
}
