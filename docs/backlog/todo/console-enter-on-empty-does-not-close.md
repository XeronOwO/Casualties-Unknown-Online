# Enter on an empty console does not close it

- Status: Todo — opened 2026-10-25 from the user's report: "打开命令框情况下,命令框是空的,按回车键为什么不能关闭命令框啊?".
- Priority: Low
- Category: Console / input behaviour
- Source: user report (2026-10-25).

## The design authority for this surface

The console is **CUO's own Minecraft-style overlay**, designed and accepted in
`docs/backlog/done/in-game-command-console-interactive.md` (batch `20261018-t`), and its command family follows **Minecraft's
current template** (root `AGENTS.md` rule 17; decision 264 for the `/data` verbs, decision 269 for `/gamerule`, decision 228 for the
`/` overlay being the only command and chat surface). In Minecraft, Enter with text sends and **Enter on an empty input closes the
chat window** — the player does not reach for the chat key again to dismiss an empty prompt.

The implementation that owes this behaviour is `CommandConsoleOverlay` + `ConsoleInputSession`: the overlay's Return branch calls
`_session.Submit()` and only consumes the key when it returns true, so an empty line currently leaves the overlay open. That is the
defect; the game's own native console (`ConsoleScript`, opened by the console key) is a DIFFERENT surface and not the one the user
was typing in — a 2026-10-25 batch opened the native console by mistake and its frames are recorded as a failed batch, not as
evidence about this ticket.

## Goal

With the console open and the input empty, Enter closes the console — as it does in Minecraft — while Enter with text still executes
the command exactly as it does today. The console key keeps toggling the console, and Tab keeps completing.

## Done in code (2026-10-25, `e46a17ca`)

`ConsoleInputSession` now carries both rules side by side: `Submit` stays the EXECUTOR (an empty line returns false, nothing runs, the
console stays open) and `SubmitOrClose` is ENTER's rule — text executes and keeps the console open, an empty or whitespace-only line
closes the input. The overlay's Return branch calls `SubmitOrClose`, so the key is consumed in both arms; Tab, Escape and typing are
untouched. Four tests pin the pair, including the trap this change walked into first: `Open()` pre-fills the command prefix, so an
"empty" console only exists in the plain-chat opener or after the prefix is deleted.

Rows 2-4 are covered by those tests. **Row 1 still needs a frame**: the console must be seen closing on an empty Enter in the running
client, and the frames from the 2026-10-25 batch never showed an input box at all — that gap belongs to
`review/console-completion-shows-no-suggestions.md` (the same overlay, whose input field did draw once opened through
`OnlineUiOverlay.OpenCommandConsole`).

## Acceptance rows

| Row | The check |
|---|---|
| 1 | With the console open and empty, pressing Enter closes the console (a frame before/after, and the console's own visibility state) |
| 2 | With text in the box, Enter still executes that command and the console stays open, exactly as before the change |
| 3 | The console key still opens and closes the console, and Tab still completes, so the rule adds one behaviour instead of replacing the others |
| 4 | A command that is only whitespace counts as empty and closes (nothing is executed, and the input is not left holding the spaces) |

## Batch note (2026-10-25, frames read)

A first pass on the running host opened the console with the console key and captured two frames (empty console, then after typing a
prefix): the log panel is visible, but **no input box and no typed text are visible in either frame**, and pressing Enter in that
state does nothing (the native no-op above). Both frames are the evidence for rows 1 and 4 of this ticket and are also referenced by
`review/console-completion-shows-no-suggestions.md`, because the missing input box is what a player has to type into in the first place.
A reflected probe of `input.text`/`input.isFocused` was attempted with a recipe and the client rejected it as `(1,1):
InteractiveHost` (the masked compile failure the tools page documents); the recipe was dropped rather than committed broken, so the
next batch needs a working read of the input field to separate "the keys never reached the box" from "the box is not drawn".
