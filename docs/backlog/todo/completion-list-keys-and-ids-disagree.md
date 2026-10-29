# Completion is not a command system yet: the list, the keys and the ids all disagree

- Status: Todo — opened 2026-10-30 from five observations in one sitting (translated from the user's Chinese report): the candidate list under the field is
  not what Tab accepts; ↑/↓ walk the command history instead of the list; nothing is offered after a completed item id; completion yields the bare game id
  instead of the namespaced one; and a pinyin hit shows a bare name with neither namespace nor Chinese label.
- Priority: High — the console is the mod-facing surface and the place a player learns what a command accepts; a list that lies teaches the wrong thing.
- Category: Console / completion / ids / localisation
- Related: `docs/backlog/done/command-id-name-completion.md`, `docs/backlog/done/command-tree-resource-location-selector.md`,
  `docs/backlog/done/console-completion-shows-no-suggestions.md`, `docs/backlog/review/pinyin-search-standalone-mod.md`,
  `docs/backlog/todo/the-command-console-is-a-contract.md` (the definition contract these five defects will be shaped by).

## Design

`docs/design/surfaces/command-overlay.md` owns the console's player-facing surface and `docs/design/cross-cutting/localization.md` the rule that a sentence a
player reads is a key plus its arguments. The console's completion is the one place where "what the player sees" and "what the command accepts" are the same
fact, so this ticket is judged on frames: a candidate list that differs from what Enter/Tab take is a lie on screen, not a cosmetic flaw.

**What Minecraft's command system does, as the standard to learn from** (the user's direction: "Minecraft's system has a lot of detail; learn it"):

- **The list is the authority.** Every keystroke recomputes the candidate set for the token under the cursor, and Tab, the mouse and ↑/↓ all move inside
  that exact set. A candidate never accepted by Tab, or a completion Tab reaches that the list does not show, is the defect this ticket exists for.
- **↑/↓ belong to the list while it is open.** They move the highlight (and the field previews the highlighted entry); they reach the history only when the
  list is closed. Ctrl+↑/↓ is the history shortcut in some builds, never a silent fallback.
- **A resolved argument continues the grammar.** After an item id, the parser expects the optional component/SNBT block and completion offers its skeleton
  (`{…}` with the keys that item accepts), then the next argument, and so on.
- **Ids are namespaced everywhere** (`namespace:path`), with the default namespace allowed to be omitted on INPUT and never on OUTPUT: the list shows
  `cu:waterbottle`, the parser accepts both, and the canonical form is what a copy/insert gives.
- **A suggestion row carries what identifies it**: the id, the human-readable name in the reader's language, and the match's reason (pinyin, alias). A bare
  internal name in the list is unreadable to the player who typed a Chinese transliteration.

## What exists today (read, not remembered)

| Piece | Where | What it does now |
|---|---|---|
| the rendered list | `ConsoleInputSession.CompletionSuggestions` | the candidates captured on the FIRST Tab (`CompletionSuggestions`), redrawn until the token changes |
| Tab | `ConsoleInputSession.CycleCompletion` | **recomputed every press** (`_completion.Suggest(input)`), then walks its own index over that fresh list |
| ↑/↓ | `CommandConsoleOverlay` (`case KeyCode.UpArrow: _session.PreviousHistory()`) | always the command history — there is no branch for a visible list |
| item ids | `CommandConsoleCompletion.TakesTheGamesOwnId` | a give/summon offers `entry.Id.Path` (the bare `waterbottle`) on purpose, because the game's own table refuses a namespaced id (measured 2026-10-25) |
| SNBT state | `CommandConsoleCompletion` (`ItemDataVerbParser.IsWriteVerb(tokens[1])`) | offered for `/data`'s write verbs only; nothing after `/give … <id>` |
| row content | `SuggestResourceLocations` | `new CommandSuggestion(bare-or-canonical, Hint.ResourceLocation, Args: [entry.Kind, entry.DisplayName])` |
| ranking | `ResourceLocationCatalog.Suggest` | 0 exact canonical, 1 canonical prefix, 2 bare path prefix, 3 display-name prefix, then mod stages; capped at `MaxSuggestions = 20` |

## The five defects, each with what the player saw

1. **The list omits a candidate Tab accepts.** Typing `/give @s water` shows only `waterbottle` in the list, and pressing Tab walks on to `waterjug`. The two
   paths disagree by construction: the list is captured on the first Tab while `CycleCompletion` recomputes a fresh ranked list every press, so the index
   walks a set the screen never showed.
