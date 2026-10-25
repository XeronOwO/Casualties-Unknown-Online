# Enter on an empty console does not close it

- Status: Todo — opened 2026-10-25 from the user's report: "打开命令框情况下,命令框是空的,按回车键为什么不能关闭命令框啊?".
- Priority: Low
- Category: Console / input behaviour
- Source: user report (2026-10-25).

## The design authority for this surface

The console's command family follows **Minecraft's current template** (root `AGENTS.md` rule 17; decision 264 chose its `/data`
verbs and the user's 2026-10-18 ruling asked for its `/summon` template). That is the authority for how the console behaves, and
this ticket follows it: in Minecraft, Enter with text sends the message/command and **Enter on an empty input closes the chat
window** — the player does not have to reach for the chat key again to dismiss an empty prompt.

The game's own console does something else, and that fact is recorded here only so nobody mistakes it for the design: in
`reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs` the console key toggles the surface
(`Input.GetKeyDown(KeyBinds.GetBind("console"))` → `ToggleActiveState()`, which also calls `input.Select()`), Tab completes a part,
and Enter (`Input.GetKeyDown(13)`) calls `TryExecuteCommand(array, true)` whose first statement returns on empty arguments — so
natively an empty Enter is a no-op and the console closes only with its own key. **That is not the behaviour this project wants:
the console is a Minecraft-shaped surface, not a copy of the game's console.** The earlier version of this ticket called the change
a "deliberate deviation from native"; that framing was wrong and is corrected here.

## Goal

With the console open and the input empty, Enter closes the console — as it does in Minecraft — while Enter with text still executes
the command exactly as it does today. The console key keeps toggling the console, and Tab keeps completing.

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
`todo/console-completion-shows-no-suggestions.md`, because the missing input box is what a player has to type into in the first place.
A reflected probe of `input.text`/`input.isFocused` was attempted with a recipe and the client rejected it as `(1,1):
InteractiveHost` (the masked compile failure the tools page documents); the recipe was dropped rather than committed broken, so the
next batch needs a working read of the input field to separate "the keys never reached the box" from "the box is not drawn".
