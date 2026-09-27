# Acceptance record — Restored entity rows: a throwing row still costs the rows behind it

- Ticket: `restored-entity-row-containment` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A throwing row costs only itself and the rows behind it still land. | machine | pass | `ContainedRowLoopTests.Run_AThrowingRowIsRefusedAndTheRowsBehindItStillLand` is `Passed`: with row `b` throwing, `Applied` is 3, `Refused` is 1 and the reached order is `["a", "c", "d"]` |
| 2 | The refused count reaches the restore's account as an exact number, never "the write threw". | machine | pass | `RestoredWorldFactReplayTests.ApplyIfPending_AThrowingWorldEntityRowCostsOnlyItself` is `Passed`: the report contains `1 world-entity row(s)` and the test asserts the refused text does not contain `threw` |
| 3 | The error line names the ROW, not the loop. | machine | pass | the same two cases assert the identity in the error log at error level — `ContainedRowLoopTests.Run_AThrowingRowIsRefusedAndTheRowsBehindItStillLand` checks `row b`, the seam test checks `BearTrapClamped` — and `RunContained` logs one error line per throwing row through `Describe(identity, row)` |
| 4 | A row the applier merely refuses stays silent. | machine | pass | `ContainedRowLoopTests.Run_ARowTheWorldDoesNotTake_IsRefusedWithoutAnErrorLine` is `Passed`: a row the applier returns false for is refused with no error line, because only a throw is an unexpected refusal at the containment |
| 5 | A systematic failure is bounded, and the count stays exact. | machine | pass | `ContainedRowLoopTests.RunContained_ASystematicFailureNamesTheHeadAndSummarisesTheTail` is `Passed`: nine throwing rows give `thrown == 9`, five named lines and one summary naming `4 further row(s)` and `9 refused in all` |
| 6 | The five converted loops really use the rule. | machine | pass | source inspection of the five call sites this ticket's table names: `EntityEventSync.OnTrapStateProjected` (`Run`), `WorldBuildingEntitySync.OnOpenedEntitiesProjected` and `OnBuildingHealthProjected` (`Run`), `WorldBlockStateTable.Apply` and `GameBlockDamageTable.Apply` (`RunContained`); the tree also carries `GeneratedItemReconcile.Apply`'s entry and leftover-destruction loops on `RunContained`, so the converted set is those five plus these two; `src/CasualtiesUnknownOnline.Runtime/Session/World/ContainedRowLoop.cs` holds both shapes and the bound |

## Residuals for the user
None.

## Limits
No client was started: no rendering, no frame, no live log and no probe. Rows 1–5 are decided by the `ContainedRowLoopTests` cases and the `RestoredWorldFactReplayTests` seam case named above, all `Passed` in the main suite (4482/4482, 0 not executed); row 6 by direct inspection of the adapter call sites (the five this ticket's table names plus `GeneratedItemReconcile`'s two), because their bodies are game-typed (`TrapVisualReplay.Replay`, `Physics2D.OverlapPoint`, `world.SetBlock`, `world.GetBlockInfo`) and the test host loads the adapter reflectively. The seam suite drives a fake sink that performs the adapter's arithmetic, so it proves the rule and the accounting path, not that the production call sites are wired to it — that half is the read-only review row 6 records. The ticket's claim that no unguarded engine call is demonstrated to throw today is carried as the ticket's own limit, not as acceptance evidence. Batch scope and exclusions are recorded in `docs/evidence/acceptance/20260927-b-scope.md`.
