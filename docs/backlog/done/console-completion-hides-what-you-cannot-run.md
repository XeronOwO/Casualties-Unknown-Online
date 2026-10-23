# The console completes only what you may run

- Status: Done — implemented and accepted in batch `20261024-c`
  (`docs/evidence/acceptance/console-completion-hides-what-you-cannot-run-20261024-c.md`). The design was
  agreed with the user on 2026-10-24.
- Priority: Medium-High
- Category: Console / completion / permissions
- Source: the user's 2026-10-24 ruling: in Minecraft a player without cheat permission cannot complete a
  cheat command's WORD, let alone its second or third argument — no completion at all. With the authority,
  `/gamerule` completes its rule names and that rule's values.
- Related: `done/gamerule-joins-the-op-family.md` (the same ruling's authority half),
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
commands (`/kick`, `/ban`, `/unban`, `/op`, `/deop`) and the cheat verbs that are
`Anyone`-at-the-console-but-gated (`/gamerule`, `/give`, `/summon`, `/heal`, `/traderreport`). Hiding is a courtesy, not
the enforcement: typing the line still reaches the handler and is refused by name the same way.

**`/data` keeps its word.** It is one command with read verbs open to everyone and write verbs gated; hiding
the word would hide the read half. Its WRITE verbs' argument completion is gated by the same predicate, and
its verb word completes `get` for everyone (the verb list itself is added here — `/data` had no verb
completion before this change). The per-verb decision belongs where the verb is classified, not in the
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

## Implemented (2026-10-24)

- **One authority predicate, three surfaces.** `ConsoleCommandAuthority` answers "may this caller run this
  command" from the declared `CommandPermission` (host-only) and the declared `ConsoleCommandGate.Cheat`
  (the family's gate, through `CheatVerbGate`'s QUIET form). The completion filter, the `/help` list and
  `/help <command>` all ask it; the dispatcher keeps its host-only pre-check and the cheat gate stays in
  each handler, so hiding is never the enforcement.
- **The classification lives at the verb.** `ConsoleCommandGate` is declared on `[ConsoleCommand]` and
  carried by `CommandDefinition`/`CommandSpec`; `/give`, `/summon`, `/heal`, `/gamerule` and
  `/traderreport` are marked `Cheat`. A hidden command's word, its argument completions and its help row
  are withheld together, while typing the line still reaches the handler and is refused by name. What is
  NOT withheld is the handler's own grammar refusal: a typed `/gamerule bogus` still lists the rule names
  (the grammar runs before the gate, decision 269 keeps the refusal naming the set), which the Limits say.
- **`/help` uses the same filter**: the list omits what the caller may not run, and `/help <hidden>` answers
  the refusal the command would meet (the host-only sentence or the gate's own), never "unknown command".
  `CommandName` completion (the kind `/help` takes) is filtered too, so the word is not leaked by Tab.
- **`CheatVerbGate` has one decision and two forms**: `Judge` returns the ground and the sentence without
  logging; `TryRefuse` logs its one line at the ground's level and returns the same sentence, while
  `MayRun`/`TryRefuseQuiet` are the completion/help forms that write nothing. The handler's behaviour and
  its log lines are unchanged.
- **`/gamerule`'s own vocabulary** is `HostRuleCompletion`, the command-specific source beside the
  `/data` precedent: the second argument completes the census's rule names in census order, each with the
  value in force as its description; the third completes that rule's `HostRuleValueText.Choices` (the two
  three-way lists; a numeric rule has none). Every candidate comes from the parser's own source, so an
  offered value is accepted by the command (pinned by a test that feeds every suggestion back through
  `HostRuleValueText.TryParse`).
- **`/data` keeps its word and gets its verb completion**: `ItemDataVerbCompletion` offers `get` to
  everyone and `merge`/`modify`/`remove` only when the gate admits the caller; the per-verb decision is
  `ItemDataVerbParser.IsWriteVerb`, which reads the same verb constants `ItemDataCommands` dispatches on
  (a new verb added to one side must be added to the other — the constants are the one source, the two
  call sites are not merged), and the write verbs' SNBT state completion is suppressed for a caller the
  gate refuses.
- **The completion side is its own collaborator.** `CommandConsoleCompletion` now owns the suggestion and
  hint surfaces (the service was at the 600-line aggregate gate); `CommandConsoleService` delegates and
  keeps dispatch, answers and its own commands.
- Tests: `CommandConsolePermissionCompletionTests` (17 cases: no authority word for a plain member with
  positive controls; the host's and granted member's sets; the switch closing and the roster not naming the
  caller each dropping the member's completions; `/help`'s list and both refusal grounds; `/help`'s own
  completion; the 16 rule names in census order with values; each rule's exact value list; every offered
  value accepted; `/data`'s read/write halves and the write-state SNBT lock; the public command-metadata
  projection filtered by the same authority; no log from a suggestion; and a hidden command typed directly
  still refused). The new committed
  recipe `tools/acceptance/recipes/console-suggest.cs` exposes `Suggest`/`GetHint` per client for the
  acceptance rows (its `trail=space` argument appends the trailing space a fresh argument position needs,
  because the driver trims a `-RecipeArg` pair), and `AcceptanceDriverGateTests` passes on it (C# 7,
  declared args, no OS input).

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
the deployed client's own answer per role; one frame of the open suggestion list records the visual row. The
batch judges row 4 by ENTERING every offered value on the host (each answer must name the value in force) and
row 6 by flipping the switch off and revoking the roster; the recipe's `trail=space` carries the
fresh-argument inputs, because the driver trims a `-RecipeArg` pair.

## Accepted (batch `20261024-c`, 2026-10-24)

All seven rows and the visual row passed on the deployed artifact
`0.1.0+77510106c0860116d68c3a48bae321a3f86dcbb6` with three clients. The record is
`docs/evidence/acceptance/console-completion-hides-what-you-cannot-run-20261024-c.md`; the frame is
`r8-alt-suggestions.png` (the plain member's open list: `clear, data, help, players, rtt` and the three
below the fold, no hidden verb). The recipe's `trail=space` was smoke-tested before the rows (16 rule names
with their values), and every rule value the completion offered was then entered and accepted.

