# Localization: one language per reader, whole sentences

Every string a player reads is rendered by the client that reads it, in that client's language, as one whole sentence.

## A console answer names its key, or it is read in the wrong language

`ConsoleAnswer` carries an optional catalogue key (`ConsoleAnswer.Refused(sentence, "key", args…)` / `Ok(…)`), and
`CommandConsoleService` renders that key through the reader's own catalogue, keeping the sentence as the log line and as the fallback a catalogue gap
degrades to. An answer built as a raw interpolated English sentence therefore reaches a Chinese client in English — measured 2026-10-27 on a Chinese host
whose panel read Chinese and whose console read English for `/gamerule`, `/give` and `/op` refusals.

The rule is the same one the panel labels follow: a sentence a player reads is a key plus its arguments. **The console's resolver is generic**: `ConsoleLineText.Resolve` looks up whatever key a line carries. It used to be a switch over the six `console.*` literals the overlay itself used, and every other keyed answer fell through to its English text — which is why the command conversions alone changed nothing on screen until the switch was removed (measured in a captured frame on 2026-10-27). A key the catalogue does not answer still falls back to the line's own sentence, so a missing entry can never blank a line.

The `/help <command>` answer is ONE keyed template (`console.help.command` = the name, the description and the usage line), so even the word in front of the usage comes from the reader's table; the assembled sentence stays as the log line and the fallback. The same is true of a command's own description, which is looked up the same way: `CommandDescriptions.Of` answers the catalogue's sentence for `console.cmd.<name>.desc` and falls back to the command's English description when the entry is missing, so `/help <command>` and the completion hint row say the same thing in the reader's language. The key space is understood by the catalogue census in its MIDDLE-hole form (`console.cmd.{name}.desc`), and `CommandDescriptionCatalogueTests` proves every registered command has both entries — the two halves that let the key be derived from the name instead of listed in a table.

A refusal the gate itself decides is a keyed answer too: `CheatVerbGate` builds `ConsoleAnswer.Refused(sentence, "cheat.refused.<ground>", args…)` per ground it can name (no session, switch closed, not on the roster), and `ConsoleCommandAuthority` hands that answer straight to the console — so the sentence a player meets before anything is sent is in their own language, while the log line keeps the English text.

**A refusal is a key where its REASON is known, not only where the answer is printed.** The item-creation port is the worked example: the reason a definition cannot be created lives in the Game Adapter, which reads the game's own item table, so `ItemDefinitionTable` answers a `GrantRefusal` (sentence + key + arguments) and `IItemGrantSource` carries it to the verb. Without that, the console could only print the host's English on a Chinese client or replace it with the generic `answer.refused_see_host_log` line, and the player who typed `/give` learned nothing either way (measured 2026-10-28). Two verbs answer that shape today: `/give` and `/summon`, the latter with each ground built in `WorldSummoner` at the branch that decided it.

**A verb whose answer is more than one fact answers in PARTS.** An item verb reports the rung that settled the item AND the state the item really carries; one sentence cannot hold both without an English clause stranded inside another language. `PlayerItemGiveService.Give` and `WorldSummonService.Summon` therefore return `ConsoleAnswer.Ok/Refused` with one `ConsoleAnswerPart` per fact, and the console prints one keyed line each.

Two gates keep that honest. `ConsoleAnswerLocalizationGateTests` counts only the answers that carry **no** key, and its budget only falls — the earlier figure of 42 was never a progress number, because the call was counted whether or not it carried a key. `LocalizationCatalogueGateTests` counts a key as read when an ANSWER (`ConsoleAnswer.Refused/Ok`), a KEYED REFUSAL (`new GrantRefusal(…)`) or a keyed console line names it, so a keyed answer whose key nothing resolves cannot hide.
## The rule

- A player-visible string is never prose that crosses a boundary. What crosses is a **key** plus the values the sentence
  needs; the receiving client's catalogue turns the pair into a sentence.
