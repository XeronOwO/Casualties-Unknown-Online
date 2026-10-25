# The console's completion shows nothing while a command is typed

- Status: Todo — opened 2026-10-25 from the user's report: "你这命令行补全了个啥,我输命令没有看到补全提示呀".
- Priority: High
- Category: Console / completion / Online UI
- Source: user report (2026-10-25).

## Goal

While the player types a command in the game console, a completion list appears (the resource-id and console-command suggestions the
seam was built for), updates as the text changes, and the accepted suggestion is the canonical id.

## The design record this ticket is judged against (it already exists)

The console is **CUO's own Minecraft-style overlay**, designed and accepted in
`docs/backlog/done/in-game-command-console-interactive.md` (category: *Tooling / UI / Minecraft-style command console*; its eleven
derived rows passed in batch `20261018-t`, record `docs/evidence/acceptance/in-game-command-console-interactive-20261018-t.md`).
That design ticket's rejection list is this ticket's specification, quoted: "*Pressing `/` did not pop up the command suggestion
list / autocomplete in the way Minecraft does. The user expects Minecraft-style command suggestions and completion*", and the redo
landed `ConsoleInputSession.LiveSuggestions` ("always reflects the current input, so pressing `/` immediately shows command
candidates and typing narrows them without requiring Tab") plus `CommandConsoleOverlay.DrawSuggestions`. Decision 228 keeps the
in-game `/` overlay as the only command and chat surface.

So this ticket is a **regression against an accepted design**, not a new feature: the sources above exist in the tree, and the
question is why the player sees none of it.

## Current behaviour (read on the player's own surface, 2026-10-25 batch `batch-console-ui` / `batch-console-ui2`)

The first batch of 2026-10-25 opened the GAME's native console and judged the wrong surface. This cycle added the capability that
was missing — `drive-in-process.ps1 -Action console-ui`, which calls `OnlineUiOverlay.OpenCommandConsole` (the `/` key's own path),
types into the overlay's own `ConsoleInputSession` and reports `LiveSuggestions` — and then `-Action console-accept`, which applies a
suggestion the way a click (`AcceptSuggestion`) and the Tab binding (`CycleCompletion`) do.

**What that read, on the pre-fix build (`7138fef0`):**

```
[/]        -> 12 :: ban|clear|data|deop|gamemode|gamerule|give|heal|help|kick|op|players
[/gi]      ->  1 :: give
[/heal ]   ->  0            <- the reported state: the list vanishes at the argument
[/give ]   ->  0
[/gamemode ] -> 0
```

So the suggestions were never absent at the verb — they disappeared the instant the player typed the space after it, which is exactly
"我输命令没有看到补全提示呀". Two causes, fixed in `a0600135`:

1. The selector source treated an EMPTY prefix as "not a selector" and returned nothing, when an empty prefix is the request for the
   choices (the state the player is in right after the verb).
2. `/give` and `/summon` declared their arguments as free `Text`, so the engine had no vocabulary to offer even though the parsers
   accept a selector, a content id, a position and a state (`ItemGiveParser` reads `@`-prefixed tokens as the selector and the rest as
   the id). They now declare `Selector` + `ResourceLocation` and `ResourceLocation` + `Text`.

**After the fix, same probes, same build the clients ran (`a0600135`):**

```
[/]              -> 12 :: ban|clear|data|deop|gamemode|gamerule|give|heal|help|kick|op|players
[/heal ]         ->  5 :: @a|@p|@s|@e|@r
[/give ]         ->  5 :: @a|@p|@s|@e|@r
[/summon ]       -> 12 :: cu:12gauge|cu:556round|cu:9mmround|cu:adhesivebandage|cu:aed|…
[/give @p ]      -> 12 :: cu:12gauge|… (the content catalog's own entries)
[/give @p band]  ->  2 :: cu:bandage|cu:bandolier
[/gamemode ]     ->  0            <- still empty: a closed word set the family has no kind for
```

and both accept paths insert the canonical id: the button (`accepted=cu:bandage`) and Tab (`accepted=tab`) both leave
`/give @p cu:bandage` in the field, `/heal @` accepts `@a`.

Frames (read, not merely captured): `host-console-slash.png` (12 commands listed above the input), `host-console-gi.png` (`/gi` → one
`give` row), `host-argument-band.png` (`/give @p band` → `cu:bandage` "item · 绷带" and `cu:bandolier` "item · 子弹带", drawn above the
input line, the list visibly shorter than the verb list).

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | Typing a prefix shows a suggestion list on the screen (a frame), and the list changes as the text changes | **passes** — `/` → 12 commands, `/gi` → `give`, `/give @p band` → 2 content ids, each read from its frame. The row's "satellite pinyin mod" is not needed for this: the game's own content names arrive localized in the catalog |
| 2 | Without the mod, typing a name the console knows still shows its own command suggestions (a frame), or the ticket records that the vanilla console has none | **passes** — the host ran plain CUO with no satellite mod and listed every command and every content id |
| 3 | Accepting a suggestion (Tab/Enter, in the game's own binding) inserts the canonical id — read from the input field's text and from the executed command's log line | **passes for the field text** through both bindings (`AcceptSuggestion` from the row's button, `CycleCompletion` from Tab); the executed line's log is the same string (the field is what `Submit` sends), read on the next submit |
| 4 | An argument with a closed word set offers it: `/gamemode ` lists `survivor` and `spectator` | **open** — needs a choice kind the console family does not have yet; until then that one verb's argument is typed blind |

## What remains (the code this ticket still owes)

`/gamemode`'s two words. The console has argument kinds for a selector, a player, a steam id, a command name, JSON and a resource
location, and none for "one of these words" — so a verb whose argument is a closed set (`survivor|spectator`) can only be typed from
memory. The fix is a choice vocabulary declared on the command (a new `CommandArgumentKind` member with its values, or a
`CommandDefinition.Arguments` entry carrying them), which also serves any future verb of that shape; the `Abstractions` baseline and
its gate move with it.

## Notes on method

The defect is a "state true, screen empty" case, so every row above is read from a frame as well as from the probe: the probe alone
said `suggestions: 12` on the pre-fix build for `/`, and the player's complaint was about a moment the probe could not see.
