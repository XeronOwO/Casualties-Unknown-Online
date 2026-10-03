# Acceptance record — Partial-damage report vs the live delta

- Ticket: `partial-damage-delta-report-overlap` — verdict: **moved to `done/`** (rows 1–7 pass)
- Batch: `20261003-f` — tickets `guest-partial-block-damage-re-report`, `partial-damage-delta-report-overlap`,
  `guest-break-drops-recovery`, `guest-command-loss-reconciliation`,
  `entity-destruction-drop-guest-fresh-state-loss`, `runtime-entity-markerless-bind-absorption`,
  `trap-layout-entry-snapshot-staleness`, `trap-layout-snapshot-recovery`, `turret-stray-fire-after-reload`,
  `trap-action-divergence-hardening`, `unhooked-damage-block-callers`, `session-control-convergence`
  (scope: `docs/evidence/acceptance/20261003-f-scope.md`)
- Commit: `76ef80c2` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+76ef80c28162de716c38626075b69a2032fc8206` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-03, from 09:54 +08:00 · Host: physical machine (launched through Steam) · Guests: the primary
  and the alternate sandboxes; three clients, one world, one lobby
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0); the alternate
  sandbox supplied the third peer
- Artifacts: the `s0*`–`s2*` ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | One sender: delta, then the absolute re-report — counted once | `machine` | **pass** | on the shared cell (520,985) the guest's inbound was cut at 10:05:15.097 so the host's answer could not clear the entry (`s1-row3-guestblackout-on.json`); the guest's live hit took both sides to `damage=3` (`s1-row3-guest-hit.json`, `s1-row3-host-live.json`) while the entry stayed at `pending damage=1` (`s1-row3-guest-pending.json`); lifted 10:05:20.779; the same absolute value re-reported at 10:06:16.105 (`[BlockSync] re-reported 1 unaccounted partial-damage cell(s) to the host.`) resolved to **no increment** — the host still read `damage=3` (`s1-row3-host-final.json`) — and the answer returned the guest's pending to 0 (`s1-row3-guest-pending-final.json`, `s1-row3-guest-final.json`) |
| 2 | One sender: absolute re-report, then the delayed delta — counted once | `machine` | **pass** | after the row-3 re-report and its answer (10:06:16.105), a later live hit landed on the same cell at 10:06:37.149 (`s1-row4-guest-live-hit.json`, `dmg=1`); the host resolved the later delta to exactly one increment: host, guest and alternate all read `damage=4` (`s2-row2-host-after-live.json`, `s2-row2-guest-after-live.json`, `s2-row2-alt-after-live.json`) |
| 3 | Two senders, both interleavings — the sum, never the maximum | `machine` | **pass** | two senders hit one cell around a single host-inbound blackout (10:03:49.682–10:03:56.042): each contribution landed once and the host's row is their sum — `damage=2`, not the maximum 1 — with the alternate reading 2 as well (`s1-row2-base-*.json`, `s1-row2-*-hit.json`, `s1-row2-host-during.json`, `s1-row2-*-after4.json`; host log `Partial block-damage report received from <guest> (1 cells).` 10:04:50.779 and `… from <alternate> (1 cells).` 10:04:52.022). The delta-then-report and report-then-delta interleavings are rows 1–2 above; the exact retransmitted-delta interleaving is unit-pinned and named as such |
| 4 | Duplicate absolute re-report — idempotent | `machine` | **pass** | the re-report at 10:06:16.105 was a duplicate of an already-accounted contribution: it changed nothing (host stayed `damage=3`) and the answered entry stopped re-sending (`s1-row3-host-final.json`, `s1-row3-guest-pending-final.json`) |
| 5 | A cell that has since broken — ignored (air); the block-state channel owns it | `machine` | **pass** | the break-before-report staging on (516,985): the guest broke the cell locally at 10:06:43.187 under a host-inbound blackout (`s1-row4-guest-break.json`, pending `block=1, damage=1`), and when the fallback re-report landed at 10:07:37.003 the damage was not re-applied to the air cell — all three read `block=0, damage=-1` (`s1-row4-*-after3.json`, host `Partial block-damage report received from <guest> (1 cells).`) |
| 6 | The game's 128-entry cap — the live path's own semantics: the game's list evicts its oldest entry, which the report path now shares with the live relay; the per-sender ledger's own cap degrades to "every report is new" and is logged once per episode | `machine` | **pass** | the host's own list was filled to the cap (`s2-fill-cap.json`: `damaged=140, tableAfter=128, evicted=12`, first cell (512,993)); a fresh hard block was placed at (512,995) (`s2-fresh-place.json`) and its contribution staged under a host-inbound blackout (10:10:47.241–10:10:54.015; `s2-cap-blackout-on/off.json`, `s2-cap-guest-hit.json`, `s2-cap-guest-pending.json`); the report at 10:11:53.381 took the fresh cell and evicted the oldest — `tableCount=128`, (512,993) absent, (512,995) present (`s2-cap-census-after.json`) — and every side converged on the fresh cell at `damage=1` (`s2-cap-*-after.json`). The per-sender ledger's own cap (65 536) is unreachable in a session and stays unit-pinned |
| 7 | Third-party view — the same total on every side | `machine` | **pass** | the alternate read the same number as the host at every staging: `damage=1` after the swallowed re-report (`s1-row1-alt-read-after3.json`), `damage=2` after the two-sender sum (`s1-row2-alt-after4.json`), `damage=3` after the duplicate (`s1-row3-alt-final.json`), `block=0, damage=-1` after the break-before-report (`s1-row4-alt-after3.json`), `damage=4` after the delayed delta (`s2-row2-alt-after-live.json`), `damage=1` for the cap cell (`s2-cap-alt-after.json`) and the cell-for-cell agreement after the world re-entry (`s1-row6b-cmp-alt-*.json`) |

## Limits

- One session, one world; every blackout was a single-stepped inbound cut of 4.1–6.8 s, checked before arming
  and after lifting and kept inside the 15 s host-silence watchdog.
- The exact retransmitted-delta interleaving (a delta re-delivered after its own report) is not producible in a
  live session; it is unit-pinned and named as such here, per the batch scope.
- The 128-entry cap half is the game's own list; the per-sender ledger's cap (65 536) is unit-pinned.
- The two-sender staging is a driven sequence (hits 1.2 s apart), not a simultaneous race; the inter-command
  gap is stated.
- Nothing in this ticket is a `feel` or `visual` row.

## Residuals for the user

- None: every row was judged from this run's evidence.