- A localized frame never wraps another language's prose: a sentence in English inside a Chinese frame, or the reverse,
  is the defect this rule forbids.
- A surface with no catalogue entry is not shipped with raw text: a missing entry means the design is not finished, and
  the catalogue gate refuses the commit while one catalogue lacks a key the other has.
- **The product's own name is localized.** The window title uses the game's official name in the reading language: the accepted reading is that "the Chinese title reads … ; the English one is unchanged", so a product name is a catalogue entry rather than a hard-coded string.- The same sentence is never written twice: one condition has one key, shared by the client that decided it and the
  client that reads it.

## Where the catalogue comes from

The UI's text is an embedded JSON data file per language with English as the fallback; a strict loader reads it, the gates read the data rather than the code, and a mod adds a language through `RegisterCatalog` — the accepted record's words: "the UI's text is an embedded JSON data file per language (English as the fallback), the catalogue is read by a strict loader, the gates read the data, a mod registers a language through `RegisterCatalog` and the picker lists it". The deployed client's own service was read afterwards: the host answered `zh` with its own word for the page and the guest answered the same key in English.

**Three steps resolve a key, in order, and a mod's language is a gap-filler.** `Runtime/Localization/LocalizationService.cs`:
"lookup order: current language table → English fallback → the key itself", with the active language read through
`IOptionsMonitor` so "a BepInEx config edit hot-reloads immediately", and a mod's registered language is "a gap-filling table
over the built-in catalogue" (codes lowercased before use). Showing the key itself is the last resort, and it is a visible
failure rather than a silent one.

## Where strings live

| Kind of string | Where it is rendered |
|---|---|
| console answers, refusals and status lines | the reading client, from its own catalogue |
| panel titles, labels, buttons | the client drawing the surface |
| command semantics and their arguments | the console surface's own vocabulary |
| log lines | the host's log is a diagnostic channel, always in one language, and never a player-facing surface |

## The console's own prose

The console builds sentences of its own, and they follow the same rule. The accepted record lists the keys it now carries — `console.ready`, `console.session_ended`, `console.command.host_only`, `console.command.empty`, `console.command.unknown`, `console.chat.not_sent` — and its live reading is that the host's console buffer holds a line keyed `console.ready` while its service answers Chinese and the guest's the same key with English, with the launcher drawing the Chinese launcher label on one client and `CUO ONLINE` on the other. A keyed line is resolved through the same catalogue read the gate counts, and a pin fails the build if a catalogued console key is never resolved.

**An answer carries both, and the console prefers the key.** `Abstractions/ConsoleAnswer.cs`: an answer "may also carry the catalogue KEY and arguments for its sentence. The text stays on t[he fallback language so] any client without the entry [can] read it, and the console renders the key i[tself] when it has one. A handler that knows its reason from a closed set (the cheat gate's grounds, …) passes a key; a sentence assembled from free data does not." That is the design's own line between a reason that must be localised and a sentence that cannot be.

## Who judges

The side that produced a sentence renders nothing; the side that READS it renders it. So a refusal is judged where the action landed, and its sentence is composed by the client that displays it.

## What travels instead of text

- A refusal or an answer travels as a key plus at most the values its sentence names.
- A name the player typed (a persona, a world name) travels as text and is shown as it was written; it is never
  translated.
- A formatted number, a distance or an amount travels as a number and is formatted by the reader.

## How a player experiences it

The same action reads naturally in both languages: the operator's screen, the target's screen and the host's console each
show their own language for the same event, and no screen mixes the two.

## Records

`../../decisions/active.md` (the console's answer family), `../../architecture/surfaces.md` (status lines row), and the
catalogue gate named in `../../evidence/normative-gates.md`.

## Gaps

- The refusal sentences a cross-player use produces still travel as prose on one path; recorded in
  `../../backlog/review/player-facing-text-localized-per-receiver.md`.
