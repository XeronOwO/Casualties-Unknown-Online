# Acceptance record — S3.6 solo menu-exit trigger for the mid-run cut

- Ticket: `save-solo-menu-exit-trigger` — verdict: **moved to `done/`** (all seven rows judged)
- Batch: `20261001-q` (Run D solo; Run E and Run E2 host + guest) — siblings
  `save-mid-run-consistent-cut`, `save-run-clock-not-sent`, `save-new-player-starting-supplies`
- Commit: `2efca14b` (runs) · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2efca14b871112f814b64c8338b69ea47e5f2d44`
- Run: 2026-10-01 14:13 → 14:42 · Host: physical machine (Steam) · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Solo play, in world, leave to the menu | machine | **pass** | `d-host-log-leave1.txt`: `Cut MenuReturn armed … the pump takes it at the frame-end seam`, `Cut MenuReturn committed for world w-20261001-95ec … revision 325`, then `Leaving the world to the main menu`; `d-leave-1.json` reads `inWorld: false` |
| 2 | Solo play, not in world (menu/generation) | machine | **pass**, limit named | `d-call-tomainmenu-outside.json`: outside a world there is no `PlayerCamera` (`no-camera`), so the patched instance call is unreachable by construction; no cut was requested and nothing was written (`d-host-log-leave2-outside.txt`: backups 2 → 2, no new cut line). The gate and policy are suite-pinned (`suite.txt`) |
| 3 | Host (with a guest) deliberately leaves | machine | **pass**, twice independently | Run E: `e-host-log-leave-with-guest.txt` (`Cut MenuReturn committed for world w-20261001-b0d7 … revision 1982 … 678 enemy row(s), 243 fluid chunk(s), 37 world-entity row(s), 2 character(s)`, then `Leaving the world to the main menu`; the guest pulled out, `e-guest-state-after-host-leave.json`). Run E2: `f2-host-log-leave-with-guest.txt` (`Cut MenuReturn committed for world w-20261001-6986 … revision 913 … 80 enemy row(s), 235 fluid chunk(s), 24 world-entity row(s), 2 character(s)`) |
| 4 | Guest leaves | machine | **pass** | `f2-guest-leave.json` / `f2-guest-state-after-leave.json`: the guest left the world and stayed in the lobby; the guest's own log carries its coordinator's `Leaving the world to the main menu` (`f2-guest-log-leave-rejoin.txt`); the host wrote **no cut** — its world's backups stayed 0 → 0 and its log has no `Cut` line for the window (`f2-host-log-guest-leave.txt`) |
| 5 | Cut refused (no world write possible) | machine | **pass** | injection: the saves root renamed aside plus a same-name file at its path; `d4-host-log-refused-cut.txt`: `Cut MenuReturn of world w-20261001-95ec failed at StageFailed: <root>: Cannot create '<root>' because a file or directory with the same name already exists`, immediately followed by `Leaving the world to the main menu`; `d4-leave-refused.json` reads `inWorld: false`; after the root was restored the previous snapshot is intact (backups 5 → 5, live files 9 → 9) |
| 6 | A stale teardown request is dropped | machine | **pass** | this batch's suite (`suite.txt`; `RunMenuReturnPolicyTests.DecideFlush_TeardownRequestedAgainstANewSession_IsDropped`) |
| 7 | Tutorial entry, then leave | machine | **pass** | `d5-start-tutorial.json` (the game's own `PreRunScript.StartTutorial` entered the world); `d5-host-log-tutorial.txt`: `This entry is the tutorial — it gets no world archive, and the previous run's identity is released`, and the leave logs the coordinator's leave with **no** cut line; the worlds directory count is unchanged 15 → 15 |

## Residuals for the user

None: every row is a state, a log line or a number.

## Limits

- **Row 2's live half is limited by the game's own surface**: `PlayerCamera.ToMainMenu` is an instance
  method and there is no camera outside a world, so the run could observe "nothing requested / nothing
  written" but could not invoke the call itself; the decision rule is suite-pinned.
- The refused cut was injected by renaming the save root (declared substitution); the branch observed is
  `StageFailed`, not a stuck transient.
- Row 3's second observation (Run E2) ran on a world whose session ended normally; Run E's own host was
  later stopped by the separate transport runaway (see the batch scope page), which does not affect this
  row's cut line.
