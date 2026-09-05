# Command console ESC still opens the game's ESC/pause menu

- Status: Todo
- Priority: High
- Category: Tooling / UI / input interception
- Source: User report (2026-09-05) — after the interactive command console redo, pressing ESC while the console is open still triggers the game's native ESC/pause menu; the command overlay's own ESC handling does not fully swallow it.

## Problem

While the in-game command console is open, pressing ESC closes the console or is
intended to close it, but the game's ESC menu also opens. The user observes that
the game's ESC menu function is not intercepted.

## Current implementation context

- `CommandConsoleOverlay.HandleKeys` handles `KeyCode.Escape` with `Close()` +
  `evt.Use()` (`src/CasualtiesUnknownOnline.Plugin/CommandConsoleOverlay.cs`).
- `Plugin.Update` calls
  `adapter.SetOnlineUiModal(_onlineUi.IsWindowVisible || _onlineUi.IsCommandConsoleOpen)`
  while the console is open.
- `PlayerCameraHandleInputPatch.Prefix` and `PauseHandlerTogglePausePatch.Prefix`
  skip native input only when `IsOnlineUiModalOpen` is true.
- Despite this, the user still sees the game's ESC menu; the exact bypass path is
  unverified.

## Expected behavior

- Pressing ESC while the command console is open closes only the console.
- The game's ESC/pause menu must not open.
- When the console is closed, ESC keeps the game's native behavior.

## Investigation directions

- Add runtime trace/log to confirm whether `PauseHandler.TogglePause` or
  `PlayerCamera.HandleInput` is reached while `IsCommandConsoleOpen` is true.
- Verify the modal flag is set before the game input path runs in the same
  frame, including the ESC frame that closes the console.
- Check whether the game has another ESC/pause menu path outside
  `PauseHandler.TogglePause` / `PlayerCamera.HandleInput`.
- Cover the standalone console overlay and any other command-input surface.

## Acceptance criteria (draft)

- A single ESC while the console is open closes the console and does not open the
  game ESC menu, verified on the deployed artifact.
- No regression for ESC when the console is closed.
- Add a regression/runtime evidence path before moving to `review/`.
