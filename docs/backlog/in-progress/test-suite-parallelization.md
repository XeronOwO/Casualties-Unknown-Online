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

Stage 1 baseline recorded in `docs/evidence/test-parallelization.md` (Stage 2
numbers are in §7 of the same document):

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

### Stage 2 — Cut the remaining critical path and the summed work (complete)

- [x] Split the next critical-path classes, largest first, into separate classes with balanced loads: `PlayerInteractionServiceTests` (92 → 13 behaviour families), `EntityEventSimulationTests` (23 → 4 scenarios), `DirectionTests` (77 → 3 direction classes + completeness fact), `Replays.ReplayTests` (23 → 4 domains + validation), then the fresh-measurement tier: the entity-event family `MemberData` partitioned by kind shard (5 families × 3 crystal/trap/machine classes, a partition of the archive — never duplicated rows), `MedicalOperationShrapnelSessionTests` (21 → 3), `CommandConsoleServiceTests` (27 → 4) and `CompareItemTraceScriptTests` (9 → 3). Shared stateless helpers only; no shared mutable fixture; no `partial` files.
- [x] Splitting is behavior-preserving: same assertions, same helpers. Test count 2 593 → 2 595 (+2 guard facts: the behavior-family partition and the replay-domain integrity guard); all pre-existing cases unchanged.
- [x] Profile the per-node setup cost: the DI graph build is 0.41 ms — the ticket's premise was wrong. The real per-node cost is the first-frame mod discovery + load (~4.3 ms) on top of 1.1–1.5 ms container/lifecycle; a full run constructs 2 260 nodes (not ≈500). Reduced where isolation allowed: `DirectionTests` now shares one read-only `DirectionProbe` query facade per class (152 → 6 node constructions, −146 total); the per-test fresh session/world and the per-node mod load are deliberately kept and the reasons recorded in `docs/evidence/test-parallelization.md` §7.1.
- [x] Re-measure with the three-run median method: paired interleaved A/B on the same host window puts Stage 2 at 36.3 s vs the Stage 1 tree at 40.1 s (≈9.5 % faster, every pair); no class is structurally above ~5 s (clean-window maximum ≈4.2 s; contention spikes on the loaded reference host inflate individual classes — see §7.4). A `maxParallelThreads` sweep was measured and handed to Stage 3 (§7.5).
- [x] Verify: full suite green (2 595), build + `dotnet format` clean; before/after evidence in `docs/evidence/test-parallelization.md` §7.

### Stage 3 — Fast feedback, anti-rot guard, final measurement (complete)

- [x] Classified 219 full-stack/game-assembly/socket test classes with `[Trait("Category", "Integration")]` (1 306 cases tagged; 1 291 untagged) and documented the inner-loop filters (`--filter "Category!=Integration"`, `--filter "FullyQualifiedName~X"`); the fast subset measured 14.5 s wall for 1 291 cases.
- [x] Added the runtime anti-rot gate `TestClassSizeGateTests.NoTestClass_ExceedsTheCaseLimit` (40 real xUnit cases/data rows, including `MemberData` expansion, inherited test methods and static test classes; current maximum 35) plus `CaseCounting_SeesMemberDataRowsAndFlagsTheLimit` for the counting/limit contract. Source parsing was rejected because it cannot see `MemberData` rows — the exact mechanism behind the original long pole.
- [x] Benchmarked `maxParallelThreads` 1x vs 2x plus 14/12/10 in an interleaved same-window sweep (7 runs for 1x/10/14, 3 for 12/2x): 1x median 35.52 s (33.20–36.59), 2x 38.25 s, 10 36.47 s (43.16 s outlier), 12 36.33 s, 14 38.49 s; kept `1x` as the faster/more stable and portable setting.
- [x] Finished the doc pass: `docs/evidence/verification.md`, `docs/evidence/normative-gates.md`, the test conventions in `AGENTS.md`, and `docs/evidence/test-parallelization.md` §9.
- [x] Verified: main suite 2 597 + normative gates 20 passed; build and format clean; final three-run full wall clock 40.1 / 42.1 / 37.0 s (median 40.1 s), with post-hardening confirmation runs at 37.8 s and 40.7 s (window noise).

The ticket stays in `in-progress/` per the staged-handoff instruction; Stage 3
is code-complete and awaits the final unified acceptance pass.

## Acceptance

- Every stage: `dotnet build CasualtiesUnknownOnline.slnx`, `dotnet test CasualtiesUnknownOnline.slnx` and `dotnet format` pass; test-case count and semantics unchanged except where a stage documents the delta.
- No test writes a process-global static field outside the `GameAssembly` collection (gate-enforced), and the test composition writes no per-node log file.
- The runner configuration is explicit and proven to be read.
- Stage 2 and Stage 3 land measured wall-clock evidence; no single test class dominates the run.
- The measurement method in `docs/evidence/test-parallelization.md` is reproducible (commands plus how per-test durations are extracted).

## Non-goals

- Migrating to xUnit v3 or the Microsoft Testing Platform.
- Multi-process / CI sharding (recorded as a follow-up; single-process class-level parallelism is the current lever).
- Reducing coverage, deleting tests, or weakening assertions to go faster.
- Production code changes, except a test-only seam that is unavoidable and documented.
