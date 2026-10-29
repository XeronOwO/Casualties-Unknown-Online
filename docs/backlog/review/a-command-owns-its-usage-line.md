# A command owns its usage line, and the "Usage:" label comes from the catalogue

- Status: Review — opened 2026-10-27 from the user's design note while the `/help <command>` translation landed.
- Priority: Medium — the sentence around it is translated now; the usage half still reads English on a Chinese client.
- Category: Console / localisation
- Related: [player-facing-text-localized-per-receiver](../review/player-facing-text-localized-per-receiver.md) — the row this completes.

## Goal

`/help <command>` reads entirely in the reader's language: the description, the label in front of the usage, and the usage text itself — with only
identifiers left as they are (a command name, a placeholder such as `<steamId>`). Today the description comes from the catalogue
(`console.cmd.<name>.desc`, derived from the name) but the answer is built as:

    $"/{command.Name} — {Describe(command.Name)}\nUsage: {command.Usage}"

so the word `Usage:` and the whole usage string are English no matter who reads them.

## Design

`docs/design/cross-cutting/localization.md` carries the rule this ticket finishes: a sentence a player reads is a catalogue KEY plus its arguments,
rendered by the reader's own table, and the console's `/help` answer is one of those sentences. The page's console section already says a keyed answer is
rendered generically; what it does not yet cover is the usage TEMPLATE, whose label (`Usage:`) is English text baked into the answer instead of a
catalogue entry.

Two halves, and the second is the one with a shape question.

1. **The label is a catalogue key** — `console.help.usage` = `Usage: {0}` (and the Chinese catalogue's own label sentence). That closes the English word with no design work.

   **Done 2026-10-28, differently and better**: the whole answer is one keyed template — `console.help.command` = `/{name} — {description}` + the
   usage line — so the label AND the layout travel as one catalogue sentence instead of a label spliced in front of an English usage string. The
   answer keeps its assembled text for the log and as the fallback a catalogue gap degrades to. **Verified on a captured frame 2026-10-28**: the console
   draws the Chinese catalogue's sentence for that key on a `Language = zh` client, label included, where the same answer read `Usage: /clear` before.

2. **The usage text belongs to the command.** The user's proposal is an `ICommandUsage`-style surface (`string Usage { get; }`) a command can
   implement, with the catalogue's `console.cmd.<name>.desc` (read: the command's own attribute text) as the fallback when it does not.

   **The shape question this ticket must answer first**: a command is not a TYPE today. `ConsoleCommandRegistry` collects `[ConsoleCommand]`-marked
   METHODS on the service — attribute + reflection over methods, which is what makes "add a command" a one-line change. An interface therefore has
   nothing to be implemented BY unless the registry's unit becomes a type. Two shapes are possible:

   | Shape | Cost | When it pays |
   |---|---|---|
   | an interface implemented by a per-command TYPE (the registry's unit becomes a class) | the registry, the attributes and every command move; a real refactor | a command needs computed usage, per-command state, or its own dependencies |
   | the attribute carries the usage KEY (+ the string as the log/fallback), and the console renders it | small: one attribute field, one catalogue entry per command, one test | every command's usage is static text, which is what all of them are today |

   Recommendation, stated so the choice is not re-litigated from scratch: take the attribute-key shape now (it covers every command in the tree) and
   keep the interface as the door to open the day a command's usage must be computed. The interface is not worth a registry refactor while nothing
   needs it; the key is worth it today because the label and the usage are the last English words on the surface.

## What the code says today

- `CommandConsoleService.Help` answers `$"/{command.Name} — {Describe(command.Name)}\nUsage: {command.Usage}"`; `Describe` asks
  `CommandDescriptions.Of`, which resolves `console.cmd.<name>.desc` and falls back to the attribute's own English description.
- `CommandDefinition`/`ConsoleCommandAttribute` carry `Usage` as an English string built for the log and for this answer.
- The catalogue census reads a MIDDLE-hole key space (`console.cmd.{name}.desc`), so a per-command usage key
  (`console.cmd.<name>.usage`) needs the same treatment — either the same space rule widened or the attribute's key read as a literal.
- `CommandDescriptionCatalogueTests` already proves every registered command has a description in both catalogues; the same test is the natural place
  to require the usage entry once it exists.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | `/help <command>` on a `Language = zh` client contains no English word except a command name, a placeholder or a rule id — judged from a captured frame, not from the answer's text |
| 2 | The same command on a `Language = en` client reads the English sentence, and the two frames name the same command |
| 3 | `Usage:` reaches the reader as a catalogue key (`console.help.usage`), proven by a second language rendering a different label |
| 4 | A command whose usage needs no custom text renders the catalogue entry; a test proves every registered command has one, so a new command cannot ship English |
| 5 | When the shape question is decided, the decision is recorded here with the reason, and the interface (or its absence) is named in the design page |

## Limits

- The usage text's syntax is not translatable: `<steamId|displayName>`, `@p`, `[limit=1]` and the command names stay as identifiers, and a frame that
  shows them is not a defect.
- This ticket does not touch the 42 raw answers the console gate budgets; it is the help surface only.
## Shape decided, and all five rows judged (2026-10-29 23:55, build `fabbfe10`)

**The shape question is answered: the catalogue key travels with the ATTRIBUTE'S COMMAND, and no command TYPE is introduced.** `CommandDescriptions.UsageOf` holds one
literal `console.cmd.<name>.usage` per registered command — the same shape the description already uses, chosen for the same reason: the catalogue census reads literals, and
the whole line is what a language may reword around its identifiers. The `ICommandUsage` interface the ticket proposed stays the door to open the day a command's usage must be
COMPUTED; nothing needs it today, and the registry refactor it would force buys nothing for a fixed line of syntax. The design page (`docs/design/surfaces/command-overlay.md`)
names the attribute-key shape and the interface as deferred.

Judged from two captured frames (host `Language = zh`, alt `Language = en`, same command `/data`):

| Row | Result |
|---|---|
| 1 | **PASS** — the zh frame shows the description and the label in Chinese, the usage line carrying the Chinese connective around the identifiers; the only Latin left is what the player types (`get`/`merge`/`slot`/`cursor`/`id`, `<target>`, `<snbt>`, `<selector>`) |
| 2 | **PASS** — the en frame reads `Usage: /data get <target> …` for the same command |
| 3 | **PASS** — one label from the catalogue, two languages: the zh frame draws the Chinese label the catalogue holds for `console.help.command`, the en frame draws its English one |
| 4 | **PASS** — `CommandDescriptionCatalogueTests.EveryRegisteredCommandHasItsUsageLineInBothLanguages` fails on any registered command without both entries, so a new command cannot ship English |
| 5 | **PASS** — this section records the decision and names it in the design page |

**What the frame also caught, and what it is not:** the zh frame's translated lead-in to the target shows the translation reaching the SENTENCE, while `<selector>`, `<n>` and `cursor` stay as typed.