2. **↑/↓ move the history while a list is on screen.** `CommandConsoleOverlay` routes both arrows to `PreviousHistory`/`NextHistory` unconditionally.
3. **Nothing after a resolved id.** With the cursor right after `waterbottle` the grammar expects the component/SNBT block and completion offers nothing:
   `ItemStateCompletion` is wired only into `/data`'s write verbs.
4. **The output form is the bare id.** `/give` suggests `waterbottle`, never `cu:waterbottle`. This is deliberate today (`TakesTheGamesOwnId`): the game's
   table refuses the canonical form, so offering it would suggest a line the command refuses. The user's direction is the canonical form, which makes this a
   COMMAND defect first — `/give` and `/summon` must accept `namespace:path` and resolve it to the game's id — and a completion change second.
5. **A pinyin hit is a bare name.** Typing `shuiguan` lists `waterjug` with no namespace and no Chinese label, so the row that the match came from does not
   say what the player would get. The row already carries `Args: [entry.Kind, entry.DisplayName]`; either the renderer drops them for this hint or the
   entry's display name is empty — the first read of the fix is which of the two.

## The shape this ticket proposes

- **One candidate computation per keystroke**, owned by `ConsoleInputSession`, consumed by the renderer, Tab, the mouse and ↑/↓ — the list and the cycle are
  the same object, and a test drives `CycleCompletion` over `CompletionSuggestions` and asserts they never diverge.
- **Arrow routing depends on the list**: while `LiveSuggestions` is non-empty, ↑/↓ move a highlight and the field previews it; Escape closes the list; Enter
  accepts. History stays on the same keys only while the list is closed.
- **Id rows carry id, label and reason**: the canonical id as the value to insert, the reader's-language name, and the matched alias/pinyin as the hint. The
  canonical form is what Tab inserts; a bare path stays ACCEPTED on input (the game's own form keeps working) and is resolved to the canonical id.
- **A resolved id continues**: after an item id the completion offers the component/SNBT skeleton for that item (the keys the state language knows), which is
  the same `ItemStateCompletion` surface `/data` already uses, asked for the item the cursor names.
- **Namespaced verbs**: `/give` and `/summon` resolve `namespace:path` to the game's own id before they touch the game's table, so the canonical form is a
  line that WORKS. The refusal text for an unknown namespace says which half failed (namespace vs path), by key.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | Typing `/give @s water` shows every candidate Tab can reach, in the same order, judged from a frame and from a list dump taken in the same frame |
| 2 | With that list open, ↑/↓ move the highlight and the field previews the highlighted entry; with the list closed (Escape) the same keys walk the history — two frames |
| 3 | With the cursor right after a completed item id, the list offers the component/SNBT skeleton for that item, and accepting it gives a parseable line — frame plus the parse result |
| 4 | The same id is inserted as `cu:waterbottle`; typing the bare `waterbottle` still runs, and `/give @s cu:waterbottle` lands the item — a frame plus the item's row |
| 5 | Typing a Chinese transliteration shows the matching id WITH its namespace and its Chinese name in the row — frame |
| 6 | A completion the player cannot run is still hidden (the 2026-10-24 rule), and the gate that pins it keeps passing |
| 7 | A token the grammar refuses is marked IN PLACE with a hint of what was expected (a frame with a wrong argument typed), and the hint names the parameter rather than restating the line |
| 8 | A candidate row can be CLICKED to complete the token and HOVERED to preview its full usage; the frame shows the preview, and the click lands the same text Tab would |

## Limits

- This ticket does not redesign the definition contract (`the-command-console-is-a-contract.md` owns that); it states what completion must do once a command
  can express its parameters, and each row above must survive that migration.
- The command list is capped at `MaxSuggestions = 20` today: the cap stays, but it applies to the ONE list both paths share, and the frame must say when it
  truncated (a count, not a silent cut).
- Selector completion (`@a`, filters) is judged by `command-tree-resource-location-selector.md`; this ticket only requires that its rows and the item rows use
  the same list.
- A mod's own completer may offer anything; the standard here is the framework's own sources, plus the contract the mod surface gets from the console ticket.
- The two interaction rows (7 and 8) assume the list is a real input surface; until the definition contract lands they are judged against the framework's own`n  sources only, and a mod's custom parser inherits them from the same surface.
