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

- **Summed test time**: aggregate `UnitTestResult@duration` from the TRX.
- **Wall clock**: time the `dotnet test` process (includes test-host startup and
  discovery, roughly 4–5 s on the reference host).
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

## 7. Stage 2 measurement plan

1. Re-run the §2 commands three times and record the median before touching
   anything.
2. Split the next critical-path classes (`PlayerInteractionServiceTests`,
   `EntityEventSimulationTests`, `DirectionTests`, `Replays.ReplayTests`) and
   re-measure after each batch.
3. Attack summed work, which is now the binding constraint on this host: the
   largest namespaces are Session and World, and every `TestNode.Create` builds
   the full production DI graph (≈500 node constructions per full run). Profile
   the per-node setup cost before and after any change; do not trade isolation
   for speed.
4. Record the result here with the same three-run median method.
