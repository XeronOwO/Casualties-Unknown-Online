# The console's completion shows nothing while a command is typed

- Status: Done — code landed 2026-10-25 (`a0600135`, `7b1f53da`, `84ea4c76`); every row judged on the host's own console in
  batches `batch-console-ui`, `batch-console-ui2` and `batch-console-ui4`.
- Priority: High
- Category: Console / completion / Online UI
- Source: user report (2026-10-25, translated): "what is this command-line completion of yours? I typed a command and saw no completion
  suggestion", extended the same day: "completion is not just the first token — everything later that has choices must complete too, and
  component snbt must complete as well; anything that can be completed should be considered".

## Goal

While the player types a command in the game console, a completion list appears, updates as the text changes, covers EVERY position on
the line that has a vocabulary — later arguments and component SNBT included — and the accepted suggestion is text the verb accepts.

## The design record this ticket is judged against (it already exists)

The console is **CUO's own Minecraft-style overlay**, designed and accepted in
`docs/backlog/done/in-game-command-console-interactive.md`; that design's rejection list is this ticket's specification, quoted:
"*Pressing `/` did not pop up the command suggestion list / autocomplete in the way Minecraft does. The user expects Minecraft-style
command suggestions and completion*", and the redo landed `ConsoleInputSession.LiveSuggestions` plus
`CommandConsoleOverlay.DrawSuggestions`. Decision 228 keeps the in-game `/` overlay as the only command and chat surface.

So this was a **regression against an accepted design**, and the work was to find where the list went.

## What the batches read (host console, frames read, not merely captured)

The capability the first attempt lacked — opening CUO's overlay — is now a driver action: `console-ui` calls
`OnlineUiOverlay.OpenCommandConsole` (the `/` key's own path), types into the overlay's own `ConsoleInputSession` and reports
`LiveSuggestions`; `console-accept` applies a suggestion the way the row's button (`AcceptSuggestion`) and Tab (`CycleCompletion`)
do.

**Pre-fix build (`7138fef0`):** the list was healthy at the verb and empty at every argument —

```
[/] -> 12 :: ban|clear|data|deop|gamemode|gamerule|give|heal|help|kick|op|players      [/heal ] -> 0     [/give ] -> 0
```

i.e. it vanished the instant the player typed the space after the verb, which is exactly the report. Two causes: the selector source
treated an EMPTY prefix as "not a selector" (an empty prefix is the request for the choices), and `/give`/`/summon` declared free
`Text` arguments so the engine had no vocabulary at all.

**After `a0600135`:** `/heal ` → 5 selectors, `/give ` → 5, `/summon ` → 12 content ids, `/give @p band` → 2, and both accept paths
insert the id (frame `host-argument-band.png`).

**After `7b1f53da` and `84ea4c76` (this cycle's extension):** every position with a vocabulary answers, and the id form is the one the
verb takes —

```
[/gamemode ]              -> 2 :: survivor|spectator
[/summon cu:aed ]         -> 3 :: cursor|player|random
[/summon item aed ]       -> 3 :: cursor|player|random
[/give @p cu:aed ]        -> 1 :: slot
[/give @p band]           -> 2 :: bandage|bandolier          (bare ids — see below)
[/give @p bandage{C]      -> 2 :: bandage{Condition:|bandage{Components:      (frame host-snbt.png)
[/data merge id <live> {] -> 4 :: {Condition:|{Favourited:|{Liquids:|{Components:
[/data set id <live> {Cond] -> 1 :: {Condition:
```

and the round trip lands: accepting `bandage{Condition:` leaves `/give @p bandage{Condition:` in the field, and completing it to
`/give @p bandage{Condition:0.5}` is answered `Gave bandage (#1164257118147) … with state {Condition:0.5f,…}`.

**The id-form defect the frames would not have shown, only the run did:** the catalog names content canonically
(`cu:bandage`) and the give/summon verbs resolve through the GAME's own item table, which is keyed by the bare id. Measured both ways in
the running client: `/give @p cu:bandage` → `Refused: No item definition 'cu:bandage' is known to this game`, `/give @p bandage` →
`Gave bandage (#…) in slot 0`, and `/summon item cu:aed` → refused while `/summon item aed` → `Summoned aed (#…)`. Accepting a
suggestion therefore produced a refused command, which is worse than no suggestion; the projection now follows the verb
(`give`/`summon` offer the catalog entry's bare path, the localized display name stays in the description, and every other
resource-location argument keeps the canonical form).

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | Typing a prefix shows a suggestion list on the screen (a frame), and the list changes as the text changes | **passes** — `/` (12 commands), `/gi` (`give`), `/give @p band` (2 item ids), each read from its frame |
| 2 | Without a mod, typing a name the console knows still shows its own command suggestions (a frame), or the ticket records that the vanilla console has none | **passes** — the host ran plain CUO with no satellite mod and listed every command and every content id |
| 3 | Accepting a suggestion (Tab/Enter, in the game's own binding) inserts the canonical id — read from the input field's text and from the executed command's log line | **passes** — the button (`accepted=cu:bandage` at the time, now `bandage`) and Tab (`accepted=tab`) both leave the id in the field; the completed line's answer is read from the console |
| 4 | An argument with a closed word set offers it: `/gamemode ` lists `survivor` and `spectator` | **passes** — two rows, from `GamemodeCompletion` |
| 5 | Every LATER position with a vocabulary completes: `/summon`'s position words, `/give`'s `slot` keyword | **passes** — `cursor|player|random` for both summon forms, `slot` for a give after its item |
| 6 | Component SNBT completes wherever an item state can be typed: `/give <item>{…}`, `/summon item <id>{…}`, `/data merge|set <target> {…}` | **passes** — the frames show `{Condition:`/`{Components:` and the live-item probes show the root's four keys for both merge and set |
| 7 | A suggestion is text the verb ACCEPTS: the item-id and definition-id positions offer the bare game id | **passes** — both forms executed against the verb in the running client, and the refusal of the namespaced form is quoted above |

## Limits (recorded, not hidden)

- **Numbers are not completed.** Neither the give slot nor a summon coordinate bounds its range, and inventing one would offer values
  the verb may refuse; `HostRuleCompletion` takes the same position for a numeric rule.
- **An entity/animal prefab's SNBT is not completed.** Its state is the game's own and this build has no schema for it; the source
  answers nothing there rather than offering item keys the prefab would refuse. `/summon item <id>{…}` completes because a loose item
  entity carries that item's own component schema.
- **`/gamemode`'s words live in a name-keyed source**, like `HostRuleCompletion`, not in an argument kind. A generic "one of these
  words" kind on `CommandSpec` is the mod-facing shape when a mod needs it, and it is not built for one verb.
- **The verbs still accept only the bare definition id.** The `cu:` namespace is CUO's resource vocabulary, not the game's; a player
  who types the canonical form is refused by name, which the completion now avoids by suggesting the accepted form instead.

## Notes on method

This is a "state true, screen empty" defect, so every row above is read from a frame as well as from the probe: the probe alone
reported `suggestions: 12` on the pre-fix build for `/`, while the player's complaint was about a moment it could not see.
