# Acceptance record — Composite command sequential semantics

- Ticket: `composite-command-sequential-semantics` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

The ticket carries no acceptance section; its rows are the claims of its landing text.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | GameStateKernel.ExecuteComposite decides and reduces each inner command in declaration order on the same working copy, so a later command can see an earlier command's staged result. | machine | pass | `CompositeCommandKernelTests.Composite_LaterInnerCommandSeesEarlierStagedResult` passed in the main suite; the entry point is `ExecuteComposite` in `src/CasualtiesUnknownOnline.GameState/GameStateKernel.cs` |
| 2 | Any rejection discards the working copy and the whole composite is atomic. | machine | pass | `CompositeCommandKernelTests.Composite_Rollback_WhenLaterInnerCommandRejected`, `CompositeCommandKernelTests.Composite_RejectsAllWhenAnyInnerCommandRejected` and `CompositeCommandKernelTests.Composite_CommitsCrossDomainBatchAtomically` all passed |
| 3 | Only the composite's OperationId is an idempotency key; inner OperationIds are not independently recorded. | machine | pass | `CompositeCommandKernelTests.Composite_InnerOperationIdsAreNotSeparateIdempotencyKeys` and `CompositeCommandKernelTests.Composite_DuplicateOperationId_ReturnsOriginalDecision` both passed |
| 4 | Staged spawn-then-update dependency, rollback on later rejection, duplicate composite idempotency, plus existing atomic commit and guest replay are covered. | machine | pass | `CompositeCommandKernelTests` 7/7 passed, including `CompositeCommandKernelTests.Composite_ReplaysAllEventsOnGuestKernel` and the four cases named in rows 1-3 |
| 5 | Docs updated: `docs/architecture/current.md` and `docs/architecture/domains.md`. | machine | pass | both pages state the cross-domain rule and name CompositeGameCommand and GameStateKernel.ExecuteComposite with the staged-result semantics of declaration order on one working copy |
| 6 | Selfcheck: `docs/evidence/selfchecks/architecture/composite-command-sequential-semantics-selfcheck.md`. | machine | pass | the sheet exists at that path |

## Residuals for the user
None.

## Limits
No client and no deployed artifact were exercised; the kernel decision is decided by its own test class over the production kernel, and rows 5 and 6 are file facts. The ticket states the semantics only — no live session, no wire round trip and no guest/host rendering were observed, and none is claimed.
