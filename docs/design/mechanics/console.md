# Console: the command and chat surface

The in-game command surface every player uses to run commands, answer the host and talk.

## What the player does

Presses the open key to show the surface, types a command or a chat line, reads the suggestions as they narrow, accepts a
completion with Tab, submits with Enter, walks the history, and closes the surface with Escape or with the open key again.
The player expects the surface to behave as the chat prompt of the game it is modelled on: an empty line submits nothing
and closes the surface, a line with text runs as a command or goes out as chat, and the list of candidates appears while
typing rather than only after Tab.

## What the game itself does

The surface is CUO's own overlay drawn on the game's canvas; the game's native console is a different surface and is not
this one. The overlay owns its own input line, its own history and its own completion, and it captures the keys it needs
(Escape, Enter, Tab, history keys) so the game's own handlers do not act on them while it is open.

## Rules and states

- The surface is bottom-anchored, translucent, and leaves the world visible behind its history.
- While it is open the full history is shown, with no fading; fading belongs to the transient notices that appear when
  the surface is closed.
- Enter runs the line if it holds text; an empty line closes the surface.
- Tab accepts the current candidate; the candidate list is live while typing, so `/` immediately offers the command
  vocabulary and typing narrows it.
- Escape closes the surface and must not reach the game's own pause handling.
- The line is a single line with its own cursor and selection; the cursor sits where the next character goes.

## Refusals and answers

A command's answer is a line in the console, in the reading player's language. A command that refuses names why. Two
families of answer exist, and both are localized by the client that reads them: the answer the host decided (it travels
as a key plus its values) and the answer the console itself decided (the line never left the client).

## Who judges

A command's own rules decide whether it runs; where the command acts on another player, the host validates it and answers.
The console surface itself is a client-side surface: it decides nothing about the world, it only collects the line and
shows the answer it is given.

## What travels

A command that acts on the session travels to the host and its answer travels back. A chat line travels to the other
players and is shown by each of them in their own surface.

## What the player sees and hears

The candidates appear under the input line as they type; the answer appears in the history with the surface still open; a
refusal is one line, never a silent no-op.

## Records

`../../architecture/surfaces.md` (console row), `../../decisions/active.md` 228 (the `/` overlay is the only command and
chat surface), 264 and 269 (the `/data` and `/gamerule` families follow Minecraft's template), rule 17 of the root
`AGENTS.md` (a command's shape follows Minecraft's current template). Accepted:
`../../backlog/done/in-game-command-console-interactive.md` (batch `20261018-t`), which fixes the surface's look, its
live suggestions, its no-fade history and the Escape interception.

## Gaps

- An empty Enter currently leaves the surface open; recorded in
  `../../backlog/todo/console-enter-on-empty-does-not-close.md`.
- The candidate list's visibility and the cursor's alignment with the text are regressions against the accepted design;
  recorded in `../../backlog/todo/console-completion-shows-no-suggestions.md` and
  `todo/console-caret-misaligned-with-text.md`.
