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

- Two openers: `/` opens the overlay as a command line, Return opens it as plain chat with an empty field.- An empty Enter closes the overlay; a non-empty Enter submits the line.- The opener is refused while the GAME's own text field holds focus: the accepted defect is that "after the game's own console has been opened and closed, the event system still names its input as the selected object, so `IsATextFieldFocused` stayed true and the Enter/Slash opener was refused" — the guard follows the event system's own selected object.
- Tab accepts the current candidate; the candidate list is live without Tab.
- Escape closes the overlay and must not reach the game's pause handling: the defect it was fixed from is "pressing ESC while the console is open still triggered the game's" own pause path, and the accepted reading is that all three expected behaviours pass on the deployed artifact.
- Completion covers three vocabularies: the command tree, the resource ids (a player types an id by its name and the namespaced id is offered — `done/command-id-name-completion.md`) and a selector's bracket filters, which are completed bracket-aware (`done/command-tree-resource-location-selector.md`).
- A notice shown while the overlay is closed fades; while it is open, the history does not.

## Answers and their kinds

An answer carries its KIND, so a refusal never renders as a success. The accepted record states the defect and the fix: every non-empty handler return used to be added as ConsoleLineKind.Success, and the overlay mapped Success to the positive colour — a refusal therefore read as a success. The landed shape is a typed answer (ConsoleAnswer in the abstractions) with two tests pinning the kind, so a refusal is drawn as a refusal.

## Refusals

None of its own: it collects the line and renders the answer it is given. An answer that refused is a line in the
history.

## Who judges

The overlay itself decides only what to show. A command's outcome is judged where that command's own rules say.

## What travels

A submitted line travels as the command or chat line it is; the answer comes back as a key plus its values.

## What the player sees and hears

A small translucent panel over the world, live candidates under the input line, answers appearing in the history.

## What it replaced

A second removal confirms the same decision: the duplicated command console PAGE was taken out of the Online UI window, with the acceptance reading that there is no Console tab and the console's own open/suggest/submit path is the one players use.`r`n`r`nThe console was first landed as "a local command/chat console inside the Online UI" with "a registered command chain (help, players, rtt, whoami, kick, ban, unban) with role-based" access. That shape is SUPERSEDED: decision 228 states the window has no console page, and the accepted interactive design (next section) keeps the `/` overlay as the only command and chat surface. A reader who finds the older ticket must follow decision 228, not the older page.`r`n`r`n## Records

Decision 228 (the `/` overlay is the only command and chat surface). Accepted:
`../../backlog/done/in-game-command-console-interactive.md` (entry keys, live candidates, no-fade history, Escape
interception), `done/command-console-esc-not-intercepted.md`, `done/console-command-replies-bilingual.md`.

## Gaps

- The panel's exact geometry (heights, paddings, font sizes) is not recorded here yet; the accepted design's frames hold
  it.
