================================================================================
EXTRAS-README: CodeBrix.Redis
Content in this repository that is NOT part of the NuGet package
================================================================================

This repository produces exactly one NuGet package,
CodeBrix.Redis.MitLicenseForever, built from src/CodeBrix.Redis (with the two
analyzer projects packed inside it). Everything described below ships with the
repository and never with the package.

There are no sample applications here. The extras are test support: two
libraries that exist so the test suites can run against something real.


CodeBrix.Redis.TestServer
=========================
    tests/CodeBrix.Redis.TestServer/

An in-process Redis server, written against CodeBrix.Redis itself, that speaks
enough of the protocol for a large part of the suite to run with no external
process at all. Ported from the upstream StackExchange.Redis repository's
toys/StackExchange.Redis.Server project.

Upstream publishes this as its own NuGet package. Here it is test support only
and is never packed: the drop-in scope of CodeBrix.Redis.MitLicenseForever
covers StackExchange.Redis, RESPite and RedLock.net, and no fourth package. If
that ever changes it is a deliberate decision, not a packaging accident - the
project sets IsPackable false on purpose.

It is the fast path. A test that only needs a well-formed responder should use
this rather than starting a container.

Two things about it are not obvious and are easy to undo by accident:

  * It has no listener of its own. RespServer hands you RunClientAsync(pipe) and
    expects an IDuplexPipe; whoever uses it owns the socket. That is upstream's
    shape - upstream's own RespSocketServer was already commented out, written
    against a pipelines package this repository does not take - and the accept
    loop lives in tests/CodeBrix.Redis.Tests/InProcessTestServer.cs instead.
  * Its csproj references src/CodeBrix.Redis.Build as an Analyzer. That is not
    tidiness: RedisServer.cs declares HelloSubFieldsMetadata.TryParseCI as a
    static partial with no implementing part, and the ASCII-hash source
    generator writes the body. Remove the reference and the project stops
    compiling. Upstream gives every project that reference from a repo-wide
    Directory.Build.props; this repository has none, so each csproj says it.

Ported 2026-09-01: 8 of upstream's 10 files, 4,033 lines, building 0 warnings
and 0 errors in Debug and Release. The two that were not ported are recorded in
THIRD-PARTY-NOTICES.txt section 1, with the reasons.


CodeBrix.Redis.TestHarness
==========================
    tests/CodeBrix.Redis.TestHarness/

The topology harness: it stands real Redis servers up in containers through
CodeBrix.Docker, hands the tests their endpoints, and tears them down again.
Seven topologies come from here, on the port numbers the ported TestConfig names:

    Basic       a primary on 6379 and its replica on 6380
    Secure      a password-protected server on 6381
    Tls         a TLS-only server on 6384
    Failover    a second primary/replica pair on 6382 and 6383, for tests that
                rearrange replication and would otherwise wreck everything else
    Cluster     six nodes on 7000-7005, three primaries and three replicas
    Sentinel    a monitored pair on 7010/7011 watched from 26379, 26380, 26381
    Proxy       Envoy's Redis proxy on 7015, in front of the cluster

Each topology is ONE container running every server that topology needs, which is
how upstream's own docker-compose.yml arranges things. It matters: every server in
a topology shares that container's network namespace, so an address a server hands
out is 127.0.0.1 on a well-known port - and because the ports are published to the
host one for one, that address is reachable from the test process exactly as
given. RedisTopologies is the entry point; ContainerTier is the environment gate
(CODEBRIX_REDIS_RUN_CONTAINER_TESTS=1). See MAINTAINER-README.txt, TESTING.

This replaces the upstream repository's tests/RedisConfigs. The per-topology .conf
files WERE carried across, into Configs/, adapted for a container. The rest of
that folder was not: the checked-in Windows Redis binaries, the docker-compose
file, the .cmd and .sh start scripts, and the checked-in certificates - which
expired in 2023 and cannot be re-signed, because the authority's private key is
not in the upstream repository. The harness generates its own TLS material at run
time instead.

Its shape is repurposed from the RedisSetupTool sample in the CodeBrix.Docker
repository, which proved the same thirteen topologies live. The CODE, however,
is vendored into this repository. Nothing in this repository's build, test or
pack path may reference another repository on the filesystem - if the harness
needs something from RedisSetupTool, copy it in.

It contains no tests itself, so it carries no xUnit reference. It does not
reference src/CodeBrix.Redis either: it has to be usable while the library is
mid-port, so its readiness probes speak RESP over a socket directly.


WHAT IS NOT HERE
================
Several things that exist upstream were deliberately not ported. They are listed
so that their absence reads as a decision rather than an oversight:

  * The benchmark projects (StackExchange.Redis.Benchmarks, RESPite.Benchmark,
    OpBench) and the BenchmarkDotNet dependency they carry.
  * The other upstream toys: KestrelRedisServer, TestConsole, AotRig, and the
    various *Baseline projects used for comparing against released versions.
  * The upstream docs site (docs/) and its build.
  * The upstream build infrastructure: AppVeyor and GitHub Actions
    configuration, Nerdbank.GitVersioning, the shared ruleset, and the
    StyleCop.Analyzers and Microsoft.CodeAnalysis.PublicApiAnalyzers references.
    CodeBrix repositories do not carry GitHub workflow files.

The upstream PublicAPI.Shipped.txt files ARE worth keeping as a checklist for
verifying that the port's public surface matches the packages it replaces, but
they are reference material, not wired to an analyzer. See MAINTAINER-README.txt.

================================================================================
