# Test suite parallelization — measurement and parallel-safety record

Scope: how `CasualtiesUnknownOnline.Tests` (xUnit v2.9.3, net48) parallelizes, the
measured baseline, the process-global hazards parallel execution exposes, and the
reproducible way to re-measure. Ticket:
`docs/backlog/in-progress/test-suite-parallelization.md`.

## 1. The parallel model

xUnit v2 semantics (verified against the runner, not assumed):

- one test **collection per test class** by default;
- different collections run **in parallel**; the thread budget defaults to the
  logical processor count;
- every test **inside one class runs strictly serially** — a 135-case class is a
  135-step serial chain no matter how many cores exist;
- the `conservative` scheduler is the 2.8+ default.

The contract is now explicit in
`tests/CasualtiesUnknownOnline.Tests/xunit.runner.json` and copied beside the test
assembly by the csproj (`parallelizeTestCollections`, `maxParallelThreads: "1x"`,
`parallelAlgorithm: conservative`, `longRunningTestSeconds`). That the runner
really reads the file was proven by flipping `maxParallelThreads` to `1` and
observing `Starting: ... (parallel mode = collections [1 thread], ...)`.

## 2. How to measure (reproducible)

```bash
dotnet build CasualtiesUnknownOnline.slnx -c Debug
dotnet test tests/CasualtiesUnknownOnline.Tests/CasualtiesUnknownOnline.Tests.csproj \
  -c Debug --no-build --logger "trx;LogFileName=<label>.trx" \
  --results-directory TestResults/<label>
```

- **Summed test time**: aggregate `UnitTestResult@duration` from the TRX. It is
  a *wall-clock* metric, so it inflates with concurrency: the same tree reports
  169 s at 10 parallel threads and 331 s at 20 (see §7.5). Never compare summed
  time across different `maxParallelThreads` settings; use it to rank classes
  within one run or one setting.
- **Wall clock**: time the `dotnet test` process (includes test-host startup and
  discovery, roughly 4–5 s on the reference host). Absolute wall clock drifts by
  more than ±10 % between batches on this host (background load); a change must
  be judged by a paired, interleaved A/B measured inside one host window (§7.3).
- **vstest duration**: the runner's own "duration" line (test execution only).
- Reference host for the numbers below: 14 physical / 20 logical processors,
  Debug configuration, `--no-build`, nothing else running.
- **Per-test durations are noisy**: identical runs varied by ±20 % per class on
  this host. Compare medians of three runs; never trust a single run.

## 3. Baseline (before Stage 1)

| Metric | Value |
|---|---|
| Test cases | 2593 |
| Summed test time | ~326 s |
| vstest duration | ~30 s |
| Wall clock (`dotnet test`) | ~35 s |
| Sum / vstest duration speedup | ~11x |
| Sum / process wall speedup | ~10x |

Work distribution by test namespace (one run, summed test time):

| Namespace | Tests | Seconds |
|---|---:|---:|
| Session | 994 | ~87 |
| World | 381 | ~60 |
| Mods | 314 | ~36 |
| Items | 169 | ~19 |
| Tooling / Replays / Patching / Networking | 389 | ~23 |
| GameState + Protocol + rest | 346 | ~6 |

The largest single classes were `EntityEventBehaviorTests` (135 cases in one
class, ~27 s serial), `PlayerInteractionServiceTests` (92 cases), and
`EntityEventSimulationTests`.

## 4. Findings

