================================================================================
MAINTAINER-README: CodeBrix.Redis
Notes for people and agents MAINTAINING this repository - not for package
consumers
================================================================================

If you are CONSUMING the NuGet package, stop reading and open AGENT-README.txt
instead. Everything below is about the repository itself: how it is laid out,
how it builds, how it is tested, how it is packaged, where its ported code came
from, and the conventions the source follows.


THE PORT, AND WHERE ITS RECORD IS
=================================
The port completed on 2026-09-01. Every line of source in this repository was
brought across from the three upstream projects named in PURPOSE AND SCOPE
below, at the exact tags recorded there and in THIRD-PARTY-NOTICES.txt. The
solution builds 0 warnings / 0 errors in Debug and Release, all three suites run
green (see TESTING), and no scaffold-only marker remains in the tree.

The plan document that drove the work is the historical record of HOW it was
done - the phase order, the namespace map, the file-by-file inventory, the
decisions taken and why, and the traps found while surveying the upstream
repositories:

    ~/ClaudeHome/PLAN_codebrix_redis_2026-09-01.md

It is not a to-do list any more, and nothing in this repository depends on it.
Read it before attempting a re-port against a newer upstream tag, or before
changing anything the phase summaries say was decided deliberately. What the
port produced, and every modification made to upstream code along the way, is
recorded permanently in THIRD-PARTY-NOTICES.txt - that file, not the plan, is
the one that must stay current.


PURPOSE AND SCOPE
=================
This repository produces exactly one NuGet package:

    PackageId:  CodeBrix.Redis.MitLicenseForever
    Assembly:   CodeBrix.Redis
    Namespaces: CodeBrix.Redis, CodeBrix.Redis.Respite, CodeBrix.Redis.RedLock
                (plus the sub-namespaces beneath each)
    Project:    src/CodeBrix.Redis/CodeBrix.Redis.csproj
    License:    MIT
    Consumer documentation: AGENT-README.txt (repo root)

It is a port of three MIT-licensed upstream libraries into that one assembly:

    StackExchange.Redis 3.1.31   ->  CodeBrix.Redis
    RESPite 3.1.31               ->  CodeBrix.Redis.Respite
    RedLock.net 2.3.2            ->  CodeBrix.Redis.RedLock

The goal is a drop-in replacement for all three. A consumer changes their using
directives to the CodeBrix.Redis.* namespaces and changes nothing else: type
names, member names, signatures, nullability annotations and behaviour are the
upstream ones. The upstream PublicAPI.Shipped.txt files - 2,859 lines for
StackExchange.Redis, 221 for RESPite - are the checklist that verifies this.


REPOSITORY LAYOUT
=================
    src/CodeBrix.Redis/              the library; the only packable project
      (root)                         the client surface: ConnectionMultiplexer,
                                     RedisDatabase, RedisValue, RedisKey,
                                     RedisResult, ConfigurationOptions, the
                                     result processors and the physical
                                     connection layer
      APITypes/                      public API value types
      Availability/                  multi-group database, health checks
      Configuration/                 options providers, Azure/AWS defaults
      Enums/                         the public enumerations
      Interfaces/                    IDatabase, IServer, ISubscriber, and the
                                     rest of the public interface set
      KeyspaceIsolation/             the key-prefixed IDatabase view
      Maintenance/                   server-maintenance event handling
      Profiling/                     profiling sessions and command traces
      Respite/                       the RESP protocol layer (from RESPite)
        Buffers/ Internal/ Messages/ Streams/ Transports/ Shared/
      RedLock/                       the Redlock implementation
        Configuration/ Events/ Internal/ Util/ Lua/
      PublicAPI/                     upstream public-API listings, kept as the
                                     fidelity checklist; reference only, not
                                     wired to an analyzer
      build/                         the props file packed into the package's
                                     build/ folder; see PACKAGING below
      InternalsVisibleTo.cs

    src/CodeBrix.Redis.Build/        Roslyn source generators and analyzers
    src/CodeBrix.Redis.CodeFixes/    the code fixes for those analyzers

    tests/CodeBrix.Redis.Tests/          the main suite
    tests/CodeBrix.Redis.Respite.Tests/  the protocol-layer suite
    tests/CodeBrix.Redis.Build.Tests/    generator and analyzer tests
    tests/CodeBrix.Redis.TestServer/     in-process Redis server (test support)
    tests/CodeBrix.Redis.TestHarness/    containerized topologies (test support)

