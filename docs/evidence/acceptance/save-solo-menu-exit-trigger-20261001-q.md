# Acceptance record — S3.6 solo menu-exit trigger for the mid-run cut

- Ticket: `save-solo-menu-exit-trigger` — verdict: **stays in `review/`** (row 4 was not staged: the
  host's Steam transport runaway ended Run E before it; rows 1, 2, 3, 5, 6 and 7 are judged below)
- Batch: `20261001-q` (Run D solo; Run E host + guest) — siblings `save-mid-run-consistent-cut`,
  `save-run-clock-not-sent`, `save-new-player-starting-supplies`
- Commit: `2efca14b` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2efca14b871112f814b64c8338b69ea47e5f2d44`
- Run: 2026-10-01 14:13 → 14:31 · Host: physical machine (Steam) · Guest: the primary sandbox (Run E)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Solo play, in world, leave to the menu | machine | **pass** | `d-host-log-leave1.txt`: `Cut MenuReturn armed … the pump takes it at the frame-end seam`, `Cut MenuReturn committed for world w-20261001-95ec: mid-run cut taken at frame-end, revision 325 … 9 file(s)`, then `Leaving the world to the main menu`; `d-leave-1.json` reads `inWorld: false` |
| 2 | Solo play, not in world (menu/generation) | machine | **pass**, limit named | `d-call-tomainmenu-outside.json`: outside a world there is no `PlayerCamera` (`no-camera`), so the patched instance call is unreachable by construction; no cut was requested and nothing was written (`d-host-log-leave2-outside.txt`: backups 2 → 2, no new cut line). The gate and policy are pinned by this batch's suite (`suite.txt`) |
| 3 | Host (with a guest) deliberately leaves | machine | **pass** | `e-host-log-leave-with-guest.txt`: `Cut MenuReturn committed for world w-20261001-b0d7 … revision 1982 … 678 enemy row(s), 243 fluid chunk(s), 37 world-entity row(s), 2 character(s)`, then `Leaving the world to the main menu`; the guest is pulled out with the session still up (`e-guest-state-after-host-leave.json`: `inWorld: false`, `active: true`, role Guest) |
| 4 | Guest leaves | machine | **unproven** | not staged: Run E ended on the host's transport runaway (see Limits) |
| 5 | Cut refused (no world write possible) | machine | **pass** | injection: the saves root renamed aside and a same-name file placed at its path; `d4-host-log-refused-cut.txt`: `Cut MenuReturn of world w-20261001-95ec failed at StageFailed: <root>: Cannot create '<root>' because a file or directory with the same name already exists`, immediately followed by `Leaving the world to the main menu`; `d4-leave-refused.json` reads `inWorld: false`; after the root was restored the previous snapshot is intact (backups 5 → 5, live files 9 → 9) |
| 6 | A stale teardown request is dropped | machine | **pass** | this batch's suite (`suite.txt`; `RunMenuReturnPolicyTests.DecideFlush_TeardownRequestedAgainstANewSession_IsDropped`) |
| 7 | Tutorial entry, then leave | machine | **pass** | `d5-start-tutorial.json` (the game's own `PreRunScript.StartTutorial` entered the world); `d5-host-log-tutorial.txt`: `This entry is the tutorial — it gets no world archive, and the previous run's identity is released`, and the leave logs the coordinator's leave with **no** cut line; the worlds directory count is unchanged 15 → 15 |

## Residuals for the user

None: every judged row is a state, a log line or a number.

## Limits

- **Row 4 was not staged.** Run E's host hit a Steam transport send-limit runaway (see the batch's
  sibling records and the follow-up ticket); the run stopped the host to protect the machine. The row
  stays unjudged.
- **Row 2's live half is limited by the game's own surface**: the patched `PlayerCamera.ToMainMenu` is
  an instance method and there is no camera outside a world, so the run could observe "nothing
  requested / nothing written" but could not invoke the call itself; the decision rule is suite-pinned.
- **The solo rows ran against a solo world (no session role); row 3 ran against a live host + guest
  session.** Row 3's cut is the same directed leave the ticket describes ("the same cut, taken before
  the leave").
- The refused cut was injected by renaming the save root (declared substitution); the refusal branch
  observed is `StageFailed`, not a stuck transient.
