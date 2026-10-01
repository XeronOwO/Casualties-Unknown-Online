# Acceptance record — DI cycle guard / cycle-path diagnostics

- Ticket: `di-cycle-guard` — verdict: moved to `done/`
- Batch: `20261001-t` — offline batch; scope and limits: `docs/evidence/acceptance/20261001-t-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working
  tree over `15ffc42a`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+1b088511d555cf09ca19b336c2f3715f94c7df4e` —
  `tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"; no client was started
- Run: 2026-10-01 — preflight 17:46 (11 present, exit 0); focused evidence run 17:47
  (`DiCycleGuardTests` 6/6, `CuoServiceOrderTests` 3/3); the run-local composition-build probe; full
  suite with build (4,573 + 300, 0 failed)
- Dependencies: `dotnet` (both suites), the run-local probe; no game client
- Artifacts: the `20261001-t` directory under `acceptance-artifacts-dir` —
  `probe-composition-time.txt`, `focused-20261001-t.log`, `full-suite-20261001-t.log`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A deliberately introduced `A → B → A` cycle in a test composition raises/logs `A -> B -> A` and does not hang | machine | pass | `DiCycleGuardTests` 6/6 `Passed`: `FieldBackedConstructorCycle_ValidateOnBuildThrowsWithChain` (`AggregateException` naming both services), `FactorySelfCycle_ResolveThrowsInsteadOfHanging`, `FactoryToConstructorCycle_ResolveThrowsWithFullChain`, `FactoryCycle_InvokesDiagnosticCallback` |
| 2 | The production startup continues to succeed with the current acyclic graph | machine | pass | the run-local probe built `CuoBootstrap.BuildServiceProvider` five times, all succeeding and disposing; `CuoServiceOrderTests` 3/3 `Passed`; the full suite green |
| 3 | The check adds no meaningful startup cost (< 50 ms) and no behaviour change for valid graphs | machine + run-local probe | pass | the probe's readings: 177.682 ms (first build, including JIT/assembly load), then 13.881 / 1.531 / 1.393 / 1.302 ms. The whole production build — registrations, `ValidateOnBuild` and `DiCycleGuard.WrapFactoryDescriptors` — is bounded by the warm readings, so the guard's share is under the 50 ms bound; `ValidFactoryChain_StillResolves` and `WrapFactoryDescriptors_PreservesDescriptorOrder` `Passed` |
| 4 | Existing tests + gates stay green | machine | pass | full suite with build: 4,573/4,573 main and 300/300 gates, 0 failed; the gates are the run's own suite output |

## Residuals for the user

None.

## Limits

- Row 3's bound is an upper bound taken from the whole composition build, not a differential
  measurement against a guard-less build; the first reading includes JIT and assembly loading.
- No runtime behaviour is observed: the diagnostic's file channels (`LogOutput.log`, `latest.log`) are
  the implementation's own path and are not re-observed here; the ticket's repair coverage is the test
  suite's.
