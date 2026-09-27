# Acceptance record — Projection failure auto-recovery

- Ticket: `projection-failure-auto-recovery` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

This ticket carries no acceptance section, so its `## Landed` section is enumerated claim by claim.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The lightweight `ProjectionHealthCoordinator` provides a per-domain dirty/rebuild loop. | machine | pass | `ProjectionHealthCoordinatorTests` (10 cases, all `Passed`): `Run_Success_TracksLastSuccessfulRevision`, `Run_Failure_MarksDirtyAndDeferRebuildToPump`, `Run_SuccessAfterFailure_KeepsDirtyUntilPumpRebuild`, `Pump_RebuildsDirtyDomainFromKernelReadModelAndClearsDirty`, `Pump_RebuildFailure_KeepsDirtyAndCountsAsAnotherFailure`, `RepeatedFailures_EscalateToDegradedState`, three `Register_` cases and `ProjectionDomain_NullArguments_Throw`; `src/CasualtiesUnknownOnline.Runtime/Session/ProjectionHealth/ProjectionHealthCoordinator.cs` holds the registration (`Register`, `Register(string, Action, Func<ulong>)`) and the per-domain state |
| 2 | A projection exception is captured, the domain is marked dirty, and the last successful revision is tracked. | machine | pass | the same file: `Run` catches the exception, logs `"... failed at revision {Revision}; marked dirty for per-domain rebuild."`, sets `LastFailedRevision` and `Dirty`, and `Run_Success_TracksLastSuccessfulRevision` pins the successful side; `KernelProtocolServiceTests.ProjectionFailure_MarksDirtyAndRebuildsWithoutRevertingKernel` is `Passed` in the main suite |
| 3 | The affected domain is rebuilt from the kernel read model on the main-thread pump. | machine | pass | `Pump` rebuilds exactly the dirty domains through the registered read-model callback (`Rebuild` calls `state.Rebuild()` and then reads `CurrentRevision()`, logging `"... rebuilt from kernel read model at revision {Revision}."`) and clears dirty on success; pinned by `Pump_RebuildsDirtyDomainFromKernelReadModelAndClearsDirty`, `Passed` |
| 4 | Repeated failures escalate to a degraded/diagnostic state. | machine | pass | the file's failure branch sets `Degraded` after the repeat threshold and `Snapshot` exposes `LastSuccessfulRevision`, `LastFailedRevision`, `Dirty` and `Degraded`; pinned by `RepeatedFailures_EscalateToDegradedState` and `Pump_RebuildFailure_KeepsDirtyAndCountsAsAnotherFailure`, both `Passed` |
| 5 | First production adoption: items, fluids, and world-entities. | machine | pass | the first adopters are `ItemService`, `FluidKernelReadProjection` and `WorldEntityKernelProjection`; the coordinator now also serves `WorldRunProjection`, `RemoteCharacterPresentationStore` and `ModStatusProjectionReadModel` (through `IProjectionDomain`), `PlayerKernelCarryProjection`, `PlayerInteractionService` and `WorldService`; registered once as a singleton in `src/CasualtiesUnknownOnline.Runtime/Composition/ItemComposition.cs` (`services.AddSingleton<ProjectionHealthCoordinator>()`, then exposed as `ICuoService`), which `KernelProtocolServiceTests.ProjectionFailure_MarksDirtyAndRebuildsWithoutRevertingKernel` exercises end to end in the main suite |

## Residuals for the user
None.

## Limits
No client was started: no rendering, no frame, no live log and no probe, so nothing here observes a running host or guest — the main-thread pump and the three adopting projections are judged from the coordinator's source, the composition registration and the named tests, not from a live session. Every row is decided by `ProjectionHealthCoordinatorTests` (and, for rows 1, 2 and 5, `KernelProtocolServiceTests.ProjectionFailure_MarksDirtyAndRebuildsWithoutRevertingKernel`) plus direct inspection of `ProjectionHealthCoordinator.cs` and `ItemComposition.cs`. The ticket cites `docs/evidence/selfchecks/architecture/projection-health-coordinator-selfcheck.md`, which this record does not rely on. Batch scope and exclusions are recorded in `docs/evidence/acceptance/20260927-b-scope.md`.
