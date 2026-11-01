# The Enter/Slash opener can claim a key the game is rebinding

- Status: Review — the capture-state fact landed (2026-10-24, deployed `0.1.0+cb0bf017`): the guard now sees the
  game's key-bind capture through the marker the capture itself publishes, with no patch and no pause gate. The
  runtime row (Enter and Slash bind the key and do not open the console while a row waits) still needs a batch with
  real key presses; its state is observable in-process (`OnlineUiKeyboardFacts.IsAKeybindCaptureWaiting`: an active
  `SettingsMenu` carrying a row whose text is exactly `...`), so what it waits on is ONE authorisation to drive real keys
  for a single batch: open the settings menu, click a key-bind row so its capture starts, press Enter and Slash, and read
  both whether the console opened and what the row bound. Found by the independent review of the Enter-guard fix;
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

**Where the capture state is read (2026-11-01, 07:40).** The guard's second arm is in the adapter
(`OnlineUiKeyboardFacts.IsAKeybindCaptureWaiting`): an active `SettingsMenu` carrying a `TMP_Text` whose text is exactly `...` is
the capture, because `SettingsMenu.WaitForKeyPress` (`reversing/Assembly-CSharp/Assembly-CSharp/SettingsMenu.cs:196-213`) writes
that literal into the row it is binding and keeps it until a key arrives. So the state the row needs is observable in-process —
`menu open + a row reading "..."` — and what the row still needs is the two REAL key presses: `WaitForKeyPress` binds the first
`Input.GetKeyDown` it sees, and the acceptance tooling drives no keyboard. **This row therefore waits on one-time authorisation to
use `tools/acceptance/send-input.ps1 -Action key` for a single batch** (the same OS channel the earlier batch used for its
clicks), and the batch is: open the game's settings menu, click a key-bind row so its capture starts, then press Enter and Slash
and read (a) whether the console opened and (b) what the row bound.

## The batch that tried it (2026-11-01): the row is reachable, and a click cannot leave it waiting

The settings menu is reachable with the OS channel and was driven to its controls tab (frames `settings1.png`, `settings2.png`: the tabs and the key-bind rows, the jump row reading Space through the throw row reading T). Clicking a key-bind row starts the game's capture — and BINDS `Mouse0` in the same click, so the row is never left waiting: the next frame already showed the throw row reading `Mouse0` (`bind-waiting.png`). An Enter pressed after that capture had ended opened the CUO console (`bind-enter.png`), which is the ticket's own "with no capture running they open the console as before" case, not a failure of the guard.

**So the row needs a capture that is still waiting when the key arrives, and this build gives no click-driven way to hold one open.** Two ways forward, both a decision rather than a guess: press a key the game IGNORES as the binding while the row is fresh (so the capture stays open across the batch's next key), or drive the row's capture through the game's own code path (a probe that calls the row's capture entry directly, the way `open-own-wound` calls the game's panel entry). The second is the honest instrument; the first is a trick that depends on the game's ignore list.

**One UX question the frames raise**: with the console open over the settings menu, both are drawn at once and the console takes the keyboard. Whether the console should open while the game's own settings menu is up — or refuse with a line — is a player-facing choice, and the settings menu is the one place a player expects the keyboard to belong to the game.

## The marker was wrong, and the staged key does not reach the capture (2026-11-02, 16:40)

A new probe (`keybind-capture-state`) reads the marker and whether the settings menu is open, which made two things visible:

1. **The marker answered true with no capture.** With the settings menu open on its Video tab, `IsAKeybindCaptureWaiting` read true and the console's Enter never opened. The cause is the marker itself: it scanned the menu for any `"..."` text, and an action with NO bind shows the same "..." as its value — the game never clears it. Narrowing the scan to active children did not help (the tab panels stay active). The marker now reads the label the capture COROUTINE is writing, through a prefix on `SettingsMenu.WaitForKeyPress`, so it is true only while that label still shows "...". Verified both ways: menu open with no capture → `capture=false`; a row clicked into capture → `capture=true` with the frame showing `...` in the 跳跃 row.
2. **The staged key does not reach the capture loop.** With a real capture waiting, pressing Return through the in-process keyboard produced exactly one true `GetKeyDown` answer (`virtual-key-stats`: `keyDownAnswers=1`), and yet the row still showed `...` and `keybind-read action=jump` still answered `Space (32)`. So the coroutine did not act on it, and the console did not open either — the row's premise (Enter binds the key while the capture waits) is neither confirmed nor refuted by this channel yet.

Two readings would finish the row and neither needs OS input: which frame the coroutine's `GetKeyDown` query lands on relative to the staged edge (the eval runs after Update, so the edge is stamped for the NEXT frame), and whether the game's capture loop runs while the pause handler holds the menu open. The `virtual-key-stats` counters answer the first directly.