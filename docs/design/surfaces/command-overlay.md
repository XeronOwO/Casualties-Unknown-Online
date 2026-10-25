# Command overlay: the console as a surface

The command and chat overlay as a player meets it — its entry, its parts and its behaviour.

## What the player does

Presses the open key, sees the overlay slide up from the bottom, types, reads candidates, submits, browses history, and
closes it. The world stays visible behind it.

## What it is

CUO's own overlay on the game's canvas: a translucent, bottom-anchored panel with a history area, one input line with its
own cursor, a candidate list, and a hint line. It is not the game's native console.

## Parts and states

| Part | What it shows |
|---|---|
| history | the session's command and chat lines, the full list while the panel is open, never faded |
| input line | the line being typed, with its own cursor and selection, a prompt in front of it |
| candidates | the commands or arguments matching what is typed, live as the player types |
| hint | the current argument's expectation, where the command family has one |

- open and closed are the two states; the transition slides the panel up and down.
- while open, the overlay captures the keys it needs (Escape, Enter, Tab, history keys) so the game does not act on them.
- the overlay accepts no pointer input outside its own panel, and its controls are reachable while the world is played.

## Rules

- An empty Enter closes the overlay; a non-empty Enter submits the line.
- Tab accepts the current candidate; the candidate list is live without Tab.
- Escape closes the overlay and must not reach the game's pause handling: the defect it was fixed from is "pressing ESC while the console is open still triggered the game's" own pause path, and the accepted reading is that all three expected behaviours pass on the deployed artifact.
- Completion covers two vocabularies: the command tree and the resource ids, so a player can type an id by its name and have the namespaced id offered — the accepted ticket for that half is `done/command-id-name-completion.md`.
- A notice shown while the overlay is closed fades; while it is open, the history does not.

## Refusals

None of its own: it collects the line and renders the answer it is given. An answer that refused is a line in the
history.

## Who judges

The overlay itself decides only what to show. A command's outcome is judged where that command's own rules say.

## What travels

A submitted line travels as the command or chat line it is; the answer comes back as a key plus its values.

## What the player sees and hears

A small translucent panel over the world, live candidates under the input line, answers appearing in the history.

## Records

Decision 228 (the `/` overlay is the only command and chat surface). Accepted:
`../../backlog/done/in-game-command-console-interactive.md` (entry keys, live candidates, no-fade history, Escape
interception), `done/command-console-esc-not-intercepted.md`, `done/console-command-replies-bilingual.md`.

## Gaps

- The panel's exact geometry (heights, paddings, font sizes) is not recorded here yet; the accepted design's frames hold
  it.