The source layout inside src/CodeBrix.Redis MIRRORS THE UPSTREAM LAYOUT, file
for file. That is a deliberate departure from the usual family preference for
regrouping a heavy project root into sub-folders. For a port, upstream's tree IS
the map: it is what makes a file locatable when tracking an upstream fix, and
what makes a future re-port or diff against a newer upstream tag tractable. Do
not reorganize it.


THE THREE ANALYZER PROJECTS, AND WHY TWO OF THEM ARE NOT OPTIONAL
=================================================================
src/CodeBrix.Redis.Build is not a developer convenience. Two of the four things
in it are incremental source generators - the ASCII-hash generator and the
auto-database generator - and src/CodeBrix.Redis DOES NOT COMPILE without the
source they emit. If a build fails with a flood of "does not exist in the
current context" errors, check that this project built first.

The other two are consumer-facing analyzers: the transaction analyzer and the
queued-result analyzer. Together with the code fixes in
src/CodeBrix.Redis.CodeFixes, they are packed into the NuGet package under
analyzers/dotnet/cs by the _ResolveCodeBrixRedisAnalyzerForPackaging target in
CodeBrix.Redis.csproj, exactly as upstream ships them. That target deliberately
asks the projects for their output paths and errors if they are missing, rather
than assembling a path from bin/$(Configuration): packing without having built
them would otherwise be a silent omission, and the failure downstream - no
diagnostics, ever, for any consumer - is invisible.

The diagnostic IDs are the upstream SERxxxx identifiers and MUST NOT be renamed.
A consumer's NoWarn entries, .editorconfig severities and inline suppressions
are written against those IDs; renaming them silently breaks every one.

ONE rule was deliberately changed from upstream, and it is the only behavioural
change in either analyzer project: QueuedResultAnalyzer does not report SER308
("blocking on a task through the library's Wait helpers") when the compilation
being analysed is CodeBrix.Redis itself. Inside this library those helpers ARE
the synchronous surface - the IRedisAsync forwarders every decorator must
implement, and the sync-over-async in RedisBase and CursorEnumerable - and
upstream marks all 27 of those call sites intentional with a pragma, which this
repository does not carry. The exemption is one documented assembly-name check
(the ported AutoDatabaseGenerator gates itself the same way) and it covers
SER308 alone. A consumer's compilation has a different assembly name, so what a
consumer sees is byte-for-byte upstream's behaviour.

Both analyzer projects target netstandard2.0. That is the family-sanctioned
exception to net10-only: a Roslyn analyzer is loaded by the compiler or the IDE,
not by the consuming application, and those hosts load netstandard2.0
assemblies. The Microsoft.CodeAnalysis version they pin is the Roslyn HOST
FLOOR, because an analyzer cannot load in a compiler or IDE older than the
version it was compiled against. The port pinned 4.3.0; Jeremy raised both
projects to 5.9.0 - the compiler shipped in .NET SDK 10.0.400 - before the first
publish (1.0.245.159), and the package is verified to load cleanly on that SDK,
which is at least as new as every host the net10-only family targets. Raising
that floor again drops older consumer tooling; do it deliberately or not at
all. netstandard2.0 defaults to C# 7.3, so both projects set LangVersion
explicitly - that governs only what the SDK compiler accepts, not which host
can load the result.


BUILDING
========
    dotnet restore CodeBrix.Redis.slnx
    dotnet build   CodeBrix.Redis.slnx

Building the library produces the .nupkg automatically
(GeneratePackageOnBuild), at:

    src/CodeBrix.Redis/bin/<Configuration>/CodeBrix.Redis.MitLicenseForever.<version>.nupkg

The build must be clean: zero warnings, zero errors. There is no WarningLevel
override, no #pragma warning disable and no [SuppressMessage] attribute
anywhere in this repository, and none may be added. There is exactly ONE NoWarn
line, listing six SERxxxx [Experimental] gate identifiers - plus, on the test
side only, StringToRedisValue - see deviation 4 below; it is closed, and nothing
may ever be added to it.
GenerateDocumentationFile is on, so CS1591 fires for any undocumented public
member - fix it by writing the comment, never by suppressing the warning. That
applies to the RESPite-derived public types too: upstream builds RESPite with
documentation generation off, but this assembly ships one documentation file, so
those types get real summaries.


