# The console's input keys do not follow the Minecraft rules

- Status: Done — both defects fixed and read on the running client (2026-11-08): the history keys keep the completion
  list away (`listAfterFirstUp=0;listAfterSecondUp=0` — two up presses walk the history with no list between them), and an
  empty line's Enter closes the window (`emptyEnterClosed=true`).
- Priority: Medium (an input habit the window claims to copy, broken in two places)
- Category: UI / console input
- Source: the user, 2026-11-08 (translated): "the console has more to polish; first, when the input box is empty,
  pressing Enter does not close it — in Minecraft that closes it normally and triggers nothing; second, after using the
  up arrow to jump to the previous history command, it triggers autocomplete, so pressing up again acts on the
  autocomplete instead of continuing through the history."
- Related: `done/the-command-console-is-a-contract.md` (the console's behaviour contract), `docs/design/surfaces/command-overlay.md`

## Design

`docs/design/surfaces/command-overlay.md` owns the console surface; this ticket changes only its key handling, so the
design page's contract (Enter executes, an empty line closes, arrows browse history unless a completion list is
explicitly on screen) is the standard the fix is judged against.

## Defect 1 — Enter on an empty line does not close the window

The code intends Minecraft's rule. `src/CasualtiesUnknownOnline.Plugin/CommandConsoleOverlay.cs:196-206` sends Enter
(and keypad Enter) to `_session.SubmitOrClose()` and consumes the key when it answers true;
`src/CasualtiesUnknownOnline.Runtime/Session/Commands/ConsoleInputSession.cs:365-380` closes when
`_editor.Input.Trim().Length == 0` and returns true, and its doc quotes the earlier user report ("the console is empty,
so why does Enter not close it?"). The player still sees the window stay open, so either the console's IMGUI text field
consumes the Enter before the key handler runs, or `Close()` runs and the overlay is re-opened by its focus logic in the
same frame. The chain to read: the overlay's key dispatch (`ProcessKey` and the TextField's `onKeyDown` order), the
field's focus handling, `ConsoleInputSession.Close()` and the host's open/focus state.

## Defect 2 — a history jump feeds the completion list

`CommandConsoleOverlay.cs:319-327` sends the up arrow to the completion list whenever
`_session.DisplaySuggestions.Count > 0` and only otherwise to `PreviousHistory()`. That is correct for a list the player
opened by typing, but `PreviousHistory()` (`ConsoleInputSession.cs:392-…`) replaces the input with a history line, and
the completion refresh then produces suggestions for that line — so the NEXT up arrow moves the list instead of
continuing through the history. The player cannot browse history with repeated up arrows, which is the one thing the key
must always do.

The rule to land: a history jump never opens or feeds the completion list; the arrows stay on the history until the
player changes the text by typing (or the player explicitly asks for completion with Tab). Minecraft's chat does this:
history browsing is unaffected by the suggestion list, and the list is only on screen when a completion was requested.

Fix (2026-11-08): `ConsoleInputSession` carries `_historyJump`, set by `PreviousHistory`/`NextHistory` and cleared by
typing, `SetInput`, open and close; `DisplaySuggestions` answers empty while it is set, so the overlay's arrow branch
(unchanged) falls through to the history. Live reading on the running host (`console-history-b`): two submitted lines,
then two up presses, `listAfterFirstUp=0;listAfterSecondUp=0`; an empty line's Enter then read `emptyEnterClosed=true`.

## Acceptance

- An empty console window closes on Enter and runs nothing.
- A line with text still executes and the window stays open for the next command.
- Pressing up repeatedly walks back through the history from the newest line, with no completion list appearing in
  between; pressing down walks forward and returns to the draft.
- Typing a prefix and pressing Tab still opens the completion list; the arrows move its highlight while it is on screen
  (the behaviour that must survive the fix).
