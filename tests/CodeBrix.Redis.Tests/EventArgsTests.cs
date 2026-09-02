using System;
using CodeBrix.TestMocks.Mocking;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class EventArgsTests
{
    [Fact]
    //default! rather than default: several of these constructor parameters are non-nullable
    //reference types and the test passes null on purpose (it only needs an instance to hand to the
    //diagnostic stub). Upstream's Substitute.For<T>(object?[]) took them untyped and never saw it.
    public void event_args_can_be_substituted()
    {
        EndPointEventArgs endpointArgsMock
            = new Mock<EndPointEventArgs>(() => new EndPointEventArgs(default!, default!)).Object;

        RedisErrorEventArgs redisErrorArgsMock
            = new Mock<RedisErrorEventArgs>(() => new RedisErrorEventArgs(default!, default!, default!)).Object;

        ConnectionFailedEventArgs connectionFailedArgsMock
            = new Mock<ConnectionFailedEventArgs>(
                () => new ConnectionFailedEventArgs(default!, default!, default!, default!, default!, default!)).Object;

        InternalErrorEventArgs internalErrorArgsMock
            = new Mock<InternalErrorEventArgs>(
                () => new InternalErrorEventArgs(default!, default!, default!, default!, default!)).Object;

        HashSlotMovedEventArgs hashSlotMovedArgsMock
            = new Mock<HashSlotMovedEventArgs>(
                () => new HashSlotMovedEventArgs(default!, default!, default!, default!)).Object;

        DiagnosticStub stub = new DiagnosticStub();

        stub.ConfigurationChangedBroadcastHandler(default, endpointArgsMock);
        stub.Message.Should().Be(DiagnosticStub.ConfigurationChangedBroadcastHandlerMessage);

        stub.ErrorMessageHandler(default, redisErrorArgsMock);
        stub.Message.Should().Be(DiagnosticStub.ErrorMessageHandlerMessage);

        stub.ConnectionFailedHandler(default, connectionFailedArgsMock);
        stub.Message.Should().Be(DiagnosticStub.ConnectionFailedHandlerMessage);

        stub.InternalErrorHandler(default, internalErrorArgsMock);
        stub.Message.Should().Be(DiagnosticStub.InternalErrorHandlerMessage);

        stub.ConnectionRestoredHandler(default, connectionFailedArgsMock);
        stub.Message.Should().Be(DiagnosticStub.ConnectionRestoredHandlerMessage);

        stub.ConfigurationChangedHandler(default, endpointArgsMock);
        stub.Message.Should().Be(DiagnosticStub.ConfigurationChangedHandlerMessage);

        stub.HashSlotMovedHandler(default, hashSlotMovedArgsMock);
        stub.Message.Should().Be(DiagnosticStub.HashSlotMovedHandlerMessage);
    }

    public class DiagnosticStub
    {
        public const string ConfigurationChangedBroadcastHandlerMessage = "ConfigurationChangedBroadcastHandler invoked";
        public const string ErrorMessageHandlerMessage = "ErrorMessageHandler invoked";
        public const string ConnectionFailedHandlerMessage = "ConnectionFailedHandler invoked";
        public const string InternalErrorHandlerMessage = "InternalErrorHandler invoked";
        public const string ConnectionRestoredHandlerMessage = "ConnectionRestoredHandler invoked";
        public const string ConfigurationChangedHandlerMessage = "ConfigurationChangedHandler invoked";
        public const string HashSlotMovedHandlerMessage = "HashSlotMovedHandler invoked";

        public DiagnosticStub()
        {
            ConfigurationChangedBroadcastHandler = (obj, args) => Message = ConfigurationChangedBroadcastHandlerMessage;
            ErrorMessageHandler = (obj, args) => Message = ErrorMessageHandlerMessage;
            ConnectionFailedHandler = (obj, args) => Message = ConnectionFailedHandlerMessage;
            InternalErrorHandler = (obj, args) => Message = InternalErrorHandlerMessage;
            ConnectionRestoredHandler = (obj, args) => Message = ConnectionRestoredHandlerMessage;
            ConfigurationChangedHandler = (obj, args) => Message = ConfigurationChangedHandlerMessage;
            HashSlotMovedHandler = (obj, args) => Message = HashSlotMovedHandlerMessage;
        }

        public string? Message { get; private set; }
        public Action<object?, EndPointEventArgs> ConfigurationChangedBroadcastHandler { get; }
        public Action<object?, RedisErrorEventArgs> ErrorMessageHandler { get; }
        public Action<object?, ConnectionFailedEventArgs> ConnectionFailedHandler { get; }
        public Action<object?, InternalErrorEventArgs> InternalErrorHandler { get; }
        public Action<object?, ConnectionFailedEventArgs> ConnectionRestoredHandler { get; }
        public Action<object?, EndPointEventArgs> ConfigurationChangedHandler { get; }
        public Action<object?, HashSlotMovedEventArgs> HashSlotMovedHandler { get; }
    }
}
