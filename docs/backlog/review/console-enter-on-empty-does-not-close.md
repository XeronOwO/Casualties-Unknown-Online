# Enter on an empty console does not close it

- Status: Review — code landed 2026-10-25 (`e46a17ca`); rows 1, 2 and 4 judged in batch `batch-enter`, row 3's key toggle is the one
  residual a batch without OS input cannot press.
- Priority: Low
- Category: Console / input behaviour
- Source: user report (2026-10-25, translated): "with the console open, the console is empty — why does pressing Enter not close it?".

## Design

`docs/design/mechanics/console.md` states the overlay's key bindings and follows Minecraft's chat window (root `AGENTS.md` rule 17;
decision 228 keeps the in-game `/` overlay the only command and chat surface): Enter with text sends, and Enter on an empty input closes
the window. The behaviour lives in `ConsoleInputSession` — the class that owns the line's semantics — and the overlay's Return branch is
the only caller.

## What the code does now

`ConsoleInputSession` carries the two rules side by side:

- `Submit` stays the EXECUTOR: an empty line returns false, executes nothing and leaves the console open.
- `SubmitOrClose` is ENTER's rule: a line with text executes and keeps the console open so another command can follow; an empty or
  whitespace-only line closes the input.

The overlay's Return branch calls `SubmitOrClose`, so the key is consumed in both arms; Tab, Escape and typing are untouched. Four tests
pin the pair, including the trap this change walked into first: `Open()` pre-fills the command prefix, so an "empty" console only exists
in the plain-chat opener or after the prefix is deleted.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | Empty console + Enter closes it (frame before/after and the visibility state) | **passes** — `wasOpen=true, emptied="", consumed=true, isOpenAfter=false`; `row1-before.png` shows the panel and its suggestion list, `row1-after.png` shows it gone |
| 2 | Text + Enter executes and keeps the console open | **passes** — `input="/"`: `consumed=true, isOpenAfter=true`, and the panel printed its refusal |
| 3 | The console key still opens and closes; Tab still completes | **residual** — the code is unchanged and Tab was judged at the same build in `done/console-completion-shows-no-suggestions.md`; the key toggle itself needs an OS-level key press |
| 4 | Whitespace-only counts as empty and closes | **passes by test** |

## Limits

- The key that opens the console is the game's own binding, so its toggle is the one thing this ticket cannot read without an OS-level
  key press; everything the rule itself decides is read from the session's state and from frames.
