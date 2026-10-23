# The console completes only what you may run

- Status: Todo — design agreed with the user on 2026-10-24; not implemented.
- Priority: Medium-High
- Category: Console / completion / permissions
- Source: the user's 2026-10-24 ruling: in Minecraft a player without cheat permission cannot complete a
  cheat command's WORD, let alone its second or third argument — no completion at all. With the authority,
  `/gamerule` completes its rule names and that rule's values.
- Related: `todo/gamerule-joins-the-op-family.md` (the same ruling's authority half),
  `done/gamerule-host-rule-command.md` (records "no completion" as a limit this ticket removes),
  `review/cheat-permission-and-op-commands.md` (the family), `done/in-game-command-console-interactive.md`
  and `docs/evidence/selfchecks/ui/command-console-selfcheck.md` (the console's completion design),
  decisions 263/265/269.

## Problem

The console's suggestion source offers every command name matching the prefix and then completes by
argument kind, with no authority check: a member sees `/kick`, `/ban`, `/give`, `/gamerule` in the list and
learns it may not run them only after pressing Enter. `/gamerule` additionally declares two plain `Text`
arguments, so it completes nothing at all — neither its rule names nor their values — which is the limit
`done/gamerule-host-rule-command.md` records.

## The design

**One authority predicate, two surfaces.** The suggestion source asks the same question the handler asks,
through a QUIET query on `CheatVerbGate` (a "may this process run this verb" that writes no log; the
refusal path keeps its one line). The predicate is the cheat family's two rulings — the host's switch and
its `/op` roster — with the host passing without either, exactly as the gate decides. One source: the
suggestion filter and the command's own gate must never disagree.

**What is hidden.** For a caller without the authority, the command WORD is not offered: the `HostOnly`
commands (`/gamerule`, `/kick`, `/ban`, `/unban`, `/op`, `/deop`) and the cheat verbs that are
`Anyone`-at-the-console-but-gated (`/give`, `/summon`, `/heal`, `/traderreport`). Hiding is a courtesy, not
the enforcement: typing the line still reaches the handler and is refused by name the same way.

**`/data` keeps its word.** It is one command with read verbs open to everyone and write verbs gated; hiding
the word would hide the read half. Its WRITE verbs' argument completion is gated by the same predicate,
while `get` keeps completing. The per-verb decision belongs where the verb is classified, not in the
suggestion list.

**`/help` uses the same filter.** A command the caller may not run is not listed. `/help <hidden command>`
answers the same permission refusal the command itself would, rather than pretending it does not exist —
so a player never learns a command by help that the console then refuses as unknown.

**`/gamerule`'s own arguments** are a command-specific completion (the `/data` precedent), not a new
generic `CommandArgumentKind`:

- second argument: the census's rule names, prefix-filtered, in the census's order (each with a one-line
  description from the rule's current value, so a player sees `remote_backpack_access` and what it is set
  to);
- third argument: the chosen rule's accepted values — `true`/`false`, the two three-level lists, and NO
  candidates for `piggyback_weight_multiplier` (Minecraft does not complete numbers; the refusal names the
  range instead);
- every candidate a completion offers must be accepted by the same parser the handler runs — a test pins
  that, so a suggestion can never lead to a refusal.

**Mod commands.** A mod console command declares `CommandPermission`; the filter applies that declaration
only. A mod's own runtime gate is not knowable to the console — recorded as a limit, not modelled.

## Acceptance

| # | Row |
|---|---|
| 1 | A plain member's live suggestions for `/ga` (and `/k`, `/gi`) offer nothing for the authority commands, while the host's and the granted member's offer them |
| 2 | The same member's `/help` does not list them; `/help gamerule` answers the permission refusal, not "unknown command" |
| 3 | The host's `/gamerule ` completes the 16 rule names; `/gamerule remote_backpack_access ` completes `allow`, `unconscious_or_dead`, `deny`; `/gamerule native_binding_parity ` completes `allow`, `warn`, `require`; the weight rule offers no value |
| 4 | Every offered value is accepted by the command when entered |
| 5 | `/data` still offers its read verbs to a plain member, and its write verbs' completions only to an authorised caller |
| 6 | With the switch off (or the roster revoked) the granted member's suggestions drop back to the plain member's set |
| 7 | A hidden command typed directly is still refused by name (hiding is not the enforcement) |

The completion source is machine-readable (`ICommandCompletionSource.Suggest`), so the rows are judged from
the deployed client's own answer per role; one frame of the open suggestion list records the visual row.

## Limits

- A number rule offers no candidate value; the refusal states its range.
- A mod command's own runtime gate cannot be reflected; only its declared permission is.
- The filter must not log per keystroke: the quiet query is the only addition to `CheatVerbGate`, and its
  refusal path is unchanged.
