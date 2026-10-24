# Acceptance record — Give an item to another player (rows 9 and 10 re-read)

- Ticket: `give-item-to-another-player` — verdict: **stays in `todo/`** (unit 2's rows 3, 4 and 6 are not
  implemented; rows 9 and 10, the two this record judges, now pass).
- Batch: `20261022-g` — ticket: `give-item-to-another-player`, rows 9 and 10, the two the previous batch left
  failed and unproven. The fix under test is commit `1f4d3ca5`.
- Commit: `1f4d3ca5` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+1f4d3ca54e77a01955cf6d0a7521addb0146b266`, verified against `<game-dir>` by `tools/verify-deploy.ps1`
  (exit 0) before the clients started.
- Run: 23:15 → 23:23 (2026-10-22) · Host: physical machine · Giver: the first sandbox.
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `dotnet`, `capture`, `input`, `logs`,
  `artifacts` — the two-client preflight state of batch `20261022-f` still held (`active=cuo`,
  `game-running=false`), and the real pointer/keyboard was used under the same one-batch authorisation, stated in
  `20261022-g-scope.md`.
- Artifacts: the batch directory under the directory named by `acceptance-artifacts-dir` (`20261022-g/`), cited
  here by file name only.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 9 | Cursor inside the target's range, on their body: the cue names that player | visual + machine | **pass** | `g1-guest-cue.png` and its zoom `g1-crop.png`: the label `Use on 萧稔` (the receiver's display name) is drawn beside the cursor, over the opened backpack; `guest-cue-state-2.json` reads `{"showing":true,"target":76561198281246659}` in the same moment, and `guest-in-range-3.json` places the pointer at game `(587,393)` against the receiver's clone at `(588,392)` — inside the 1.5-unit range; the giver's log at 23:21:18 carries `[BackpackView] opened 76561198281246659's backpack because the dragged item entered its detection range`. |
| 10 | Cursor on the displayed backpack: no use label | visual + machine | **pass** | `g2-guest-on-displayed.png` and `g2-crop.png`: no label while the pointer sits on the receiver's displayed slot-3 item button (`guest-on-displayed.json`: `pointerInside` slot 3, `itemOwner=76561198281246659`, `proxy=true`). The same drag's state read `showing:true` a moment earlier in the range (`guest-cue-state-2.json`), so the drawer was live and the absence on the backpack is the give resolution's hide; moving back into the range re-read `showing:true` and the release then landed as a use (`[DragUse] dropped bread on 76561198281246659` at 23:21:42, the receiver's own client ate it), with the drag-opened backpack closed on the next read (`guest-after-release.json`: `viewOpen=false`, `radialOpen=false`, `dragItem=none`). |

## The run's own course

- The world had the two bodies placed through `body-place` at `(0, 483.615)` and `(3.5, 483.615)`, and the giver's
  fixture was created by `item-provide mode=create` (`bread`, `#9492956821`, slot 0).
- The giver's own ring was opened with the game's own `toggleinventory` key. One attempt was made while the game
  was paused (the game's pause menu had been opened by the ESC used to check the input path), and the ring toggle
  is gated on `!PauseHandler.paused`; the run unpaused and pressed again, and every press afterwards was read back
  (`radialOpen`, the pointer's own `mouseX/mouseY`, the button's `pointerInside`) before the drag continued.
- The first drag began while the host's own configuration carried `RemoteBackpackAccess = deny` (left by an earlier
  run), so the backpack open was refused — the refusal line is in the extracted log — and the drag's `_openedFor`
  then refuses to retry the same target. The run set the rule to `allow` through the host's console, ended that
  drag, and made a fresh one for the rows. The rule was restored to `deny` (the value found) at the end.
- The camera's lead drifts on this machine (the receiver's clone moved from game `(568,382)` to `(588,394)` over
  the session), so the run re-read the clone's screen point immediately before each aiming move; the one drag that
  started against the stale point missed the 1.5-unit range and was ended without a release on the target.

## Residuals for the user

None: both rows are judged from their own frames and the state read in the same moment. The label's taste (wording,
size, exact offset) is the design the ticket already fixed; a frame cannot make it subjective.

## Limits

- The label renders in CUO's own language setting, which on this client is English (`Use on 萧稔`) while the game's
  own UI is Chinese; the localization key `cue.use_on` carries the Chinese form (`对{0}使用`) for a Chinese CUO
  locale, and this run did not switch that setting to read it.
- The pixels are judged from a captured frame, not measured.
- Rows 1, 2, 5, 7 and 8 remain batch `20261022-f`'s readings and were not re-run; rows 3, 4 and 6 are unit 2's.
- The two limits the fix itself leaves (two players with the same display name still read one label; a CUO modal
  that swallows the release leaves the game's own drag stuck) are recorded on
  `docs/backlog/review/cross-player-drag-use-feedback.md`, not judged here.
