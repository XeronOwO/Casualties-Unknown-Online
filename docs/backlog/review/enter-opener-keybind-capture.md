# The Enter/Slash opener can claim a key the game is rebinding

- Status: Review — the capture-state fact landed (2026-10-24, deployed `0.1.0+cb0bf017`): the guard now sees the
  game's key-bind capture through the marker the capture itself publishes, with no patch and no pause gate. The
  runtime row (Enter and Slash bind the key and do not open the console while a row waits) still needs a batch with
  real key presses, which is named in the landed section. Found by the independent review of the Enter-guard fix;
  pre-existing.
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

## Landed (2026-10-24, deployed `0.1.0+cb0bf017`): the capture-state fact, from what the game already publishes

Option 1 was taken (the pause gate changes when the opener works, which nobody asked for), but its mechanism is
lighter than the ticket's own wording: **no patch at all**. The capture writes the literal `"..."` into the row it is
binding (`SettingsMenu.cs:198`, the only occurrence in that file) and keeps it there until a key arrives, so
`OnlineUiKeyboardFacts` now also answers true when an OPEN settings menu carries that marker. That is a fact the game
publishes already; a patch on the iterator's `MoveNext` would have needed the dynamic contract table and the
capability catalogue for no extra truth, and patching `WaitForKeyPress` itself only sees the coroutine's CREATION,
not its wait.

The read is bounded to the opener's own key edge (the guard runs when Enter or Slash is pressed), not to a per-frame
scene scan, and the settings menu has to be open for it: a closed or absent menu costs one `FindObjectOfType`.

**Runtime row still owed**: "with a key-bind row waiting, Enter and Slash bind the key and do NOT open the CUO
console; with no capture running they open the console as before". That needs a settings menu and two real key
presses — a batch with keyboard input, which the acceptance tooling does not drive.
