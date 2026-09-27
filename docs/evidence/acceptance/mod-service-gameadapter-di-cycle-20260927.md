# Acceptance record — ModService ↔ GameAdapter DI cycle

- Ticket: `mod-service-gameadapter-di-cycle` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

This ticket carries no acceptance section, so the claims of its body are enumerated one by one.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Fixed by registering `ModStatusStore` as its own singleton and injecting only that store into `GameAdapter`/`GameAdapterDomains`. | machine | pass | `src/CasualtiesUnknownOnline.Runtime/Composition/ModComposition.cs` registers `ModStatusStore` as its own singleton (`AddSingleton` with its logger); the constructors of `src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs` and `GameAdapterDomains.cs` take `ModStatusStore`; a bounded grep for `ModService` across `src/CasualtiesUnknownOnline.GameAdapter/` returns one comment sentence and no dependency |
| 2 | The adapter no longer depends on the whole ModService, so ModService → IMod* → GameAdapter → ModStatusStore is acyclic. | machine | pass | `ModServiceDiCycleContractTests.GameAdapter_DoesNotDependOnModService_AndUsesStatusStore` is `Passed` — it reflects over the production `CasualtiesUnknownOnline.GameAdapter.GameAdapter` type, takes its single public constructor and asserts that no parameter is a `ModService` while one is a `ModStatusStore`; `ModService`'s own constructor takes `ModStatusStore`, so the remaining edge points at the store and the back-edge the cycle needed is gone |
| 3 | Regression guard: `ModServiceDiCycleContractTests.GameAdapter_DoesNotDependOnModService_AndUsesStatusStore`. | machine | pass | the guard exists at `tests/CasualtiesUnknownOnline.Tests/Patching/ModServiceDiCycleContractTests.cs` and its row is `Passed` in the batch's outcome index (main suite), so the construction that caused the cycle is now a failing assertion |

## Residuals for the user
None.

## Limits
No client was started: no rendering, no frame, no live log and no probe. The startup hang the ticket's source reports was therefore not reproduced in this batch, and no startup or in-game observation is claimed — the rows are decided by the constructor-shape guard the ticket names as its regression guard, plus direct inspection of the composition-root registration and of the adapter sources.

"acyclic" is decided as "the back-edge is absent": the guard asserts that the production adapter's constructor takes `ModStatusStore` and not `ModService`, which is exactly the dependency the ticket's diagnosis identifies, and the ticket names that guard as the fix's check. Resolving the whole production graph end to end is not something this batch exercised.

The ticket's prose names the production adapter `GameAdapterImpl`; the type the tree and the guard carry is `CasualtiesUnknownOnline.GameAdapter.GameAdapter`, and the row is judged on that type. Batch scope and exclusions are recorded in `docs/evidence/acceptance/20260927-b-scope.md`.