TESTING
=======
    dotnet test --solution CodeBrix.Redis.slnx

global.json sits beside the .slnx and selects the Microsoft.Testing.Platform
runner, which xunit.v3 4.x requires - without it the SDK reports that "testing
with VSTest target is no longer supported".

Note a known trap on the .NET 10.0.400 SDK: `dotnet test --solution` has been
observed reporting zero tests ran across several CodeBrix repositories even
though every test assembly runs correctly on its own. If that happens, run the
assemblies directly and quote those counts:

    dotnet tests/CodeBrix.Redis.Respite.Tests/bin/Release/net10.0/CodeBrix.Redis.Respite.Tests.dll
    dotnet tests/CodeBrix.Redis.Tests/bin/Release/net10.0/CodeBrix.Redis.Tests.dll
    dotnet tests/CodeBrix.Redis.Build.Tests/bin/Release/net10.0/CodeBrix.Redis.Build.Tests.dll

A `dotnet test` run leaves a TestResults/ folder at the repository root. The
family-canonical .gitignore does not cover it - it ignores bin/ and obj/ but not
that - so delete it after a run rather than committing it.

Three tiers of test, in increasing cost:

  * CodeBrix.Redis.Respite.Tests needs nothing. The protocol layer is testable
    against byte buffers.
  * Tests that need a well-formed responder use CodeBrix.Redis.TestServer, the
    in-process server. Still no external process.
  * Tests that need real server behaviour - cluster slot migration, sentinel
    failover, replica promotion, TLS, eviction policy - use
    CodeBrix.Redis.TestHarness, which starts containers through CodeBrix.Docker.
    These need a working Docker daemon and are the slow tier.

Server-backed tests are gated on an environment variable so that a contributor
without Docker still gets a green run from the first two tiers:

    CODEBRIX_REDIS_RUN_CONTAINER_TESTS=1


HOW TO RUN THE THREE TIERS, AND WHAT TO EXPECT
----------------------------------------------
Build Release first, then run the assemblies directly. Counts below are from
2026-09-01 on this machine; treat them as the baseline, not as a contract.

TIERS 1 AND 2 - no Docker. Leave CODEBRIX_REDIS_RUN_CONTAINER_TESTS unset:

    dotnet build CodeBrix.Redis.slnx -c Release
    dotnet tests/CodeBrix.Redis.Respite.Tests/bin/Release/net10.0/CodeBrix.Redis.Respite.Tests.dll
    dotnet tests/CodeBrix.Redis.Build.Tests/bin/Release/net10.0/CodeBrix.Redis.Build.Tests.dll
    dotnet tests/CodeBrix.Redis.Tests/bin/Release/net10.0/CodeBrix.Redis.Tests.dll

    Respite.Tests   1,717 total, 1,717 passed, 0 skipped     (~0.2s)
    Build.Tests       154 total,   154 passed, 0 skipped     (~3s)
    Tests           6,131 total, 3,533 passed, 0 failed,
                                 2,596 skipped, 2 not run    (~11s)

    2,470 of those skips are the container tier itself, reported with the tier's
    own sentence. That whole tier must skip WITHOUT opening a socket: if this run
    takes minutes rather than seconds, something is connecting before it checks
    the gate, and that is a defect, not slowness. The 2 "not run" are upstream's
    two [Fact(Explicit = true)] tests, which xUnit does not run unless asked.

TIER 3 - with Docker. Set the variable for the run:

    CODEBRIX_REDIS_RUN_CONTAINER_TESTS=1 \
      dotnet tests/CodeBrix.Redis.Tests/bin/Release/net10.0/CodeBrix.Redis.Tests.dll

    Tests           6,131 total, 6,010 passed, 0 failed,
                                   119 skipped, 2 not run    (~87s, including
                                                              container startup)

    The assembly fixture (tests/CodeBrix.Redis.Tests/Helpers/RedisTopologyFixture.cs)
    starts all seven topologies once for the run and stops them at the end. It
    ADOPTS containers a previous run left behind rather than recreating them, so
    if you change anything under tests/CodeBrix.Redis.TestHarness/Configs you must
    remove the containers before the next run or the old configuration is what you
    will test against. They all carry one prefix:

    docker ps -a --filter name=codebrix-redis-test-
    docker rm -f $(docker ps -aq --filter name=codebrix-redis-test-)

    RedisTopologies.RemoveAllAsync() is the in-process equivalent of that sweep,
    for a run that was killed part way. After a normal run "docker ps -a" shows
    no container with that prefix.

    The xunit.v3 runner switches worth knowing when chasing one test:
    -class <full.Name>, -method <full.Name.method>, -parallel none, -maxthreads N,
    -stoponfail, -xml <file>.

