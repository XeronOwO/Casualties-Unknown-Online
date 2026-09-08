# Test suite parallelization and runtime efficiency

- Status: In progress
- Priority: Medium
- Category: Test infrastructure / build performance
- Source: User request (2026-09-08) — the xUnit v2 suite is ~2500 test cases and a full run is measured in tens of seconds; because a normal work session runs the suite many times, the cumulative cost is material and the suite must stay fast as it grows. Question: can the current test architecture run in parallel, and what has to change to make it faster and stay safe?
- Related: `docs/evidence/test-parallelization.md`, `docs/evidence/verification.md`, `docs/evidence/normative-gates.md`

## Goal

Make the test suite's parallelism **explicit, safe and effective**:

1. **Explicit** — the parallel model is declared in `xunit.runner.json`, not inherited from framework defaults.
2. **Safe** — no test mutates process-global state (game-assembly/Unity statics, fixed temp paths) in a way another parallel collection can observe.
3. **Effective** — the wall clock is bounded by *total work / cores*, not by one oversized test class. xUnit v2 runs every test of one class strictly serially, so a 100+ case class becomes the critical path even on a many-core machine.

## Current state (measured, not assumed)

Baseline recorded in `docs/evidence/test-parallelization.md`:

| Metric | Value |
|---|---|
| Test project | `CasualtiesUnknownOnline.Tests` (xUnit v2.9.3, net48) |
| Test cases | 2593 |
| Summed test time | ~326 s |
| vstest duration | ~30 s |
| Wall clock (`dotnet test`) | ~35 s |
| Longest single class | `EntityEventBehaviorTests` — 135 cases in one class, ~27 s serial |
| Longest single test | ~3 s |

Conclusions (method and full numbers in `docs/evidence/test-parallelization.md`):

- The suite **already ran test classes in parallel** (xUnit v2 default: one collection per class, `maxParallelThreads` = logical CPU count, conservative scheduler). The ~11x speedup over the summed test time proves it.
- On the reference host the run is **throughput-bound** (summed work / effective cores), so the longest class is only the binding constraint on hosts with more cores — where it caps the whole run at ~27 s. The class-level tail is still worth removing: it is a hard serial ceiling that grows with every case added to a big class.
- The test composition was writing a **rolling log file per node** with `AutoFlush` per line: ~19 % of the summed test work, a shared-file race between parallel nodes, and one temp directory per node. Nothing in the suite read those files.
- Two latent parallel hazards existed: the shared per-node log file, and game-assembly static tables mutated by three classes xUnit is free to run side by side.

## Scope

- **A. Runner contract** — `xunit.runner.json` + output copy; document the model.
- **B. Parallel safety** — non-parallel `GameAssembly` collection for game-assembly static state, no shared process-global test artifact (no per-node log file), gate-enforced.
- **C. Long-pole removal** — split the largest test classes into genuinely separate classes (partial files do not help: xUnit still sees one class).
- **D. Fast feedback** — category traits + documented filters for the inner loop.
- **E. Measurement + guard + docs** — reproducible measurement method, anti-rot gate, doc updates.

## Staged plan

This ticket is multi-stage by design: implement one stage per session, each stage ending with a verifiable result and a handoff prompt for the next session.

### Stage 1 — Baseline, parallel safety, runner contract, critical-path split (complete)

- [x] Record the measurement method and baseline in `docs/evidence/test-parallelization.md`.
- [x] Add `xunit.runner.json` (`parallelizeTestCollections`, `maxParallelThreads: "1x"`, `parallelAlgorithm: conservative`, `longRunningTestSeconds`) and copy it beside the test assembly; prove the runner reads it (the runner's `Starting: ... [N threads]` line changed when the value changed).
- [x] Introduce the `GameAssembly` collection definition with `DisableParallelization = true` (a shared collection alone would only serialize the writers; no other collection may run while a game-assembly static is being replaced) and move every test class that writes a game-assembly/Unity static into it.
- [x] Remove the per-node rolling file sink from the test composition (`Fakes/TestLogging.cs`, wired in `TestNode` and the IP-direct tests): it removes the shared-file race, the temp directory per node and ~19 % of the summed test work; the sink keeps direct coverage in `LoggingOptionsTests`.
- [x] Add the normative gate `TestIsolationGateTests`: Roslyn parses each test class and requires the collection for any static `SetValue(null, ...)`, and the gate carries its own negative/positive unit test so it cannot silently rot.
- [x] Split the dominant critical-path class `EntityEventBehaviorTests` (135 cases in one class) into five behavior-family classes sharing one MemberData source; case count unchanged.
- [x] Verify: full build + full `dotnet test` + `dotnet format`, three consecutive green runs.

### Stage 2 — Cut the remaining critical path and the summed work

- Split the next critical-path classes, largest first, into separate classes with balanced loads: `PlayerInteractionServiceTests` (92 cases), `EntityEventSimulationTests`, `DirectionTests`, `Replays.ReplayTests`, then the next tier from a fresh measurement.
- Splitting is behavior-preserving: same assertions, same helpers, no shared mutable fixture. A shared stateless helper/base is allowed; `partial` files are not.
- Now that the reference host is throughput-bound, attack summed work as well: profile the per-node setup cost (`TestNode.Create` builds the full production DI graph, ≈500 node constructions per full run) and reduce it without weakening isolation. Record a written reason for any cost that is deliberately kept.
- Re-measure with the three-run median method after each batch; target a lower median wall clock and no single class above ~5 s.
- Verify: same test count (or a documented, justified delta), full suite green, before/after evidence recorded in `docs/evidence/test-parallelization.md`.

### Stage 3 — Fast feedback, anti-rot guard, final measurement

- Classify slow/integration tests with `[Trait("Category", ...)]` and document the inner-loop filters (`--filter "FullyQualifiedName~X"`, `--filter "Category!=Integration"`).
- Add an anti-rot guard for the long pole (a gate that fails when a test class grows past the agreed limit, or a recorded decision not to).
- Benchmark `maxParallelThreads` 1x vs 2x and keep the faster/stable setting.
- Finish the doc pass: `docs/evidence/verification.md`, `docs/evidence/normative-gates.md` and the test conventions in `AGENTS.md` are already updated for Stage 1; keep them current.
- Verify: full suite green, final measured wall clock recorded.

## Acceptance

- Every stage: `dotnet build CasualtiesUnknownOnline.slnx`, `dotnet test CasualtiesUnknownOnline.slnx` and `dotnet format` pass; test-case count and semantics unchanged except where a stage documents the delta.
- No test writes a process-global static field outside the `GameAssembly` collection (gate-enforced), and the test composition writes no per-node log file.
- The runner configuration is explicit and proven to be read.
- Stage 2 lands measured wall-clock evidence; no single test class dominates the run.
- The measurement method in `docs/evidence/test-parallelization.md` is reproducible (commands plus how per-test durations are extracted).

## Non-goals

- Migrating to xUnit v3 or the Microsoft Testing Platform.
- Multi-process / CI sharding (recorded as a follow-up; single-process class-level parallelism is the current lever).
- Reducing coverage, deleting tests, or weakening assertions to go faster.
- Production code changes, except a test-only seam that is unavoidable and documented.
