# Acceptance record — random rolls in a cross-player item use (batch 20261011-b)

- Ticket: `cross-player-item-use-random-determinism` — verdict: **stays in `review/`** (row 1 now passes;
  rows 2-5 and 8 are still unjudged, and they are not this world's scenarios)
- Batch: `20261011-b` — tickets: `medical-state-names-no-kind` (rows 1-4) and this ticket (row 1's open
  half). Scope and the pre-declared criteria: `docs/evidence/acceptance/20261011-b-scope.md`
- Commit under test: `c6a35bb9` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+c6a35bb9…`, verified against `<game-dir>` (`tools/verify-deploy.ps1`, exit 0)
- Run: 2026-10-11 12:24 → 12:34 · Host: physical machine (the operator) · Guest: first sandbox (the
  patient) · Third client: alternate sandbox
- Dependencies: `preflight.txt` and `session-environment-status.txt` in the batch directory — 11 present,
  `active=cuo`, `game-running=false`, `swap-needed=false` (re-captured after the run; the machine was left
  in the same state)
- Artifacts: the batch directory under `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | a bare-hand shrapnel pull that breaks the grip: the affected client's skin/bleed/pain before and after agree with what the host committed, and the operator's screen ends on those same values | machine | **pass** (the half batch `20261011-a` left open) | the authority derived and committed one slip — `session 3 grip slip 2 on piece 0 by <host> (head=False, seed -1901959984): derived skin 5.783163 bleed 0.9278045 pain 11.24498 brain 0.`, then `both sides derived the same values`, then `break-grasp applied to <guest> limb 1: skin -5.783163 bleed +0.9278045 pain +11.24498` at 12:31:26.570/.571 (`host-slip-session-3.log.txt`). The PATIENT's own limb 1 reads `94.217 / 0.928` (`run2-guest-limb1-after-slip.json`, read at 12:31:32.324) — exactly the committed values from a 100/0/0 baseline (`run2-guest-seed-shrapnel-limb1.json`, `run2-host-saved-guest-limb1-before-slip.json`) — and its own log carries a non-terminal write 414 ms after the commit: `[MedicalOps] local target body updated (terminal=False).` at 12:31:26.985 and .986 (`guest-slip-session-3.log.txt`; the line at 12:31:26.461 precedes the commit and is the session-opening state, not this write). The authority's stored copy still reads `94.217 / 0.928` ~17 s later (`run2-host-saved-guest-limb1-after-slip-2.json`). The operator's own screen: the drive reported `skinDelta 5.783, bleedDelta 0.928, painDelta 11.245` with `tryBegin: "slip"` and `minigameActive: true` (`run2-host-break-grasp-3.json`) |
| 1 (third-party half of the same disagreement) | — | machine | **pass** | the third client's displayed limb advanced to `94.217 / 0.928` at 12:31:32.931 (`run2-alt-display-limb1-after-slip.json`), 8.7 s before the session's terminal (`terminal TimedOut` at 12:31:41.596, `host-slip-session-3.log.txt`) |
| 2-5, 8 | the late-joiner replay, the injection and the drink whose liquid carries a per-call roll, the unrelated local roll, and "neither action reads as a stall" | — | **unjudged** | not this batch's world or scenario; they need their own batch |

The full row-by-row reasoning, the timings (including the probe read landing 5.75 s after the slip rather
than inside the scope's one-second window) and the drive's own gates are in the sibling record,
`docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md`. This ticket's subject — a divergence
between what the operator's screen shows and what the authority commits — was the defect that record's row 1
closes.

## Limits

- Pain is read, not compared as a constant: the native limb drives it every frame (the ticket's own
  analysis), so the three clients show it at different points of the same decay. Skin and bleed are the
  frozen values while the limb carries fragments, and those are the ones the row is judged on.
- The row was judged with the driver's numbers for the operator's screen, not from a captured frame.
- This run judged one slip in one session; a rare race is not excluded by it.
