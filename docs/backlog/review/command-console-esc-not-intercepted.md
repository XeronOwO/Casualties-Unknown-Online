# Command console ESC fully intercepted

- Status: Review (code complete; waiting for the final unified acceptance pass)
- Priority: High
- Category: Tooling / UI / input interception
- Source: User report (2026-09-05) — after the interactive command console redo, pressing ESC while the console is open still triggered the game's native ESC/pause menu. Resolved in this cycle.

## Problem

While the in-game command console is open, pressing ESC should close only the console. The user observed that the game's ESC menu also opened.

## Root cause

The command console overlay closes on ESC inside IMGUI `OnGUI`. The game's native pause handling runs in `PlayerCamera.HandleInput` during `Update`. Depending on Unity event ordering, `Plugin.Update` can observe an ESC-closing CUO surface as already closed in the same frame in which the ESC is still active, and would clear the modal guard before the game's native input path runs. The existing `PlayerCameraHandleInputPatch` and `PauseHandlerTogglePausePatch` are correct, but they depend on the modal guard remaining set during that closing frame; the closing frame was the one gap. The same ordering applies to the Online UI window and the non-modal quick panel, so the fix was applied to the whole ESC-closing family.

## Fix

- Added a Unity-free `CuoEscCloseSuppression` policy in Runtime that tracks the command console, Online UI window, and quick panel visibility states, and returns one-frame suppression when any of them closes.
- `Plugin.Update` now keeps `IGameAdapter.SetOnlineUiModal(true)` for the first frame after an ESC-closing CUO surface closes, so the closing ESC is swallowed by the existing modal input guard. The next frame clears the modal state.
- Added `IGameAdapter.SetOnlineUiEscapeSurfaceVisible` / `IPatchBridge.IsNonModalEscapeSurfaceOpen`: while the non-modal quick panel is visible, `PauseHandlerTogglePausePatch` suppresses only the native pause toggle, without making the panel fully modal.
- Covered the whole ESC-closing family: standalone command console, Online UI modal window, and the non-modal quick panel. Added per-surface ESC-consumed logs and a close-frame log, so a deployed run can confirm the guard held on the closing frame.

## Expected behavior after fix

- A single ESC while the command console is open closes the console and does not open the game ESC menu.
- When the console is closed, ESC keeps the game's native behavior.
- The one-frame suppression is intentionally narrow: the next frame after an ESC-closing surface closes restores normal input immediately.

## Evidence / verification

- New regression tests: `CuoEscCloseSuppressionTests` — command-console close, Online window close, quick-panel close, dangerous OnGUI-before-Update order, next-frame release, stay-open, one-surface-closes-while-another-remains-open, closed-from-start, reopen-then-close (9 cases); plus 2 input-guard contract tests for the new escape-surface setter.
- Full test suite: 2335 passed / 0 failed.
- `dotnet build`, `dotnet format`, `check-architecture`, `check-event-replay`, `check-entity-event-dispatch`, `check-no-absolute-paths`, `check-delivery` all pass.
- Deployed to the physical game directory with `tools/deploy.ps1`; deployed plugin/Runtime/GameAdapter/Abstractions hashes match build output.
- Final dual-client visual acceptance remains the user's unified acceptance pass; no claim of actual two-client manual verification is made here.
