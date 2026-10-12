# Acceptance record — The world-entry trap-layout fanout can carry an entity the host has removed

- Ticket: `trap-layout-entry-snapshot-staleness` — verdict: **moved to `done/`**
- Batch: `20261012-c` — offline batch; scope and plan: `docs/evidence/acceptance/20261012-c-scope.md`
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91` — deployed and hash-verified in this run (`tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `dotnet` — build 0 warnings / 0 errors in 12.4 s (`build.log`), gate suite `CasualtiesUnknownOnline.NormativeGates.Tests` 626 passed / 0 failed / 0 skipped (`gates.log`), behavioural suite `CasualtiesUnknownOnline.Tests` 5072 passed / 0 failed / 0 skipped (`full.log`), and `dotnet format CasualtiesUnknownOnline.slnx` (`format.log`, the tree byte-identical before and after); `tools/deploy.ps1` (`deploy.log`) and `tools/verify-deploy.ps1` (`verify-deploy.log`); `tools/acceptance/preflight.ps1` (11 present, exit 0) and `tools/acceptance/session-environment.ps1 -Mode status` (CUO tree active, `game-running=false`), both captured before the build; no game client
- Artifacts: none — the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | An entity was removed (self-destruct / break) and a member enters the world before the next repair — the entry group carries the LIVE table, so no phantom is materialized | machine | pass | `CasualtiesUnknownOnline.Tests.World.TrapLayoutEntryFreshnessTests.WorldEntry_ReDerivesTheLayout_SoARemovedEntityIsNeverSent` — `Passed` in this run's behavioural suite; the case records a turret in the table, holds only the spike in the live scene, and asserts the entry group carries the live table |
| 2 | Reconnect while InWorld inside the same window — same, and the member's existing copies are not duplicated | machine | pass | `CasualtiesUnknownOnline.Tests.World.ReconnectWorldSnapshotTests.GuestReconnects_WhileStillInWorld_ReceivesTheLiveLayout` — `Passed` in this run's behavioural suite; the reconnect is driven through the real handshake restore of `member.InWorld` and the case asserts that EVERY send around the reconnect carries the live table, so a stale send is not masked by a fresh one after it |
| 3 | An entity is removed while every member stays in the world — unchanged: the periodic in-session repair destroys the surplus | machine | pass | `CasualtiesUnknownOnline.Tests.World.TrapLayoutEntryFreshnessTests.InSessionRepair_ReDerivesTheLayout_TheSameWay` — `Passed` in this run's behavioural suite; the repair send re-derives the table and carries the live layout, never a record the world has removed |
| 4 | The host's scene scan sees nothing (world in transition) — the fail-safe keeps the last known table, never a mass destroy | machine | pass | `CasualtiesUnknownOnline.Tests.World.TrapLayoutEntryFreshnessTests.AnEmptyLiveScan_KeepsTheLastDerivedTable` — `Passed` in this run's behavioural suite; the send attempts the re-derive and the kept entry still rides with its identity intact |
| 5 | Third-party guest — all peers converge to the same layout | machine | pass | `CasualtiesUnknownOnline.Tests.World.TrapLayoutEntryFreshnessTests.EveryEnteringMember_ReceivesTheSameLiveTable` — `Passed` in this run's behavioural suite; both members enter and both receive the same live table |

## Residuals for the user

None. Every row of this ticket's acceptance matrix is a `machine` row and this run judged it from the behavioural suite's own per-case outcomes; no row asks for a person's judgment.

## Limits

- No client was started in this batch, so there is no rendering, frame, live log or in-process probe for any row: the rows are decided by this run's test output (the behavioural suite's per-case `Passed` lines, read through the batch's anchor index) and by inspection of the committed tree.
- The Game Adapter's own half of the mechanism is not exercised by these cases and is not claimed here: `LiveTrapLayoutSource`'s guards (`Time.frameCount`, `TrapEntityScan`'s `FindObjectsOfType`, `HarmonyTraverse.IsGenerating`) are Unity-typed and cannot run in the test host — the ticket states this boundary itself and assigns that half to the dual-client acceptance pass.
- Row 4 drives the Runtime seam through the fake live-layout source, not through the adapter's `LiveTrapLayoutSource`; the ticket records the same limit. The neighbouring guard case `CasualtiesUnknownOnline.Tests.World.TrapLayoutEntryFreshnessTests.WithoutThePort_TheSendCarriesTheLastDerivedTable` is also `Passed` in this run but is not one of the matrix's five rows.
- The historical numbers the ticket carries (the red run of 5 failed / 4 passed on the seam-present tree, the wider filter's 3454 passed, 69/69 gates, anchors 21 → 27 and evidence entries 982 → 988) were **not re-measured**. The durable outcome recorded here is that the family the ticket names still exists and passes in this run: 5072 passed / 0 failed in the behavioural suite and 626 passed / 0 failed in the gate suite.
- The two limits the ticket records for this landing (the asymmetric empty-scan fail-safe, and the generation-edge scan's order dependence against the boundary reset) are the ticket's own recorded limits; this batch neither re-measures nor contradicts them.
