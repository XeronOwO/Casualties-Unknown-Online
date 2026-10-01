# Acceptance record — S4.3 starting supplies for a new player (batch 20261001-q)

- Ticket: `save-new-player-starting-supplies` — verdict: **moved to `done/`** (row 3's exact ordering
  was produced and passed)
- Batch: `20261001-q` (Run D solo; Run E and Run E2 host + guest) — siblings
  `save-mid-run-consistent-cut`, `save-solo-menu-exit-trigger`, `save-run-clock-not-sent`
- Commit: `2efca14b` (runs) · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2efca14b871112f814b64c8338b69ea47e5f2d44`
- Run: 2026-10-01 14:25 → 14:42 · Host: physical machine (Steam) · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1–2 | An omitted player / a mid-run join is supplied | machine + residual | pass (batch `20261001-o`) | sibling record `save-new-player-starting-supplies-20261001-o.md` |
| 3 | A player the world HAS a character for: nothing granted, no second account, including a restore in flight | machine | **pass** | the restored world sent the character restore **before** its completion marker, and the guest's own log shows the hold covering that window (`f2-entity-refusal.txt`, 14:40:25.979–26.023): `Starting supplies held for this body: the world-entry group has not completed yet, and the host sends a character restore before its completion marker (waiting up to 8000ms)` → `Received character restore (1 items)` (40 ms later) → `Applying character restore (1 items)` → `Character restore pending for this body — the world has a character for this player, so no starting supplies are granted (decision 180)`. No grant line, no supply notification and no second account were printed. The same ordering was seen on the mid-run rejoin entry (`f2-guest-supply-lines.txt`, 14:39:01.398–.889) |
| 4 | A fresh run's first layer | machine | **pass** | the run's first entry judged `CUO new player: the world's own first-layer supplies (light) are already yours — CUO granted nothing on top.` with no item grant (`e-guest-supply-lines.txt`) |
| 5–14 | The restored-starting-layer, once-per-body, no-baseline, partial-grant, setting-table, wiring and tutorial rows | machine | pass (batch `20261001-o` + this batch's suite) | sibling record plus `suite.txt` |

## Residuals for the user

- None: the supplies account is a console line and the body's tree is a probe reading.

## Limits

- Run E (the first attempt) never staged row 3's precondition: its Continue opened the repository's solo
  world, where the guest had no character, so no restore was queued and the guest judged the first-layer
  `AlreadyOwned` branch instead (`e-guest-supply-lines.txt`). Run E2 fixed that by selecting the session
  world on the Worlds page before the Continue (`lastOpened = w-20261001-6986`), which produced the
  target ordering above.
- The account lines are the guest's own log; the console rendering of the same line was not captured as
  a frame.
