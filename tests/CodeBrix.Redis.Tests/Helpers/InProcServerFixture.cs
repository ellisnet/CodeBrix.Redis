using System;
using CodeBrix.Redis.Configuration;
using Xunit;

[assembly: AssemblyFixture(typeof(CodeBrix.Redis.Tests.InProcServerFixture))]

// ReSharper disable once CheckNamespace
namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class InProcServerFixture : IDisposable
{
    private readonly InProcessTestServer _server = new();
    private readonly ConfigurationOptions _config;
    public InProcServerFixture()
    {
        _config = _server.GetClientConfig();
        Configuration = _config.ToString();
    }

    public ConfigurationOptions Config => _config;

    public string Configuration { get; }

    public Tunnel? Tunnel => _server.Tunnel;

    public void Dispose()
    {
        try { _server.Dispose(); } catch { }
    }
}