1. **The suite already ran in parallel.** The ~11x speedup over the summed test
   time (relative to the runner's own test-execution duration) proves class-level
   parallel collections were active; no switch was missing.
2. **The host is throughput-bound, not tail-bound.** Summed work / effective
   cores ≈ wall clock on this 14-core host, so the longest class (27 s) was not
   the binding constraint *here*. It becomes the binding constraint on a host
   with more cores (schedule model: at 20 workers the class alone bounds the run
   at ~27 s while the total work would allow ~16 s).
3. **Two real parallel hazards existed**, both surviving only because the races
   are rare: a shared per-node log file, and game-assembly static tables mutated
   by three classes xUnit is free to run side by side.
4. **The per-node rolling file sink was pure test overhead.** It wrote a
   `latest.log` per node (plus a `.gz` archive of the previous run's log on every
   open) with `AutoFlush` per line, and nothing in the suite ever read it.

## 5. Parallel-safety inventory and fixes (Stage 1)

| Hazard | Evidence | Fix |
|---|---|---|
| Game-assembly static table replaced by three classes that xUnit may run concurrently | `Patching/ItemDropSourceProviderTests.cs:54,61` (`Item.GlobalItems`, `ItemLootPool.pool`), `Patching/ItemAdvancedBehaviorProviderTests.cs:54`, `Patching/GameAdapterItemInjectionContractTests.cs:43` | all three join the `GameAssembly` collection (`Patching/GameAssemblyCollection.cs`) |
| Unity static state (`PlayerCamera.main`, `RemoteBackpackView._focusedBody`) written by a test | `Patching/RemoteBackpackViewCloseTests.cs:38,40,52` | same collection |
| Reader outside the collection observing a half-applied static write | collection membership alone only serializes the writers | the `GameAssembly` collection declares `DisableParallelization = true`: it runs with no other collection in flight (total work a fraction of a second) |
| New static write added without isolation | — | normative gate `TestIsolationGateTests.StaticGameStateMutations_JoinTheGameAssemblyCollection`: Roslyn-parses every test source, finds each test class with a static `SetValue(null, ...)` and requires the `[Collection]` attribute; the gate's own negative/positive contract is asserted by `StaticGameStateMutationDetection_FlagsUnisolatedTestClasses` |
| Per-node log file shared by nodes with the same Steam id | `TestNode` used `cuo-tests/node-{steamId}`; `RollingFileLoggerProvider` opens with an exclusive write handle and rotates the previous `latest.log` | tests no longer create the file sink (`Fakes/TestLogging.cs`, wired in `TestNode.Create` and `IpDirectSessionIntegrationTests.CreateProvider`); the sink keeps direct coverage in `LoggingOptionsTests` |
| Fixed IP-direct log directories | `Networking/IpDirectSessionIntegrationTests.cs` used `cuo-ipdirect-tests/{host,guest}` | same sink removal plus a per-instance directory for the failure-log path |
| Implicit runner configuration | no `xunit.runner.json` existed | explicit runner contract + output copy |

Residual, review-enforced blind spots (no source gate can see them without
semantic references to the game assemblies): a direct static assignment through
the GameRef alias, applying a Harmony patch, and calling production code that
mutates a process-global static.

## 6. Stage 1 measured effect

A/B with only the file sink toggled (three runs each, back to back):

| Configuration | Wall (s) | vstest duration (s) | Summed test time (s) |
|---|---|---|---|
| Sink kept | 36.4 / 36.6 / 37.2 | 31 / 32 / 31 | 298 / 306 / 317 |
| Sink removed | 33.4 / 33.5 / 33.6 | 29 / 29 / 29 | 239 / 249 / 251 |

Removing the sink cut the summed test work by ~19 % and the wall clock by ~3 s
(~9 %). The gap between the two ratios is expected: the file I/O overlapped CPU
work, so only the non-overlapped part of the saving reaches the wall clock.

Critical-path split (`EntityEventBehaviorTests` → five behavior-family classes,
same 135 cases), class-level schedule derived from TRX durations:

| | Longest class | LPT makespan @ 16 workers | @ 20 workers |
|---|---|---|---|
| Before | 26.7 s | 26.7 s | 26.7 s |
| After | 12.0 s | 18.9 s | 15.1 s |

On this throughput-bound host the split is wall-clock neutral; its value is
removing the serial tail that binds on higher-core hosts and capping the tail as
the suite grows. `EntityEventBehaviorTests` was replaced by
`EntityEventTriggerRelayTests`, `EntityEventDuplicateGuardTests`,
`EntityEventRaceTests`, `EntityEventSnapshotExtraTests` and
`EntityEventResetTests`, all sharing `EntityEventBehaviorData`.

Final Stage 1 verification on the delivered tree: three consecutive
`dotnet test CasualtiesUnknownOnline.slnx` runs, all green — 20 normative-gate
tests + 2593 main-suite tests, wall clock 38.1 / 40.5 / 41.1 s in that batch.
Absolute wall clock drifts with host thermal/load state (the same tree measured
34.6–35.2 s in an earlier batch), so only compare medians measured within one
batch. The isolation gate was red-checked by removing the `[Collection]`
attribute from `RemoteBackpackViewCloseTests`: the gate failed with the exact
class/file name and passed again after the attribute was restored; the gate's
detection logic now carries its own negative/positive unit test
(`StaticGameStateMutationDetection_FlagsUnisolatedTestClasses`).

The `DisableParallelization = true` guarantee was verified from the TRX timeline
of a full run rather than assumed: the 10 `GameAssembly` tests occupy a single
68 ms window with zero tests from any other collection overlapping it, so the
reader-during-write race is closed and the serialization cost is negligible.

## 7. Stage 2 measured effect

### 7.1 The per-node setup cost is not the DI graph

The ticket assumed the cost was `TestNode.Create` building the full production
DI graph (estimated at ≈500 node constructions per run). Profiling (temporary
`Perf` tests + a temporary node counter, both removed before the final tree;
medians of 10–15 iterations after warm-up, Debug, reference host) corrects that:

| Phase | Cost | Notes |
|---|---|---|
| `CuoBootstrap.BuildServiceProvider` (full graph, `ValidateOnBuild`) | 0.41 ms | the DI graph build is *not* the cost |
| `TestNode.Create` (no first frame) | 1.1–1.5 ms | includes the container + lifecycle start |
| First `Update()` frame | 3.4–6.3 ms | first-frame mod discovery + load of 21 mods + every service update |
| `ModRegistry.Discover` alone | 2.91 ms | scans 20 assemblies / 11 719 types; the raw type scan is 0.69 ms |
| `TestNode.CreatePair` (two pumped nodes + handshake) | 16.8–18.9 ms | |
| `EntityEventSimWorld.Create()` (three nodes + handshake ticks) | 34.8 ms | |
| `ItemSimWorld.Create()` | 33.5 ms | |

Node constructions per full run (counter, exact): **2 260** for the Stage 1 tree
(2 419 measured with the temporary profiling harness in the tree, which itself
constructs 159 nodes) and **2 114** after Stage 2 — a reduction of 146, which is
exactly `DirectionTests`' 152 → 6.

Deliberately retained costs, with reasons:

- **Per-node first-frame mod discovery + load (~4.3 ms/node, ≈9 s of the run).**
  Each node must own its mod instances (a shared discovery result would share
  mutable mod state across tests, which is forbidden). The scan is the
  production first-frame behaviour under test; caching it would need a
  production seam, and the ticket's non-goals exclude production changes unless
  unavoidable.
- **Fresh per-test sessions/worlds.** `PlayerInteractionServiceTests` (now 13
  classes) and the entity-event families mutate the session/world under test;
  sharing them would break isolation, so every test still builds its own nodes.

### 7.2 Splits

All splits are behaviour-preserving: the same test methods, assertions and
helpers; only the class boundaries changed. Shared helpers are stateless static
classes; no `partial` files and no duplicated MemberData rows.

| Former class | Cases | Now | Cases per class |
|---|---:|---|---|
| `PlayerInteractionServiceTests` | 92 | 13 behaviour-family classes + `PlayerInteractionTestSession` | 3–13 |
| `DirectionTests` | 77 | 3 direction classes + `DirectionClassificationTests` + `DirectionProbe` fixture | 18–30 (+1 fact) |
| `Replays.ReplayTests` | 23 | 4 domain classes + `ReplayValidationTests` + `ReplayHarness` | 2–10 |
| `EntityEventSimulationTests` | 23 | 4 scenario classes | 5–7 |
| `EntityEventTriggerRelayTests` | 33 | 3 kind-shard classes (crystal / trap / machine) | 9–12 |
| `EntityEventRaceTests` | 33 | 3 kind-shard classes | 9–12 |
| `EntityEventDuplicateGuardTests` | 33 | 3 kind-shard classes | 9–12 |
| `EntityEventSnapshotExtraTests` | 18 | 3 kind-shard classes (one-shot subset) | 3–9 |
| `EntityEventResetTests` | 18 | 3 kind-shard classes (one-shot subset) | 3–9 |
| `MedicalOperationShrapnelSessionTests` | 21 | 3 family classes + `ShrapnelSessionFixture` | 5–9 |
| `CommandConsoleServiceTests` | 27 | 4 family classes + `CommandConsoleTestSession` | 4–9 |
| `CompareItemTraceScriptTests` | 9 | 3 family classes + `CompareItemTraceHarness` | 2–4 |

The kind shards are a PARTITION of `EntityEventArchives` (asserted by
`EntityEventArchivesTests.BehaviorFamilies_PartitionTheArchive`), so a kind runs
in exactly one shard — no row is duplicated and none is dropped. The replay
domain split adds `ReplayValidationTests.NoReplayFile_MixesExclusiveDomains`,
which keeps the folder-level "one file, one world" contract as an explicit test
instead of an implicit `SelectDomain` throw; the domain classes classify each
file at discovery time, so a malformed or mixed file now fails during discovery
with its file:line instead of as one red Theory row (the same fail-loud
contract, earlier). Test count: **2 593 → 2 595** (+2 guard facts); every
pre-existing test case and assertion is unchanged.

`DirectionTests` was the one summed-work win: each of its 76 Theory rows built a
full host+guest pair only to call the pure `PacketReceiver.IsValidDirection`
(static registry + session role). The three direction classes now share one
`DirectionProbe` class fixture each. The probe exposes only
`HostAccepts`/`GuestAccepts`; the nodes stay private, so the shared object is a
read-only query facade, not a mutable fixture, and no test can reach the live
session. 152 → 6 node constructions for the class.

### 7.3 Wall clock — paired A/B on the same host window

Absolute wall clock on this host drifts by more than ±10 % between batches
(background browser/video/IM load; CPU load was 36–48 % during Stage 2). The
only trustworthy comparison is a paired, interleaved A/B. Same host window,
`dotnet vstest` on two build outputs of the same tree (`--no-build`), 1x runner
config:

| Pair | Stage 1 tree | Stage 2 tree |
|---|---:|---:|
| 1 | 38.7 s | 35.7 s |
| 2 | 41.4 s | 36.3 s |
| 3 | 40.1 s | 36.4 s |
| **Median** | **40.1 s** | **36.3 s** |

Stage 2 is 3.8 s (≈9.5 %) faster in every pair. One earlier batch (three pairs,
measured during a background-load spike) showed the opposite direction for the
Stage 2 tree only (45–47 s vs 40 s); a repeat in a quieter window and the four
interleaved thread-sweep pairs below all favour Stage 2, so that batch is
attributed to host state, not to the tree. It is recorded here because the
Stage 2 tree's higher collection count makes it more sensitive to a
contended host — see 7.5.

### 7.4 Per-class durations (three-run medians, 1x)

The per-class metric is the sum of its tests' reported wall durations, so it is
inflated by contention exactly like the suite total. Final batch (three runs,
`dotnet test`, 1x), classes above ~4 s:

| Class | Cases | Median | Min run | Max run |
|---|---:|---:|---:|---:|
| `EntityEventTriggerRelayCrystalTests` | 9 | 7.24 s | 1.83 s | 7.29 s |
| `ShrapnelOperationApplicationTests` | 7 | 6.90 s | 2.33 s | 7.26 s |
| `EnemySyncServiceTests` | 15 | 6.23 s | 2.61 s | 6.37 s |
| `ModHandshakeTests` | 24 | 5.21 s | 3.77 s | 5.74 s |
| `ItemSnapshotSimulationTests` | 9 | 5.11 s | 3.37 s | 6.12 s |
| `EntityEventSimulationTransientTests` | 6 | 4.93 s | 2.24 s | 6.59 s |
| `ItemRaceTests` | 10 | 4.87 s | 3.65 s | 8.60 s |
| `EntityEventTriggerRelayTrapTests` | 12 | 4.85 s | 4.19 s | 8.74 s |

Read together with the earlier batches (8 stage-2 runs in total, e.g.
`EntityEventTriggerRelayTrapTests` min 3.58 / median 4.00 / max 8.74), the shape
is consistent: on a quiet host every class lands at 1.8–4.2 s, and contention
spikes add 2–4 s to whichever class catches them. The dominant classes are
gone — the longest class fell from 26.7 s (pre-Stage 1) / 12.0 s (Stage 1) to a
clean-window maximum of ~4.2 s, and no class is structurally above ~5 s any
more. The absolute 5 s line is only missed in runs that coincide with a
background-load spike; that is a host property, not a class-size property.

### 7.5 Thread count — Stage 3 input (measured, not decided here)

Interleaved single-run sweep on the Stage 2 tree (`dotnet vstest`, same window;
the runner config in the repository was restored afterwards):

| `maxParallelThreads` | Stage 2 wall | Stage 2 summed test time | Stage 1 tree wall |
|---:|---:|---:|---:|
| 20 (`1x`) | 35.7 s | 273.2 s | 38.7 s |
| 14 | 34.5 s | 221.6 s | 36.8 s |
| 12 | 34.3 s | 184.9 s | 37.1 s |
| 10 | 33.9 s | 153.3 s | 37.4 s |

Two findings for Stage 3: (1) the summed test time is a concurrency-inflated
metric — the same tree reports 169 s at 10 threads and 331 s at 20, so it must
never be compared across different thread settings; (2) this host has 14
physical / 20 logical processors and ran with 36–48 % background load, so
`1x` (20 threads) oversubscribes it and a lower cap is slightly faster and much
less contention-sensitive. The cap stays `1x` in Stage 2 because the setting is
Stage 3's deliverable; Stage 3 should benchmark it (including whether a
fractional multiplier such as `0.5x` is accepted by this runner) and pick the
faster/stable value.

## 8. Stage 3 plan

1. Classify slow/integration tests with `[Trait("Category", ...)]` and document
   the inner-loop filters.
2. Add the anti-rot guard for the long pole (a gate that fails when a test class
   grows past the agreed case/row limit) or record why not.
3. Benchmark `maxParallelThreads` (the ticket's 1x vs 2x plus the intermediate
   values measured in 7.5) and keep the faster/stable setting; record the
   concurrency-inflation caveat from 7.5 in the measurement method.
4. Final doc pass + final measured wall clock.