THE 119 TIER-3 SKIPS, AND WHY EACH IS EXPECTED
-----------------------------------------------
None of them is a failure in disguise, and only ONE was added by this port.

    55  "Skipping long-running test" - upstream's Config.RunLongRunning switch,
        false by default. Set RunLongRunning in RedisTestConfig.json to include
        them; they take minutes.
    12  Config.AzureCacheServer is not set - upstream's, needs a real Azure cache.
     8  "TODO: Hostile" - upstream skips this theory itself.
     6  "Flaky" - upstream's own word, on one stream-trim theory.
     6  "In-process server is in use" - the tier-2 server cannot answer these.
     4  "This needs some CI love..." - upstream's own.
     3  Config.RedisLabsSslServer is not set - upstream's; note the vendored
        redislabs_ca.pem expired in 2023 and is carried inert.
     2  "Debug only due to parallelism overhead" - Release run.
     2  Need HACK_TUNNEL_ENDPOINT - upstream's manual tunnel scenario.
     2  "FlushAllDatabases" / 1 "Unfriendly" / 2 "We don't need to test this..."
        - upstream's permanently skipped tests. Upstream spells them
        [Fact(Skip = "...")]; here they are a plain [Fact] whose first statement
        is Assert.Skip(same reason), because a compile-time Skip trips the
        xUnit1004 analyzer and this repository carries no suppressions.
     8  "Database 'NN' is not supported on this server" - ClusterTests.keys against
        the cluster, where Redis supports database 0 only. Inherent, not config.
     1  "this is timing sensitive; unable to verify this time" - upstream's own.
     3  RedLock's two SSL tests and its timing test - upstream marks all three
        [Ignore]; the harness's TLS authority is generated per run and is in no
        trust store, so an Ssl = true endpoint still cannot validate it.
     1  Config.SSDBServer, 1 "no active:active endpoints", 1 "need to think about
        CompletedSynchronously" - upstream's own.
     1  SanityChecks.value_tuple_not_referenced - THE ONE SKIP THIS PORT ADDED.
        Upstream asserts that its assembly does not reference System.ValueTuple,
        because StackExchange.Redis targets net461 where that is a separate
        package. This assembly is net10.0 only, where ValueTuple is in the box,
        and it merges RedLock.net in - whose own source returns
        (RedLockStatus, RedLockSummary) tuples. That is the single ValueTuple
        reference in the assembly and it does not come from the ported client
        core. The scan is left intact in the test so it can be re-enabled if the
        premise ever returns.

A tier-1/2 run skips 2,596 instead: the same list, plus the 2,470 container-tier
skips, and minus the tests those cover.

Two ClusterSlotsTests used to be on this list, skipping with "this deployment
reports no replicas". They are not any more, and the fix was in the harness
rather than in a test: a cluster answers cluster_state:ok before its replicas
have finished their initial sync, and a replica is left out of its primary's
CLUSTER SLOTS entry until it has. ClusterTopology now waits, after the
cluster_state:ok wait and bounded by
RedisHarnessOptions.ClusterReplicaVisibilityTimeout (30 seconds), for every
range in CLUSTER SLOTS to name at least one replica. It is deliberately a
COURTESY wait: exceeding it leaves the topology usable and simply puts those
two skips back, because every other cluster behaviour is already correct
without it and a slow machine must not turn a working cluster into a failed
harness.

