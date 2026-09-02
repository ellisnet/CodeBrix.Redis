//Adapted from RedisSetupTool.DockerManagement in the CodeBrix.Docker samples.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.Redis.Testing.Probe;

namespace CodeBrix.Redis.Testing.Topologies;

/// <summary>
/// What every topology shares: one container, its configuration files, its published ports, the
/// idempotent start that reuses a container the harness already left running, and the teardown.
/// </summary>
/// <remarks>
/// <para>
/// Each topology is ONE container running every server that topology needs, which is how upstream's
/// own <c>docker-compose.yml</c> arranges things and is the reason the ported suite's expectations
/// survive the move into containers. Every server in a topology shares one network namespace, so
/// <c>slaveof 127.0.0.1 6379</c>, <c>sentinel monitor myprimary 127.0.0.1 7010</c> and
/// <c>cluster-announce-ip 127.0.0.1</c> all mean what they say - and because the same ports are
/// published to the host one-for-one, an address a server hands out (a <c>MOVED</c> redirect, an
/// <c>INFO replication</c> line, a <c>SENTINEL masters</c> row) is reachable from the test process
/// under exactly the address it was given. That is what upstream's <c>TestConfig</c> assumes
/// throughout: every server lives at <c>127.0.0.1</c> on its own well-known port.
/// </para>
/// <para>
/// The ports are therefore NOT allocated dynamically. They are upstream's, because the ported
/// suite's <c>TestConfig</c> defaults name them: 6379, 6380, 6381, 6382, 6383, 6384, 7000-7005,
/// 7010, 7011, 7015 and 26379-26381.
/// </para>
/// </remarks>
public abstract class RedisTopologyBase : IAsyncDisposable
{
    private readonly bool _ownsClient;
    private DockerClient _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="RedisTopologyBase"/> class.</summary>
    /// <param name="name">
    /// The topology's short name; it becomes the container name suffix and the topology label.
    /// </param>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <param name="client">
    /// A Docker client to borrow, or null to create and own one. A caller starting several
    /// topologies should share one client: it owns a pooled HTTP connection.
    /// </param>
    protected RedisTopologyBase(string name, RedisHarnessOptions options, DockerClient client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Options = options ?? new RedisHarnessOptions();
        _client = client;
        _ownsClient = client is null;
    }

    /// <summary>Gets the topology's short name, for example <c>cluster</c>.</summary>
    public string Name { get; }

    /// <summary>Gets the options this topology was created with.</summary>
    public RedisHarnessOptions Options { get; }

    /// <summary>Gets the container name, which is the resource prefix followed by <see cref="Name"/>.</summary>
    public string ContainerName => RedisHarnessOptions.ResourcePrefix + Name;

    /// <summary>Gets the container id once the topology has started, or null before that.</summary>
    public string ContainerId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="StartAsync"/> found the container already running
    /// and reused it rather than creating one.
    /// </summary>
    public bool ReusedExistingContainer { get; private set; }

    /// <summary>Gets a value indicating whether the topology is started.</summary>
    public bool IsStarted => ContainerId is not null;

    /// <summary>Gets every endpoint this topology publishes, in the order it declares them.</summary>
    public abstract IReadOnlyList<RedisEndpoint> Endpoints { get; }

    /// <summary>Gets the image this topology's container runs.</summary>
    protected virtual string Image => Options.Image;

    /// <summary>Gets the shared Docker client, creating one on first use when none was supplied.</summary>
    protected DockerClient Client
    {
        get
        {
            _client ??= DockerClient.Create(
                string.IsNullOrWhiteSpace(Options.DockerEndpoint)
                    ? null
                    : new DockerClientOptions { Endpoint = Options.DockerEndpoint });
            return _client;
        }
    }

    /// <summary>
    /// Gets the name of the user-defined bridge network every harness container joins. It exists so
    /// the proxy topology can reach the cluster topology by container name; nothing else needs it,
    /// and it costs nothing.
    /// </summary>
    public static string NetworkName => RedisHarnessOptions.ResourcePrefix + "net";