## Limits

- A number rule offers no candidate value; the refusal states its range.
- A mod command's own runtime gate cannot be reflected; only its declared permission is.
- The filter must not log per keystroke: the quiet query is the only addition to `CheatVerbGate`, and its
  refusal path is unchanged.
- The filter models the DECLARED permission and the cheat gate, not every refusal a handler can make. A
  built-in counterexample stays: `/save` is `CommandPermission.Anyone` (solo play needs it) and is offered
  to a guest, whose run answers "a guest never writes a world archive"; the same shape the mod-gate limit
  names. A command's own runtime policy is the handler's, never the console's.
- A hidden command typed in full still shows its grammar: `/gamerule bogus` lists the rule names and
  `/gamerule pvp_enabled z` names the accepted set, because the parse precedes the gate (the design's
  order, so an invalid value costs no round trip). What the filter withholds is the COMPLETION and the
  help row, not the handler's own refusal text.
- A member that has received NO announcement (a swallowed admission, a fresh member) has both local checks
  skipped by the gate's documented fallback, so its completion and help offer the cheat verbs and its
  typed line is admitted locally; the host's own refusal is what settles it. This is the same
  interception-not-verdict rule every cheat verb follows.
- `/help <hidden>`'s refusal is a handler return, so it renders in the console's success colour — the
  console-wide defect tracked as `todo/console-command-refusals-render-green.md`, whose instance list now
  names this surface.
- The host-only condition is tested twice by design: the dispatcher's pre-check (enforcement, before the
  handler) and `ConsoleCommandAuthority` (the filter). Both read the same `Permission` declaration; a
  future third caller must use the authority rather than copy the test again.

## Independent review (2026-10-24) — every finding and what changed

Verdict: 0 blocker, 1 major, 9 minor, 6 nit. The reviewer reproduced the build, the focused suite (15 at
the time), the full suite (6335) and the gate suite (776/777, the one red being the unchecked final
checklist box), and traced every runtime path without finding a behavioural defect. Every finding is
dispositioned below; all fixed ones landed in this commit.

### MAJOR