DEBUG differs slightly, and is also green: 6,139 tests rather than 6,131 (eight
live inside "#if DEBUG"), 3,534 passed / 2,603 skipped with the tier off, and
6,016 passed / 121 skipped with it on. The Debug tier-3 list differs from the
Release one by four entries: two upstream DEBUG-only skips appear ("expected 2
ambient exceptions" and "only predictable in release builds"), the two "Debug
only due to parallelism overhead" ones stop applying, and one this port added
appears:
FailoverTests.subscriptions_survive_primary_switch_async asserts that a primary
switch produces at least 12 configuration-changed broadcast echoes, and against
redis:8-alpine it is consistently 10 - measured three times, solo, with the two
assertions the test is NAMED for (each subscription sees exactly its two messages)
passing every time. The missing pair is the echo upstream attributes to a PUBLISH
reaching a replica, which is a server-version property. The floor was NOT lowered:
falling short is reported as a skip that names the number seen, so a real
regression later still shows up rather than being absorbed.

Anything else, including the variable being unset, leaves the tier off. The
harness exposes that decision as CodeBrix.Redis.Testing.ContainerTier -
IsEnabled and DisabledReason - so a test skips with a sentence rather than
failing with a Docker error:

    if (!ContainerTier.IsEnabled) { Assert.Skip(ContainerTier.DisabledReason); }

The harness itself is one container per topology, started through CodeBrix.Docker
on the official redis:8-alpine image, with every server in a topology sharing that
container's network namespace and every port published to the host one for one.
That is upstream's own docker-compose arrangement, and it is what makes the ported
suite's expectations survive the move into containers: an address a server hands
out - a MOVED redirect, an INFO replication line, a SENTINEL masters row - is
127.0.0.1 on a well-known port, reachable from the test process exactly as given.
The port numbers are upstream's, because the ported TestConfig names them.
RedisTopologies.Create() builds the whole set; StartAllAsync() brings it up,
adopting any container a previous run left behind rather than recreating it;
RedisTopologies.RemoveAllAsync() is the sweep for a run that was killed part-way.
Two more variables override the images it starts, CODEBRIX_REDIS_TEST_IMAGE and
CODEBRIX_REDIS_TEST_PROXY_IMAGE. TLS material is generated at run time into a
folder under the system temporary directory - never vendored, because upstream's
checked-in certificate expired in 2023 and its authority key is not in the
repository, so nothing can re-sign it.


PACKAGING AND PUBLISHING
========================
The package version is date-stamped and computed at build time by the canonical
CodeBrix version block in CodeBrix.Redis.csproj: 1.<years since 2026>.<day of
year>.<minute of day>, all UTC. Do not replace it with a literal <Version>. Two
builds in the same UTC minute produce the same version, so do not publish two
packages within one minute.

Four content files are packed alongside the assembly: the icon, README.md,
AGENT-README.txt and THIRD-PARTY-NOTICES.txt. The two analyzer assemblies are
packed under analyzers/dotnet/cs, and
src/CodeBrix.Redis/build/CodeBrix.Redis.MitLicenseForever.props is packed under
build/. That props file is named for the PackageId, not the assembly, because
NuGet auto-imports build/<PackageId>.props and nothing else; it is what lets the
shipped analyzer see a consumer's <RedisMinServerVersion>. Rename the package
and that file has to be renamed with it. Verify a built package with:

    unzip -l src/CodeBrix.Redis/bin/Release/CodeBrix.Redis.MitLicenseForever.*.nupkg

Before publishing, confirm THIRD-PARTY-NOTICES.txt still matches reality. It is
not boilerplate here: this is a port, the upstream MIT licenses require their
copyright notices to travel with the source, and that file is where they travel.


DEPENDENCIES, AND THE RULE BEHIND THEM
======================================
The library takes exactly two package references, both published by Microsoft:

    Microsoft.Extensions.Logging.Abstractions
    System.IO.Hashing

The first is NOT merely internal logging plumbing - ILoggerFactory appears in
the public surface (ConfigurationOptions.LoggerFactory and
DefaultOptionsProvider.LoggerFactory), so a consumer's own ILoggerFactory has to
be the same type. Vendoring the abstraction would break drop-in compatibility.
The second supplies the hashing used for cluster hash slots.

Everything else the upstream projects reference existed only to serve target
frameworks below net10.0 and is in the box on .NET 10: System.IO.Pipelines,
Microsoft.Bcl.AsyncInterfaces, System.Threading.Channels, System.IO.Compression,
System.Buffers, System.Memory, and System.Runtime.InteropServices.
RuntimeInformation. Do not reinstate any of them.

The rule for anything new: prefer an in-box API, then a CodeBrix.* package, then
a Microsoft-published package. A non-Microsoft, non-CodeBrix package reference
does not belong in the library at all. In the test projects the allowance is
xUnit, SilverAssertions, CodeBrix.TestMocks and CodeBrix.Docker, plus
Microsoft-published packages. That is why the upstream suites' NSubstitute usage
becomes CodeBrix.TestMocks and their Newtonsoft.Json usage becomes
System.Text.Json. coverlet.collector is not used in this family any more.

Nothing in this repository's build, test or pack path may reference another
repository on the filesystem. The upstream checkouts under ~/GitHome are
read-only reference material for porting, never a build input. If a test needs a
fixture that exists upstream - a certificate, a Lua script, a captured RESP
frame - vendor a copy into this repository and resolve it through
AppContext.BaseDirectory.


CODING CONVENTIONS
==================
Family conventions apply, with the deviations noted in the next section:

  * net10.0 only. No multi-targeting, ever.
  * File-scoped namespaces (namespace X;), never block-scoped.
  * No leading blank line at the top of a file. Usings in one contiguous block
    at the top, System.* first, alphabetical within each group, never below the
    namespace line.
  * No global using and no #nullable directives in source.
  * Every ported file records its origin on the namespace line:
        namespace CodeBrix.Redis.Respite.Buffers; //was previously: RESPite.Buffers;
    Files that are new in this repository do not carry that comment.
  * Preserve upstream file headers verbatim where they exist. Never invent one.
  * Test files that cover one class are named <ClassUnderTest>Tests.cs and hold
    public class <ClassUnderTest>Tests. Test methods are <MemberName>_snake_case
    or plain snake_case. Multi-statement test bodies carry //Arrange, //Act and
    //Assert comments; single-statement bodies are expression-bodied. Prefer
    SilverAssertions' fluent form (x.Should().Be(y)) over Assert.Equal. Thread
    TestContext.Current.CancellationToken through every cancellable call in a
    test - xUnit1051 fires otherwise.

Ported test source arrives written in the upstream's style. Convert it to the
conventions above as it lands, in the same pass; do not leave the drift behind
for a later sweep.


THE DEVIATIONS FROM FAMILY CONVENTION, AND WHY
==============================================
Four, all deliberate, all approved by Jeremy on 2026-09-01. If you are auditing
this repository against the family rules, these are the expected findings:

1. Nullable reference types are ENABLED on src/CodeBrix.Redis and on the test
   projects, where the family default is off. The upstream is annotated
   throughout and - the point - its PUBLIC signatures are annotated. Stripping
   the annotations across roughly 83,000 lines would change what a consumer
   migrating off StackExchange.Redis sees at compile time, which is precisely
   what this package exists to preserve. Same grounds as the standing exception
   in CodeBrix.Platform.OpenGL. This does not generalize: a CodeBrix library
   that is not a port of NRT-annotated upstream code still has NRT off.

2. The source tree is not reorganized into CodeBrix-style sub-folders; it
   mirrors upstream. See REPOSITORY LAYOUT above.

3. Microsoft.Extensions.Logging.Abstractions is a Microsoft.* package rather
   than a System.* one. See DEPENDENCIES above for why it cannot be avoided.

4. There is ONE <NoWarn> line, carrying six identifiers and nothing else, in
   src/CodeBrix.Redis, src/CodeBrix.Redis.Build and the test projects that need
   it:

       <NoWarn>$(NoWarn);SER001;SER004;SER005;SER007;SER008;SER009</NoWarn>

   On the TEST SIDE ONLY - and never in src/ - that line carries a seventh
   identifier, StringToRedisValue:

       <NoWarn>$(NoWarn);SER001;SER004;SER005;SER007;SER008;SER009;StringToRedisValue</NoWarn>

   It is the same kind of thing and it is there for the same reason. The library
   marks its implicit string -> RedisValue conversion [Experimental] under
   #if DEBUG - a deliberate trap upstream set for itself, to find places that
   lean on that conversion - so a DEBUG build of any code that uses it fails.
   Test code uses it constantly: without the opt-in, a Debug build of
   tests/CodeBrix.Redis.TestServer alone fails at 35 sites in 5 files, while
   Release is unaffected either way. Upstream opts its whole test side in the
   same way, in tests/Directory.Build.targets and its test-server csproj.
   Approved by Jeremy on 2026-09-01 as plan section 12 item 6.

   WHAT IT IS NOT: it is not a suppressed defect, and it silences no analyzer
   rule. Those six identifiers are not defect rules at all - they are the
   diagnostic IDs of the [Experimental] API gates that this assembly's own
   source declares, in Respite/Shared/Experiments.cs.

   WHY IT IS NEEDED: C# raises an [Experimental] diagnostic as a compile ERROR,
   and the language offers exactly two sanctioned opt-ins - NoWarn, or marking
   the consuming code [Experimental] too. The second cascades: 97 client-core
   files use the gated types, internal transport types are referenced by public
   ones, and marking a public type would change the drop-in surface this package
   exists to preserve. Upstream carries the identical NoWarn line in its own
   Directory.Build.props for the same reason.

   WHAT IT DOES NOT CHANGE: every upstream [Experimental] attribute is present
   and unaltered, so a CONSUMER of this package sees exactly the gates
   upstream's consumers see, on the same identifiers.

   THE LINE IS CLOSED, in both its forms. Nothing may ever be added to either.
   A new warning is fixed at source, as every other one in this repository was. (For the record: the
   SERDBG gate is deliberately NOT on it - the one test that needs SERDBG opts
   in with an attribute on that single test method.)

Note what is NOT on this list, and one thing that could be mistaken for it.
Nine members of tests/CodeBrix.Redis.Tests carry an [Obsolete] attribute of their
own - five in PubSubKeyNotificationTests, two in ChannelTests, three in ParseTests
(one helper and its two callers) and one in SSLTests. That is NOT a suppression:
each of them exists to exercise an API this library or the BCL marks [Obsolete] -
KeyNotification.IsKeySpace/.IsKeyEvent, the implicit string/byte[] -> RedisChannel
conversion, LoggingTunnel - and C# does not report CS0618 inside a member that is
itself obsolete. That is the language's own opt-in for the case, it is scoped to
the one member, and each site says in a comment what it is exercising and why.
Where an obsolete value appears in an ATTRIBUTE ARGUMENT instead - SslProtocols.Ssl2
and .Ssl3 in four InlineData rows - marking the method does not cover it, so those
two values are named by their numeric constants beside a comment saying which is
which.

Apart from that one NoWarn line there is no warning
suppression anywhere - no #pragma, no [SuppressMessage], no ruleset -
GenerateDocumentationFile stays on and CS1591 is fixed at source, unlike the
CodeBrix.AssemblyTools and CodeBrix.Platform.OpenGL exceptions. The public
surface here is a few thousand members, not tens of thousands, and upstream
already documents most of it.


PROVENANCE
==========
Everything derived from an upstream project is recorded in
THIRD-PARTY-NOTICES.txt: the source repository, the exact tag and commit ported
from, the copyright holders, the full license text, the file mapping, and every
modification made during the port. Keep it current if anything derived from an
upstream project ever changes - it is the legal record, and MIT requires those
notices to travel with the source.

The public surface was diffed against the upstream PublicAPI listings when the
client core landed, with the namespace prefixes swapped and the SE.Redis and
RESPite listings merged (they share one assembly here). Nothing upstream declares is
missing: zero "declared but not found". Ten members are present here and absent
from those listings, and all ten are LoggingTunnel's nested transport and its
DuplexTransport-typed members - which upstream also has, and which are absent
from its listings only because upstream suppresses that analyzer rule on the
whole LoggingTunnel type. Re-run that check with a THROWAWAY project outside
this repository: never add Microsoft.CodeAnalysis.PublicApiAnalyzers here.

The upstream checkouts used as reference:

    ~/GitHome/StackExchange.Redis   tag 3.1.31, commit 90bbf1fc
    ~/GitHome/RedLock.net           tag release-2.3.2, commit dcfe373

Read them freely. Never modify them, and never let this repository's build,
tests or packaging reference them.


THE AI-AGENT POINTER STUBS
==========================
Eight files - AGENTS.md, CLAUDE.md, .clinerules, .cursorrules,
.cursor/rules/agent-readme.mdc, .windsurfrules,
.github/copilot-instructions.md and .junie/guidelines.md - are byte-identical
across every repository in the family and point at README-INDEX.txt. They carry
no repository-specific content. Do not edit them here; if something needs
saying, it belongs in AGENT-README.txt or in this file.

================================================================================
END OF MAINTAINER-README
================================================================================
