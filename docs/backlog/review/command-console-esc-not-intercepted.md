# Command console ESC fully intercepted

- Status: Review (code complete; waiting for the final unified acceptance pass)
- Priority: High
- Category: Tooling / UI / input interception
- Source: User report (2026-09-05) — after the interactive command console redo, pressing ESC while the console is open still triggered the game's native ESC/pause menu. Resolved in this cycle.

## Problem

While the in-game command console is open, pressing ESC should close only the console. The user observed that the game's ESC menu also opened.

## Root cause

The command console overlay closes on ESC inside IMGUI `OnGUI`. The game's native pause handling runs in `PlayerCamera.HandleInput` during `Update`. Depending on Unity event ordering, `Plugin.Update` can observe the console as already closed in the same frame in which the ESC is still active, and would clear the modal guard before the game's native input path runs. The existing `PlayerCameraHandleInputPatch` and `PauseHandlerTogglePausePatch` are correct, but they depend on the modal guard remaining set during that closing frame; the closing frame was the one gap.

## Fix

- Added a Unity-free `CommandConsoleModalSuppression` policy in Runtime that tracks whether the console was open on the previous frame and returns one-frame suppression when it closes.
- `Plugin.Update` now keeps `IGameAdapter.SetOnlineUiModal(true)` for the first frame after the console closes, so the closing ESC is swallowed by the existing modal input guard. The next frame clears the modal state.
- Added an information log when the close-frame suppression is active, so a deployed run can confirm the guard held on the closing frame.

## Expected behavior after fix

- A single ESC while the command console is open closes the console and does not open the game ESC menu.
- When the console is closed, ESC keeps the game's native behavior.
- The one-frame suppression is intentionally narrow: the next frame after the console close restores normal input immediately.

## Evidence / verification

- New regression tests: `CommandConsoleModalSuppressionTests` — stay-open / close-one-frame / closed-from-start / reopen-then-close (4 cases).
- Full test suite: 2328 passed / 0 failed.
- `dotnet build`, `dotnet format`, `check-architecture`, `check-event-replay`, `check-entity-event-dispatch`, `check-no-absolute-paths`, `check-delivery` all pass.
- Deployed to the physical game directory with `tools/deploy.ps1`; deployed plugin/Runtime/GameAdapter/Abstractions hashes match build output.
- Final dual-client visual acceptance remains the user's unified acceptance pass; no claim of actual two-client manual verification is made here.