- **M1 — the committed recipe could not carry a trailing space, so the planned `/gamerule ` and `/data `
  inputs would have been mis-driven.** The driver trims each `-RecipeArg` pair before splitting it
  (`drive-in-process.ps1`, `$pair = $pair.Trim()`), so `input=/gamerule ` arrives as `/gamerule` and the
  recipe answers the command word instead of the rule names. **Fixed:** the recipe takes `trail=space`
  (appends one space) or `trail=none`, its header says why, every acceptance call passes it, and the
  checklist's L1 plan names it; the batch smokes the recipe live before its rows are judged.

### MINOR

- **m2 — two Structure-review line counts did not reproduce.** The checklist said 502/285; the frozen tree
  measures 507/299 (the reviewer even showed what each figure had left out). **Fixed by re-measurement**
  after this cycle's additions: the checklist now carries the file counts measured on the final tree.
- **m3 — `ConsoleCommandRegistry.ToSpecs()` became dead** while the checklist claimed no dead mechanism.
  **Fixed:** deleted; the filtered projection builds its specs from `All` directly.
- **m4 — the `/data` write-state SNBT lock had no test.** **Fixed:** `DataStateCompletion_IsWithheld…`
  seeds the same kernel row on both clients, proves the host (and a granted member) gets the state
  suggestion while a caller the roster refuses gets nothing.
- **m5 — the rule-name ORDER and the boolean rules' value lists were under-pinned.** **Fixed:** the names
  are asserted as the exact census sequence, and every one of the 16 rules' `Suggest` output is compared
  with `HostRuleValueText.Choices` (true/false included).
- **m6 — `/help <hidden>`'s new refusal is green.** **Fixed by record:** `todo/console-command-refusals-render-green.md`
  now names `/help <hidden>` as an instance and its acceptance covers it; the colour itself is that
  ticket's work.
- **m7 — "word, arguments and help row all disappear" over-claimed.** The handler's grammar runs before the
  gate, so a typed `/gamerule bogus` still lists the rules. **Fixed:** the Implemented bullet says what is
  withheld (completion + help row) and the Limits name the refusal-text disclosure.
- **m8 — `/save` is a built-in counterexample the Limits did not name.** It is `Anyone` (solo play) and
  refuses a guest. **Fixed:** the Limits name it as the built-in analogue of the mod-runtime-gate limit.
- **m9 — the extended selfcheck carried stale evidence** (a 2335-case count, five deleted `tools/check-*.ps1`
  scripts, a `review/` ESC path now in `done/`, and `CommandConsoleServiceTests` names that class no
  longer exists). **Fixed:** the verification table now carries the current full-suite result and the gate
  command, the ESC link points at `done/`, and the stale class names are replaced with the real suites.
- **m10 — the L1 plan did not cover rows 4 and 6.** **Fixed:** the ticket's acceptance note and the
  checklist's L1 line now include entering every offered value and the switch-off/revoke steps.

### NITS

- **n11 — "the dispatch's own classification" overstated `IsWriteVerb`.** The dispatch switches on the same
  verb constants but never calls the helper. **Fixed:** the ticket, selfcheck row and the method's own doc
  say the constants are the one source, with two call sites to keep aligned.
- **n12 — presentation of the filtered metadata and the hint.** `ICommandCompletionSource.Commands` now
  documents that it is caller-filtered, `CommandSpec` documents the gate, and the `GetHint` comment no
  longer claims a player-visible surface (the input session exposes it; the overlay reads the live list).
- **n13 — `HostRuleCompletion`'s comment named the wrong caller.** **Fixed:** `CommandConsoleCompletion`.
- **n14 — the checklist carried 6334 and 6335.** **Fixed:** one current total (6335) plus its log, and the
  ruling is described as recorded rather than quoted.
- **n15 — the design text said `/data`'s `get` "keeps completing"** although `/data` had no verb completion
  before this change. **Fixed:** the design paragraph says the verb list is added here.
- **n16 — the host-only condition lives in the dispatcher and in the authority.** Kept deliberately (the
  dispatcher enforces before the handler; the filter asks the authority) and recorded in the Limits as a
  drift note rather than merged.

The review could not check the three-client acceptance (it had not run then), the in-game overlay's
rendering/Tab insertion, vanilla Minecraft's exact suggestion behaviour (the recorded user ruling is the
premise), or deployment.