    /// <summary>
    /// Starts the topology, or adopts the one already running, and waits until it is ready to serve.
    /// Calling it twice is a no-op the second time.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>How long the start took, readiness included.</returns>
    public async Task<TimeSpan> StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsStarted)
        {
            return TimeSpan.Zero;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        Report("starting");

        await EnsureImageAsync(Image, cancellationToken).ConfigureAwait(false);
        await EnsureNetworkAsync(cancellationToken).ConfigureAwait(false);
        await OnBeforeCreateAsync(cancellationToken).ConfigureAwait(false);

        var existing = await FindContainerAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            ContainerId = existing.Id;
            ReusedExistingContainer = true;
            if (!existing.IsRunning)
            {
                Report("starting the container it found stopped");
                await Client.Containers.StartAsync(ContainerName, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                Report("reusing the container already running");
            }
        }
        else
        {
            var spec = BuildSpec();
            try
            {
                ContainerId = await Client.Containers.RunAsync(spec, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DockerApiException exception)
            {
                //A container that was created and then failed to start would be adopted by the next
                //run as "already there", so it does not get to survive the failure that made it.
                await RemoveQuietlyAsync().ConfigureAwait(false);
                throw new HarnessException(
                    "Could not start the " + Name + " topology's container ("
                    + ContainerName + "). The ports it publishes are fixed, because the ported test"
                    + " suite's TestConfig names them: "
                    + string.Join(", ", PublishedPorts().Select(port =>
                        port.ToString(CultureInfo.InvariantCulture)))
                    + ". Free them on the host, or stop whatever is holding them, and try again.",
                    exception);
            }
        }

        await WaitForReadyAsync(cancellationToken).ConfigureAwait(false);
        Report("ready in " + clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)
            + " s");
        return clock.Elapsed;
    }

    /// <summary>
    /// Stops the topology and removes its container and the anonymous volume that carried its data
    /// directory. Safe to call when the topology was never started.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the container is gone.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Report("removing");
            await Client.Containers
                .RemoveAsync(ContainerName, force: true, removeVolumes: true, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DockerContainerNotFoundException)
        {
            //Already gone, which is the state this method is asking for.
        }
        finally
        {
            ContainerId = null;
            ReusedExistingContainer = false;
        }
    }

    /// <summary>
    /// Reads the container's log, which is where a topology that will not come up says why.
    /// </summary>
    /// <param name="tail">How many lines to read, or null for all of them.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The container's standard output and standard error.</returns>
    public async Task<string> GetLogAsync(int? tail = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await Client.Containers
                .GetLogsAsync(ContainerName, tail, timestamps: false, cancellationToken)
                .ConfigureAwait(false);
            return logs.Combined;
        }
        catch (DockerContainerNotFoundException)
        {
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (!Options.LeaveContainersRunning)
        {
            try
            {
                await StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (DockerException)
            {
                //Teardown is best effort: a daemon that has gone away cannot be asked to tidy up.
            }
        }

        _disposed = true;
        OnDispose();

        if (_ownsClient)
        {
            _client?.Dispose();
            _client = null;
        }
    }

    /// <summary>Waits until every server in the topology is serving.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the topology is ready.</returns>
    protected abstract Task WaitForReadyAsync(CancellationToken cancellationToken);

    /// <summary>Builds the shell script the container runs as its whole command.</summary>
    /// <returns>The script, which must be <c>/bin/sh</c>-compatible.</returns>
    protected abstract string BuildStartupScript();

    /// <summary>Gets the container ports this topology publishes to the host, one for one.</summary>
    /// <returns>The ports.</returns>
    protected abstract IReadOnlyList<int> PublishedPorts();

    /// <summary>
    /// Gets the folder under <c>Configs/</c> holding this topology's configuration files, or null
    /// when it has none.
    /// </summary>
    protected virtual string ConfigFolderName => null;

    /// <summary>Runs before the container is created, for work such as generating certificates.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the topology is ready to be created.</returns>
    protected virtual Task OnBeforeCreateAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>Adds any mounts beyond the configuration folder.</summary>
    /// <param name="spec">The specification being built.</param>
    protected virtual void AddMounts(ContainerSpec spec)
    {
    }

    /// <summary>Releases anything the topology holds beyond its container.</summary>
    protected virtual void OnDispose()
    {
    }

    /// <summary>Builds an endpoint on the harness host for one published port.</summary>
    /// <param name="port">The port.</param>
    /// <returns>The endpoint.</returns>
    protected RedisEndpoint Endpoint(int port) => new RedisEndpoint(Options.Host, port);

    /// <summary>Writes one line to the options' progress sink, when there is one.</summary>
    /// <param name="message">What is happening, without the topology name.</param>
    protected void Report(string message) =>
        Options.Progress?.Report("[" + Name + "] " + message);

    /// <summary>Runs one command inside this topology's container.</summary>
    /// <param name="command">The command and its arguments.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The command's result.</returns>
    protected Task<ExecResult> ExecAsync(IReadOnlyList<string> command,
        CancellationToken cancellationToken) =>
        Client.Containers.ExecAsync(ContainerName, command, cancellationToken: cancellationToken);

    /// <summary>Polls a condition until it holds, bounded by the options' readiness timeout.</summary>
    /// <param name="description">What is being waited for.</param>
    /// <param name="condition">The condition.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>How long the wait took.</returns>
    protected Task<TimeSpan> WaitAsync(string description,
        Func<CancellationToken, Task<bool>> condition, CancellationToken cancellationToken) =>
        ReadinessWaiter.WaitAsync(description, Options.ReadinessTimeout, condition,
            cancellationToken);

    /// <summary>
    /// Resolves the folder holding this topology's configuration files, next to the harness
    /// assembly.
    /// </summary>
    /// <returns>The absolute folder path.</returns>
    /// <exception cref="HarnessException">The folder did not travel to the output folder.</exception>
    protected string ResolveConfigFolder()
    {
        if (string.IsNullOrEmpty(ConfigFolderName))
        {
            throw new HarnessException("The " + Name + " topology declares no configuration folder.");
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Configs", ConfigFolderName);
        if (!Directory.Exists(path))
        {
            throw new HarnessException(
                "The configuration folder " + path + " is missing. The harness resolves its .conf"
                + " files through AppContext.BaseDirectory, so they have to be copied to the output"
                + " folder - check the <None Update=\"Configs/...\" CopyToOutputDirectory> items in"
                + " CodeBrix.Redis.TestHarness.csproj.");
        }

        return path;
    }

    /// <summary>
    /// Builds the <c>/bin/sh</c> fragment that copies the mounted configuration files into the
    /// container's writable data directory and changes into it.
    /// </summary>
    /// <returns>The fragment, ending in a newline.</returns>
    /// <remarks>
    /// The configuration mount is read-only, and Redis rewrites some of the files it is given - a
    /// sentinel rewrites its own configuration as it discovers the topology, and a cluster node
    /// writes its nodes file - so the server has to run against a writable copy.
    /// </remarks>
    protected static string CopyConfigsFragment() =>
        "set -e\nmkdir -p /data\ncp /conf/*.conf /data/\ncd /data\n";

    /// <summary>Builds the <c>/bin/sh</c> line that starts one server in the background.</summary>
    /// <param name="configFileName">The configuration file's name, without a folder.</param>
    /// <param name="sentinel">Whether to start it in sentinel mode.</param>
    /// <returns>The line, ending in a newline.</returns>
    protected static string StartServerFragment(string configFileName, bool sentinel = false) =>
        "redis-server /data/" + configFileName + (sentinel ? " --sentinel" : string.Empty) + " &\n";

    private ContainerSpec BuildSpec()
    {
        var spec = new ContainerSpec
        {
            Image = Image,
            Name = ContainerName,
            Entrypoint = ["/bin/sh", "-c"],
            Command = [BuildStartupScript()],
            NetworkName = NetworkName,
            RestartPolicy = RestartPolicy.No,
        };

        spec.Labels[RedisHarnessOptions.HarnessLabelName] = "true";
        spec.Labels[RedisHarnessOptions.TopologyLabelName] = Name;
        spec.NetworkAliases.Add(ContainerName);
        spec.NetworkAliases.Add(Name);

        foreach (var port in PublishedPorts())
        {
            spec.PortBindings.Add(new PortBinding(port, port));
        }

        if (!string.IsNullOrEmpty(ConfigFolderName))
        {
            spec.Mounts.Add(MountSpec.Bind(ResolveConfigFolder(), "/conf", readOnly: true));
        }

        AddMounts(spec);
        return spec;
    }

    private async Task RemoveQuietlyAsync()
    {
        try
        {
            await Client.Containers
                .RemoveAsync(ContainerName, force: true, removeVolumes: true, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (DockerException)
        {
            //Best effort; the failure being reported is the one that matters.
        }
    }

    private async Task<ContainerSummary> FindContainerAsync(CancellationToken cancellationToken)
    {
        var containers = await Client.Containers.ListAsync(all: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var container in containers)
        {
            foreach (var name in container.Names)
            {
                if (string.Equals(name.TrimStart('/'), ContainerName, StringComparison.Ordinal))
                {
                    return container;
                }
            }
        }

        return null;
    }

    private async Task EnsureImageAsync(string reference, CancellationToken cancellationToken)
    {
        var images = await Client.Images.ListAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        foreach (var image in images)
        {
            if (image.RepoTags is not null
                && image.RepoTags.Contains(reference, StringComparer.Ordinal))
            {
                return;
            }
        }

        Report("pulling " + reference);
        using var pullTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        pullTimeout.CancelAfter(Options.ImagePullTimeout);
        await Client.Images.PullAsync(reference, progress: null, pullTimeout.Token)
            .ConfigureAwait(false);
    }

    private async Task EnsureNetworkAsync(CancellationToken cancellationToken)
    {
        var networks = await Client.Networks.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var network in networks)
        {
            if (string.Equals(network.Name, NetworkName, StringComparison.Ordinal))
            {
                return;
            }
        }

        try
        {
            var labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RedisHarnessOptions.HarnessLabelName] = "true",
            };
            await Client.Networks.CreateAsync(NetworkName, "bridge", labels, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DockerApiException)
        {
            //Another topology starting in parallel won the race, which is the outcome asked for.
        }
    }

    /// <summary>
    /// Builds a startup script from a header fragment and one line per server.
    /// </summary>
    /// <param name="configFileNames">The configuration file names, in start order.</param>
    /// <returns>The script.</returns>
    /// <remarks>
    /// Every server is started in the background and the script then waits on all of them, so the
    /// container lives as long as any server does. Readiness probing, not the shell, is what decides
    /// whether the topology actually came up.
    /// </remarks>
    protected static string BuildSimpleStartupScript(params string[] configFileNames)
    {
        var script = new StringBuilder(CopyConfigsFragment());
        foreach (var configFileName in configFileNames)
        {
            script.Append(StartServerFragment(configFileName));
        }

        return script.Append("wait\n").ToString();
    }
}
