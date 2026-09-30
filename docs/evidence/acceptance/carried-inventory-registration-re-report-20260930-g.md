# Acceptance record — Carried-inventory registration has no re-report

- Ticket: `carried-inventory-registration-re-report` — verdict: **stays in `review/`**
  (rows 1–2 stand from batch `20260930-f`; row 3 `unproven` again — the table converged
  after the reconnect, but through the kernel rebuild path, and the rebuild-by-report the
  row names could not be exercised)
- Batch: `20260930-g` — the reconnect single-point session shared with
  `guest-container-contents-ghost-drops-on-host`
- Commit: `7c4da9df` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+7c4da9df802612e66bdd9ac1e8efe71ba6c4b43a` (deploy and `verify-deploy.ps1`
  exit 0)
- Run: 2026-09-30 23:05 → 23:19 (+08:00) · Host: physical machine (evaluator `18590`) ·
  Guest: primary sandbox (evaluator `18591`; relaunched three times) · Third peer: not
  used in this batch
- Dependencies: preflight `11 present`, exit `0`; machine gate before launch `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`
- Artifacts: the batch directory under `acceptance-artifacts-dir` — probe JSON and the two
  clients' logs, cited below by name

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest starting inventory; registration dropped → host converges; take arbitration works | machine | not re-run — batch `20260930-f` verdict stands | — |
| 2 | An id added after the first report → converges on the next report | machine | not re-run — batch `20260930-f` verdict stands | — |
| 3 | Reconnect while in world → table rebuilt exactly once | machine | **unproven** | The reconnect edge fired: the host granted the watermark on the member's return (`[IdWatermark] granted 3` in the host log) and the handshake completed end to end, three times. With the table cleared offline (`r1-00-clear-offline.json`, 2 → 0) the table converged after the rejoin to three unique entries — light, bag, dogfood, each `isTransferred=true` with kernel location `Carried` (`r1-02-host-table-after-rejoin.json`, `r1-03-host-tables-after-rejoin.json`) — but no `Registered … carried items` line and no `Carried inventory of …` frame followed that reconnect: the convergence came from the kernel rebuild path the batch-f record already named as "exists and was not the mechanism observed", not from the registration re-report. The reason the re-report sent nothing in that cycle is observable: the guest's body held no capturable items at the time (`r1-01-guest-local-after-rejoin.json`, `localCount=0`). In the control reconnect (table left populated) the re-report did fire — the host received six new `Carried inventory of …` frames and the guest's traffic log names `CarriedInventory` sends — but every frame was a no-op because the table was already full, so no `Registered` line appears by design (`ItemArbitration.RegisterCarried` logs only when something new is registered). The row's "rebuilt exactly once" therefore cannot be judged from this run: the outcome converged, the mechanism it names never had a chance to be observable. |

## Residuals for the user

- None: every reading this run could decide is machine- or log-judged.

## Limits

- The reconnect's registration evidence depends on the guest having a non-empty captured
  set; on this machine the rejoin's world restore outruns the dense 12 × 5 s window, so a
  run must watch the steady minute after the body exists before judging "no report".
- No third peer in this batch; the per-guest tables of two guests are not re-read.
- One session does not disprove a rare race; the batch-f P2P wedge remains an
  unreproduced observation.
