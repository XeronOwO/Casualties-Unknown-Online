# Acceptance record — Dead runtime-entity relay API: `BroadcastEntitySpawned` has no caller

- Ticket: `runtime-entity-dead-api-cleanup` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | `rg 'BroadcastEntitySpawned' src tests` returns either zero hits (deleted) or exactly the call site(s) that need it, each with a comment naming why the source is excluded. | machine | pass | bounded grep for `BroadcastEntitySpawned` over `src/` returns zero hits and over `tests/` returns zero hits — the deleted branch of the ticket's criterion, so no comment obligation arises |
| 2 | The live relay still reaches the reporter (its echo is the acknowledgement). | machine | pass | `src/CasualtiesUnknownOnline.Runtime/Session/World/RuntimeEntityChannel.cs` keeps the live path `SendEntitySpawned`, whose host branch records the accepted creation and broadcasts with `_session.Broadcast(NetMsg.EntitySpawned, msg)` — a source-including broadcast — while `ISessionControl.BroadcastExcept` remains in use by the other relays; with zero hits for the deleted forward, no source-excluding path is left on this channel |
| 3 | Full build + tests + `dotnet format` green; no behaviour change on the live path. | machine | pass | build exit 0 with 0 warnings / 0 errors; gate suite 288/288; main suite 4482/4482 with 0 not executed; `dotnet format CasualtiesUnknownOnline.slnx` exit 0 with the working tree unchanged; the removed surface had no call site, so the live path it never served is the one row 2 verifies as unchanged |

## Residuals for the user
None.

## Limits
No client was started: no rendering, no frame, no live log and no probe, so "the reporter's echo acknowledges its pending report" is judged from the channel's source and the zero-hit grep, not from a running host and guest. Rows 1 and 2 are decided by bounded greps over `src/` and `tests/` (zero hits) plus inspection of `RuntimeEntityChannel.cs`; row 3 by the batch's build, suite and format results. The ticket's `## Verification` section also records a deployment with per-assembly hash comparison from its own 2026-09-09 cycle — that is not this batch's evidence and no row here depends on it. The ticket's earlier figures (build, suite, gates) are historical; this batch's numbers are the ones cited above. Batch scope and exclusions are recorded in `docs/evidence/acceptance/20260927-b-scope.md`.
