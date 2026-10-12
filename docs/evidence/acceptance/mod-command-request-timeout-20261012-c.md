# Acceptance record — Dropped mod command requests settle on a deadline instead of waiting for the session to end

- Ticket: `mod-command-request-timeout` — verdict: **moved to `done/`**
- Batch: 20261012-c — offline batch; scope and plan: docs/evidence/acceptance/20261012-c-scope.md
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: CasualtiesUnknownOnline.dll 0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91 — deployed and hash-verified in this run (tools/verify-deploy.ps1 exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (exit 0; its own last line reads `RESULT: OK - a full two-client run is possible`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`, `swap-needed=false`), both captured into the batch directory before the build; no client capability (steam, sandboxie, sandbox-alt, hotrepl, capture, input, logs) is consumed by this run
- Artifacts: none — the run's logs and TRX results are under the directory named by acceptance-artifacts-dir

The rows below are the ticket's own acceptance table, in its order (`Scenario` is the row, its `Evidence` is
the anchor quoted here).

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The host rate-limits a command request: the requester's callback fails with a reason, no silent wait | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModCommandTests.RateLimitedRequest_FailsOnTheDeadline_WithAnObservableReason` — `Passed` in this run: it spends the host's per-guest burst on requests that resolve, sends one more that the host drops, and asserts the deadline settles it with `Success=false` and a `timed out` reason |
| 2 | A result frame is lost: the callback fails on the deadline, not at session end | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModCommandTests.RequestWithNoResult_FailsOnTheDeadline_NotAtSessionEnd` — `Passed` in this run: the entry is still pending one millisecond before the deadline, the sweep at the deadline fails the callback, and the late result is dropped instead of settling a second time |
| 3 | A normal request/result is unchanged | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModCommandTests.SettledRequest_IsNeverReplacedByALaterDeadlineSweep` — `Passed` in this run; the untouched happy-path cases it pairs with are `...HostLocalCommand_RunsSynchronously_WithHostRequester` and `...GuestRequest_ExecutesOnlyOnHostCopy_AndReturnsDirectedResult`, both `Passed` |
| 4 | A session ending with pending requests still fails them all | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModCommandTests.SessionEnded_SettlesPendingRequestWithFailure` — `Passed` in this run (the reason is `session ended` and the late result cannot overwrite it) |
| 5 | A burst of dropped requests leaves the pending map bounded | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModCommandTests.PendingBurstOverTheCap_IsRefusedAtTheSender_AndTheMapDrainsOnTheDeadline` — `Passed` in this run: exactly the cap is accepted, the surplus is refused without a callback, every accepted entry is failed by the sweep, and a fresh request is accepted afterwards because the map drained |
| 6 | `ModStatusTransport` (and `ModMessage`) are documented as loss-tolerant, not silently reliable | machine | pass | `file:docs/en/reference/mod-api.md` — "`ModMessage` and the mod-status transport (`ModStatusTransport`) are loss-tolerant by design": a drop is accepted loss, nothing is re-sent, and the next message is the recovery, which is the semantics the row asks for. The matrix's own N8 entry records the reason — `file:docs/evidence/sync-coverage-matrix.md` — "`ModMessage` and the mod-status transport carry no backfill by design" |

The class this table drives is `CasualtiesUnknownOnline.Tests.Mods.ModCommandTests`: this run's outcome index
carries all 18 of its cases and every one of them is `Passed`, with the whole behavioural suite at 5072 passed /
0 failed / 0 skipped and the gate suite at 626 passed / 0 failed / 0 skipped.

## Residuals for the user

None. Every row is a test outcome or a documentation read this run produced; no row asks for a person's
judgement.

## Limits

- **No client was started**, so this run produced no rendering, no frame, no live log and no in-process probe,
  and no two-client session. Every row is decided by test output from this run or by reading the committed
  pages. The ticket's own `Limits` say real dual-client behaviour is unverified and the timeout was never
  observed on a real client — that stays true here, and this batch does not claim it.
- **The ticket's numbers belong to the cycle that wrote it and were not re-measured**: the class's counted red
  (3 failed / 15 passed pre-fix) and its post-fix `18 passed`, the full-suite `3514 passed` and the gate
  project's `69 / 69`. The durable outcome was judged instead — the class the rows name still exists with all
  18 cases `Passed`, and the whole suite and gate project are green in this run at the counts stated above.
- **The 10 s deadline and the 32-entry cap are judged, not measured**, exactly as the ticket records; the
  deadline's real-world value, the indistinguishability of a drop from a lost result, and the clock-wrap
  caveat all stay the ticket's own limits rather than findings of this run.
- The ticket's `Related`/status lines name decision 197 and `docs/decisions/` is outside this batch's read
  scope, so no decision entry is claimed as verified here; the rows anchor the suites and the pages instead.
