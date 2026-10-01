# Acceptance record — S4.3 starting supplies for a new player (batch 20261001-q)

- Ticket: `save-new-player-starting-supplies` — verdict: **stays in `review/`** (row 3's exact
  precondition was not staged; the run did observe the hold and a no-grant verdict)
- Batch: `20261001-q` (Run D solo; Run E host + guest) — siblings `save-mid-run-consistent-cut`,
  `save-solo-menu-exit-trigger`, `save-run-clock-not-sent`
- Commit: `2efca14b` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2efca14b871112f814b64c8338b69ea47e5f2d44`
- Run: 2026-10-01 14:25 → 14:31 · Host: physical machine (Steam) · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1–2 | An omitted player / a mid-run join is supplied | machine + residual | pass (batch `20261001-o`) | sibling record `save-new-player-starting-supplies-20261001-o.md`; not re-run here |
| 3 | A player the world HAS a character for: nothing granted, no second account, including a restore in flight | machine | **unproven** | the guest's own log shows the hold running twice — `Starting supplies held for this body: the world-entry group has not completed yet, and the host sends a character restore before its completion marker (waiting up to 8000ms)` (`e-guest-supply-lines.txt`, 14:25:49.891 and 14:27:59.783) — and **no grant**: both verdicts are `CUO new player: the world's own first-layer supplies (light) are already yours — CUO granted nothing on top.` (14:25:49.939, 14:28:03.468). But the target shape (a guest with a **stored character** whose restore races the verdict) was never produced: the host's Continue restored the solo world `w-20261001-95ec`, where this guest has no character, so no character restore was queued — see the run-clock record's Limits |
| 4 | A fresh run's first layer | machine | pass (observed live) | the first entry's verdict is the `AlreadyOwned` branch above (the world's own first-layer grant covered it), with no item grant |
| 5–14 | The restored-starting-layer, once-per-body, no-baseline, partial-grant, setting-table, wiring and tutorial rows | machine | pass (batch `20261001-o` + this batch's suite) | sibling record plus `suite.txt` |

## Residuals for the user

- None new; the row-3 shape remains the open one from the ticket.

## Limits

- **The exact row-3 ordering was not staged.** The guest did hold its verdict and printed no grant, but
  because the restored world had no character for it, the "restore in flight" half never existed. The
  failing shape the fix addresses (`a grant announced before the stored character arrived`) was not
  re-created, so `unproven` — not a pass.
- **The world the Continue opened was not the session world**: the repository's `lastOpenedWorldId`
  still named the solo world (a new run does not move the pointer), which is also why the character
  restore was absent. A repeat must select the session world first (the Worlds page's own control was
  used later, but the run had already restored the wrong one).
- The host's transport runaway ended the session after the second verdict; the account lines above are
  the guest's own log, captured before that.
