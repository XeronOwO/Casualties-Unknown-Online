# The Enter/Slash opener can claim a key the game is rebinding

- Status: Todo — found by the independent review of the Enter-guard fix (2026-10-24); pre-existing.
- Priority: Low-Medium
- Category: Online UI / input / native keyboard ownership
- Source: the review of `docs/backlog/done/enter-key-opens-plain-chat.md` read the game's settings
  key-bind capture and found it is not a text field, so the guard the opener asks cannot see it: the
  capture is a button-driven coroutine that binds whichever key is pressed.
- Related: `docs/backlog/done/enter-key-opens-plain-chat.md` (the guard and its pause-menu limit),
  `src/CasualtiesUnknownOnline.GameAdapter/OnlineUiKeyboardFacts.cs` (the reading),
  `src/CasualtiesUnknownOnline.Plugin/OnlineUiHost.cs` (`/` and Enter share one opener gate).

## Problem

The game's settings key-bind capture (`reversing/Assembly-CSharp/Assembly-CSharp/SettingsMenu.cs`) starts
`WaitForKeyPress` from a click on a plain `Button` (lines 133-140) and then, inside the coroutine, binds
the first `KeyCode` it sees pressed (`foreach (object obj in Enum.GetValues(typeof(KeyCode))) if
(Input.GetKeyDown(keyCode)) …`, lines 196-213). There is no `TMP_InputField` or `InputField` anywhere in
that path, and the selected object is a button, not a field — so
`OnlineUiKeyboardFacts.IsTheGameUsingTheKeyboard` answers false while the capture waits.

The CUO opener (`OnlineUiHost.Update`) then claims the same key: pressing Enter to bind Enter also opens
the CUO console in plain-chat mode, and pressing `/` to bind Slash also opens command mode. The guard's
own pause check does not exist (a pause-wide gate would change when the opener works, which is a
user-visible decision), and the existing console/text-field facts cannot see the capture.

## What the fix owes

- A reading that is true while the capture waits, or a decision that the opener stands down while the
  game is paused (the capture is reached through the pause menu; a main-menu settings capture is the other
  entrance and would need its own fact).
- The reading's shape is a decision, not a detail: a pause gate is one line but changes when the player
  can open CUO's console; a capture-state fact needs either a game flag (none exists) or an adapter patch
  around `WaitForKeyPress`; a reflective read of the coroutine's own state is the third option.
- Acceptance: with a key-bind row waiting, Enter and Slash bind the key and do NOT open the CUO console;
  with no capture running, Enter still opens plain chat and Slash still opens command mode.

## Limits if it stays open

The pause-menu path is the ordinary way a player reaches a key-bind row in a run; the main-menu settings
page is the other entrance. Until this lands, the guard's doc comment names the gap
(`INativeInputBlocker.IsTheGamesOwnTextInputFocused`), so a reader of the guard does not mistake the
coverage for complete.

## Decision point (2026-10-24) — waiting on the user

The shape is a design decision because the cheap one changes when the opener works:

1. **A capture-state fact** (recommended): an adapter patch around `SettingsMenu.WaitForKeyPress` (or a
   reflective read of its coroutine) exposes "a key-bind row is waiting", and the guard asks it. Precise, no
   behaviour change outside the capture, but it patches a game coroutine.
2. **A pause gate**: the guard answers true while the game is paused. One line (`PauseHandler.paused`), but it
   also stops `/` and Enter from opening CUO's console whenever the game is paused — a user-visible change
   beyond the capture, and the main-menu settings capture stays uncovered.
3. **Patch the game's console/capture to deselect or clear** on close — fixes the root cause but changes game
   UI behaviour for the console's own use.

Recommendation: option 1 for the capture plus the guard's current console fact; option 2 only if the user
wants "no CUO opener while paused" as its own rule. No work is started because the patch-vs-pause choice is
user-visible.
