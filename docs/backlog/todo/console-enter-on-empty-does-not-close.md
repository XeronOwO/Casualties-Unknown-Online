# Enter on an empty console does nothing, not close

- Status: Todo — opened 2026-10-25 from the user's report: "打开命令框情况下,命令框是空的,按回车键为什么不能关闭命令框啊?".
- Priority: Low
- Category: Console / native input behaviour
- Source: user report (2026-10-25).

## What the game itself does (read first, so the ticket states the native fact)

In `reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs`, the console's key handling during a frame does this: the console
key toggles the whole surface (`Input.GetKeyDown(KeyBinds.GetBind("console"))` → `ToggleActiveState()`), Tab completes a part
(`Input.GetKeyDown(9)` → `TryFinishCommandPart`), and Enter (`Input.GetKeyDown(13)`) calls `TryExecuteCommand(array, true)` — whose
FIRST statement is `if (args.Length == 0 || string.IsNullOrEmpty(args[0])) { return; }`. So natively, **Enter on an empty console
returns without doing anything, and never closes the console**: the console is opened and closed only with its own key. The
behaviour the user expects therefore does not exist in the game.

## Goal (a deliberate CUO deviation, recorded as such)

With the console open and the input empty, Enter closes the console — the same result the console key gives — while Enter with text
in the box still executes the command exactly as the game does. The console key keeps working as it does now.

## Why deviate

The console is CUO's most-used surface during development, and the user expects the shell convention that an empty Enter dismisses the
prompt. It is a small, local input rule with no effect on any command's semantics. Because the rule is CUO's own, it is recorded
here and in the code that implements it, per the native-parity standard (`docs/development/native-parity.md`), which requires a
deviation to name the native chain it replaces and the reason.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | With the console open and empty, pressing Enter closes the console (a frame before/after, and the console's own visibility state) |
| 2 | With text in the box, Enter still executes that command and the console stays open, exactly as before the change |
| 3 | The console key still opens and closes the console, and Tab still completes, so the deviation adds one rule instead of replacing the native ones |
| 4 | A command that is only whitespace counts as empty and closes (nothing is executed, and the input is not left holding the spaces) |
