# Acceptance record — random rolls in a cross-player item use (batch 20261011-b)

- Ticket: `cross-player-item-use-random-determinism` — verdict: **stays in `review/`** (row 1 now passes;
  rows 2-5 and 8 are still unjudged, and they are not this world's scenarios)
- Batch: `20261011-b` — tickets: `medical-state-names-no-kind` (rows 1-4) and this ticket (row 1's open
  half). Scope and the pre-declared criteria: `docs/evidence/acceptance/20261011-b-scope.md`
- Commit under test: `c6a35bb9` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+c6a35bb9…`, verified against `<game-dir>` (`tools/verify-deploy.ps1`, exit 0)
- Run: 2026-10-11 12:24 → 12:34 · Host: physical machine (the operator) · Guest: first sandbox (the
  patient) · Third client: alternate sandbox
- Dependencies: `tools/acceptance/preflight.ps1` — 11 present; `session-environment.ps1 -Mode status` —
  `active=cuo`, `game-running=false`, `swap-needed=false`
- Artifacts: the batch directory under `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | a bare-hand shrapnel pull that breaks the grip: the affected client's skin/bleed/pain before and after agree with what the host committed, and the operator's screen ends on those same values | machine | **pass** (the half batch `20261011-a` left open) | the authority derived and committed one slip — `session 3 grip slip 2 on piece 0 by <host> (head=False, seed -1901959984): derived skin 5.783163 bleed 0.9278045 pain 11.24498 brain 0.`, then `both sides derived the same values`, then `break-grasp applied to <guest> limb 1: skin -5.783163 bleed +0.9278045 pain +11.24498` (host log 12:31:26.570/.571). The PATIENT's own limb 1 reads `94.217 / 0.928` (`run2-guest-limb1-after-slip.json`, 12:31:32.324) — exactly the committed values from a 100/0/0 baseline — and its log carries the non-terminal write batch `20261011-a` could not find: `[MedicalOps] local target body updated (terminal=False).` at 12:31:26.461. The authority's stored copy still reads `94.217 / 0.928` ~17 s later. The operator's own screen: the drive reported `skinDelta 5.783, bleedDelta 0.928, painDelta 11.245` for the same slip, and `tryBegin: "slip"` with `minigameActive: true` |
| 1 (third-party half of the same disagreement) | — | machine | **pass** | the third client's displayed limb advanced to `94.217 / 0.928` at 12:31:32.931, 8.7 s before the session's terminal (`terminal TimedOut` at 12:31:41.596) |
| 2-5, 8 | the late-joiner replay, the injection and the drink whose liquid carries a per-call roll, the unrelated local roll, and "neither action reads as a stall" | — | **unjudged** | not this batch's world or scenario; they need their own batch |

The full row-by-row reasoning, the timings and the drive's own gates are in the sibling record,
`docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md`; this ticket's subject — a divergence
between what the operator's screen shows and what the authority commits — was the defect that record's row 1
closes.

## Limits

- Pain is read, not compared as a constant: the native limb drives it every frame (the ticket's own
  analysis), so the three clients show it at different points of the same decay. Skin and bleed are the
  frozen values while the limb carries fragments, and those are the ones the row is judged on.
- The row was judged with the driver's numbers for the operator's screen, not from a captured frame.
- This run judged one slip in one session; a rare race is not excluded by it.
