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

- Two openers: `/` opens the overlay as a command line, Return opens it as plain chat with an empty field.
- An empty Enter closes the overlay; a non-empty Enter submits the line.
- The opener is refused while the GAME's own text field holds focus: the accepted defect is that "after the game's own
  console has been opened and closed, the event system still names its input as the selected object, so `IsATextFieldFocused`
  stayed true and the Enter/Slash opener was refused" — the guard follows the event system's own selected object.
- **A chat line has one cap, and the host applies the same one.** `Runtime/Session/Chat/ChatPolicy.cs` is "the only
  validation surface shared by the sender-side UI path and the host's receive/relay path", with `MaxLength = 200` "a UI
  friendly cap for one line of text", and "the host uses the same rule so a guest cannot flood an oversized line into the other
  peers".
- Tab accepts the current candidate; the candidate list is live without Tab.
- **The standard this console is held to is Minecraft's command system, not "a line that runs".** Five properties decide whether it is mature, and each is`n  judged from a frame: (1) **the list is the authority** — the set drawn under the field is the set Tab, the mouse and ↑/↓ walk, recomputed per keystroke,`n  and a truncated list says so; (2) **the keys follow the list** — ↑/↓ move the highlight and preview it while the list is open, and reach the history only`n  while it is closed; (3) **a resolved argument continues the grammar** — after an item id the component/SNBT skeleton is offered, then the next argument; (4)`n  **ids are namespaced** (`namespace:path`, the default namespace omissible on input and never on output) and the verbs that take an id resolve both forms; (5)`n  **a row identifies what it offers** — canonical id, the reader's-language name, and the reason it matched (pinyin, alias).
  **A TREE, not a flat verb list (user direction 2026-10-30).** A command is a definition with literals and arguments at any depth, so `/give <selector> <id>` and`n  a mod's own `/mod sub <arg>` are the same shape; aliases are declared by the command and completion offers both forms; a mod may supply its own argument`n  parser/completer for one parameter while the framework keeps the tokenisation, the list, the keys, the permission gate and the answer shape; a`n  `HostRelayed` command runs on the host behind its own declared permission AND the host's rules, and needs the mod on both sides. The LIST is an input`n  surface: rows are clickable to complete and hoverable to preview, and a refused token is marked in place with what was expected. The five gaps measured on`n  2026-10-30 are recorded in `todo/completion-list-keys-and-ids-disagree.md`; this rule is what a fix is judged against, not a wish list.
- **The line is a small editor over one immutable registry, and its output is bounded.**
  `CommandConsoleService` registers commands "once at construction from `ConsoleCommandAttribute`-marked methods into an
  immutable command registry", parses "a slash-prefixed line, gated by a role permission, executed on this side, and answered
  into a bounded output buffer", and forwards "non-command lines … to the existing text-chat domain so the same input surface
  doubles as the reworked chat UI". It is "deliberately a local console: no new wire protocol, no packet handler, no host
  relay".
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

**Who may run a verb is one predicate, shared by the three places that ask it.** `ConsoleCommandAuthority` is "the console's
**A command's usage line is a catalogue entry, and the command's ATTRIBUTE carries the key.** `/help <command>` is one keyed template (`console.help.command`) whose description and usage both come from the reader's catalogue (`console.cmd.<name>.desc`, `console.cmd.<name>.usage`, resolved by `CommandDescriptions` from literals so the catalogue census can read them). What a language may change is the connective wording; a command name, a placeholder (`<steamId|displayName>`) and a token such as `cursor` or `slot` are what the player types and never move. An `ICommandUsage`-style interface is the recorded door for the day a usage must be COMPUTED — it would turn a command into a type and rewrite the registry, so it stays unbuilt while every line is fixed syntax (user direction 2026-10-27; shape decided 2026-10-29).

authority predicate: may THIS caller run THIS command? One decision, asked by the completion filter, by `/help` and (for the
declaration half) by the dispatcher, so a command the console hides is exactly a command the console would refuse" — with two
sources, "the declared `CommandPermission` … and the declared `ConsoleCommandGate` (a cheat verb asks the family's gate)". A
mod command declares no permission, and "its own runtime gate is not knowable here and is recorded as a limit, not modelled".

## What travels

A submitted line travels as the command or chat line it is; the answer comes back as a key plus its values.

## What the player sees and hears

A small translucent panel over the world, live candidates under the input line, answers appearing in the history.

## What it replaced

A second removal confirms the same decision: the duplicated command console PAGE was taken out of the Online UI window, with the acceptance reading that there is no Console tab and the console's own open/suggest/submit path is the one players use.

The console was first landed as "a local command/chat console inside the Online UI" with "a registered command chain (help, players, rtt, whoami, kick, ban, unban) with role-based" access. That shape is SUPERSEDED: decision 228 states the window has no console page, and the accepted interactive design (next section) keeps the `/` overlay as the only command and chat surface. A reader who finds the older ticket must follow decision 228, not the older page.

## Records

Decision 228 (the `/` overlay is the only command and chat surface). Accepted:
`../../backlog/done/in-game-command-console-interactive.md` (entry keys, live candidates, no-fade history, Escape
interception), `done/command-console-esc-not-intercepted.md`, `done/console-command-replies-bilingual.md`.

## Gaps

- The panel's exact geometry (heights, paddings, font sizes) is not recorded here yet; the accepted design's frames hold
  it.
