# A host rule is read and changed with `/gamerule`

- Status: Review — implemented 2026-10-23; awaiting the acceptance batch.
- Priority: Medium-High
- Category: Host rules / console command / native parity
- Source: the user's 2026-10-23 ruling — changing a host rule is a console command, shaped like
  Minecraft's `/gamerule`; the live syntax was fetched and cross-checked before this design was written
  (rule 17 of `AGENTS.md`).
- Related: `todo/give-gesture-own-ui-and-center-use.md` (its row 7 is `blocked` on a driveable rule switch
  and unblocks with this verb), decisions 263 (a host rule is the host's and reaches the members that read
  it) and 264 (a console command's shape follows Minecraft's current template, fetched not remembered),
  `docs/en/reference/configuration.md` (the `[HostRules]` and `[Respawn]` values this verb writes), and
  `todo/config-clamp-silently-widens-rule-levels.md` (the adjacent pre-existing clamp defect its review
  found).

## Problem

A host rule is changed today by two writers with two vocabularies: the Online UI's Admin page (a native
`TMP_Dropdown` per three-way rule, a checkbox per flag, a slider for the weight) and the console's
`/hostrules <json>` — a CUO-invented flat JSON object whose keys are the config entries
(`{"AllowLateJoin": false}`). Neither is a player's sentence: the JSON blob names the code's property, not
the rule, and the Admin page's dropdown options carry no control ids, so a rule cannot be switched by the
agent's in-process driver (the give gesture's row 7 was `blocked` on exactly this: the only state that
makes a displayed backpack view-only is `RemoteBackpackAccess=unconscious_or_dead`, and no command could set
it; batch `20261023-f` recorded the row `unproven` for that reason).

The user's ruling fixes the shape: a host rule is changed by a command, modelled on Minecraft's
`/gamerule`.

## The fetched shape (the citation this design answers to)

- `https://minecraft.wiki/w/Commands/gamerule` (fetched 2026-10-23): Java Edition syntax
  `gamerule <rule name> [<value>]`; permission level 2; the arguments are case-sensitive; `<rule name>` must
  be one of the predefined rules; `<value>` omitted queries the value instead of setting it; a supplied
  value is type-checked (boolean rules take `true`/`false`).
- `https://minecraft.wiki/w/Game_rule` (fetched 2026-10-23): a rule's allowed value depends on its type
  (boolean, or a bounded integer on current versions); 1.21.11 (25w44a) renamed every rule to a resource
  location in snake case (`advance_time`, `keep_inventory`, `max_entity_cramming`) and gave several rules a
  minimum and maximum.

## The design

**The verb.** `gamerule <rule name> [<value>]`, registered in the host-administration command group with
`CommandPermission.HostOnly`. The player's input sequence is the console line itself — `/` opens the
console, the line is typed, Enter runs it — so this introduces no new key, window or gesture.

**The rules are CUO's own set, named once.** The census (`HostRuleCensus`) already answers "one row per
rule"; it also becomes the rule table the verb reads:

- every row gains the rule's console `Name`: lowercase snake case, the rule's own spelling declared once
  in the census (e.g. `remote_backpack_access`, `piggyback_weight_multiplier`, `save_inventory` — the
  config key is `KeepInventory`, but the player sees "Save inventory" and the verb spells what the player
  reads; the names are not derived from the localised labels, which differ from them);
- the number row gains its accepted `Range` (0–3, the Admin slider's bounds), so the verb refuses a value
  the slider could not produce;
- `Find(name)` is an exact, case-sensitive lookup (JE's arguments are case-sensitive) and `Names` is the
  ordered list a refusal prints.

**Query — no value.** The answer reads the live rule set (`IHostRules`, which on the host is its own
config) and names the value in the same text the verb would accept back:
`Game rule remote_backpack_access is currently set to: allow.`
The value text is one new runtime helper (`HostRuleValueText`): `true`/`false` for a flag,
`allow`/`unconscious_or_dead`/`deny` and `allow`/`warn`/`require` for the two three-way rules through their
existing text forms, and the invariant decimal for the weight.

**Set — with a value.** The text is parsed by the rule's own kind before anything is written; only the
canonical form reaches the write seam. The write goes through `IHostRulesEditor`, now keyed by
`HostRuleId` and taking that canonical text, so the config entry is the same one the Admin page writes and
decision 263's change edge announces the new set to every member on the next pump. The answer names what
was set: `Game rule remote_backpack_access is now set to: unconscious_or_dead.`

**Refusals, each one line and by name.**

- an unknown rule: `Unknown game rule 'x'. Known rules: <the names, sorted>.` — the list is the discovery
  surface, because the console has no argument completion for these names (Limits).
- a value the rule's type refuses: `Game rule <name>: '<value>' is not one of <the allowed names>.` For
  the number rule the allowed set is its range: `'5' is not a number between 0 and 3.`
- a member: the console's own `Permission denied: this command is host-only.` (before the handler runs).

**`/hostrules` is deleted with its parser and applier.** Two verbs writing the same state in two
vocabularies is the drift the project's "one rule, one place" discipline refuses, and the JSON dialect is
not a player's words; the pre-release policy allows the breaking change. The write seam becomes
`IHostRulesEditor.TrySet(HostRuleId, canonical text)`, and the plugin's switch over the id names the rule in
its default arm; a rule added to the census without a config entry therefore fails loudly at the console
(the caught exception prints `Command error: host rule has no config entry`) instead of silently writing a
neighbouring entry. It does not fail the BUILD: C# does not check enum exhaustiveness once a discard arm
exists, and the plugin assembly is not part of the test project, so this mapping has no unit test (Limits).

## Deviations (each recorded with its reason)

Items 1–3 and 5 deviate from the fetched Java Edition shape; item 4 deviates from the rest of CUO's console
family.

1. **The one numeric rule takes a decimal, not an integer.** `piggyback_weight_multiplier` is a fraction
   (default 0.8) and its game fact is a multiplier, which an integer type cannot express; its accepted
   range is the 0–3 the Admin slider already enforces, and the refusal names that range.
2. **The permission is the host, not an operator roster.** Minecraft's level 2 is "an operator may"; CUO's
   rules are the host's own switches (decision 263) and this build has no member→host rule-write path, so
   the host is the only caller the verb can serve.
3. **The refusals list the known rules and the allowed values.** Minecraft's parser only says it cannot
   parse the argument; the console has no completion for rule names or values yet, so the refusal is the
   only place a player learns the vocabulary. The list is one line, printed only on a refusal.
4. **A surplus argument is refused with the usage line**, where the console's other verbs ignore extras.
   Minecraft's parser refuses it too, and a host-rule write is authority-bearing: a typo after the value
   must not succeed silently.
5. **Two rules take a three-way level name, not a boolean or an integer.** `remote_backpack_access` and
   `native_binding_parity` are CUO's own three-way facts, so their value is one of the named levels (listed
   on a refusal). The fetched Java Edition types are boolean and bounded integer; the *argument shape*
   (name, optional value, type-checked) is what this verb copies, and the level names are the rules' own
   existing text forms.

## What landed (2026-10-23)

- `HostRuleCensus`: every row carries the rule's console `Name`, and the numeric row its accepted `Range`;
  `Find` is the exact, case-sensitive lookup and `Names` is the ordered list a refusal prints.
- `HostRuleValueText` (new): the value grammar — the named choices derived from
  `RemoteBackpackAccessText`/`NativeBindingParityText`, the invariant number form, and the refusal text
  that names the accepted set.
- `IHostRulesEditor` now takes a `HostRuleId` and the canonical value; `HostRulesConfigEditor` maps each id
  to its `ConfigEntry` and re-reads the value through the same grammar before writing (the editor's
  `KeepSkills` property and the JSON writer that was its only caller are gone; the `[Respawn]` `KeepSkills`
  config entry itself stays and is still read by `RespawnOptions`).
- `HostAdminCommands`: `/gamerule <rule name> [<value>]` replaces `/hostrules <json>`;
  `HostRulesJsonParser`/`HostRulesJsonApplier` and their tests are deleted, and the Admin page's weight
  slider reads the census range instead of its own literals.
- The plugin's config metadata is bound from the same rows: `AcceptableValueRange<double>` for the weight
  and both `AcceptableValueList<string>` sets from `HostRuleValueText.Choices`, so a canonical write from
  the console cannot be clamped and the accepted sets cannot drift from the verb.
- Tests: `HostRuleValueTextTests`, the `/gamerule` matrix in `CommandConsoleCommandTests`, and the
  census name/range pins in `HostRuleCensusTests`.

## Acceptance rows

One two-client session against the deployed artifact; the host's console is driven in-process and the
member's announced set is read with the existing `host-rules-read` recipe. The member's `remote_backpack_access`
and `piggyback_weight_multiplier` values are the ones the member's own client is acting on.

| # | Row | Verdict |
|---|---|---|
| 1 | `/gamerule remote_backpack_access` on the host queries the value in force (`allow` on a fresh config) | — |
| 2 | `/gamerule remote_backpack_access unconscious_or_dead` sets it; the answer names the new value; a second query reads it; the member's announced rule becomes `unconscious_or_dead` on the next pump | — |
| 3 | `/gamerule remote_backpack_access maybe` is refused, names `allow`, `unconscious_or_dead` and `deny`, and the value stays | — |
| 4 | `/gamerule nope` is refused and prints the known rule names | — |
| 5 | A member's `/gamerule remote_backpack_access deny` answers the host-only refusal and the member's announced set is unchanged | — |
| 6 | `/gamerule piggyback_weight_multiplier 5` is refused with the 0–3 range; `/gamerule piggyback_weight_multiplier 0.5` sets it and the member reads 0.5 | — |
| 7 | The rules are restored (`allow`, `0.8`) and the member reads both back | — |

The same batch re-reads `give-gesture-own-ui-and-center-use` row 7 on the fixed artifact: the rule set by
this verb, the centre release of a view-only displayed backpack refused with its one line, and nothing moved.

## Limits (recorded, not claimed away)

- **No argument completion.** `/gamerule ` does not suggest rule names and a rule name does not suggest its
  values; the refusals (rows 3 and 4) are the discovery surface. Completion is a separate usability item.
- **One rule per command.** A bulk rule change is one console line per rule; Minecraft's verb is the same
  shape.
- **The query always reads the host's own set.** The verb is host-only, so its value is the host's config;
  a member reads the announced set through its Admin page and `host-rules-read`, not through this verb.
- **`pvp_enabled` and `auto_continue` are reserved rules** with no gameplay effect yet; the verb reads and
  writes them like every other rule, and their reservation is the config's own documented state.
- **Records keep their moment.** Acceptance records written before this ticket used `/hostrules`; they stay
  point-in-time and are not re-run with the new verb.
- **The plugin editor's canonical re-parse has no unit test.** The plugin assembly is not in the test
  project; the runtime grammar it calls is tested, and the real write path is exercised by the acceptance
  rows.
- **The config entry still clamps an unparseable file value to its list's first entry** (pre-existing, filed
  as `todo/config-clamp-silently-widens-rule-levels.md`): a hand-edited `Deny` or `Require` silently becomes
  `allow`, and the runtime parsers' conservative fallbacks are unreachable for file values. This change
  binds the lists from the census so they cannot drift from the verb, but does not change the clamp
  semantics.
- **A refusal line renders in the console's success colour** (pre-existing console-wide, filed as
  `todo/console-command-refusals-render-green.md`): the console adds every handler return as a success line,
  so `/gamerule`'s refusals are green; the text names the refusal, the kind does not.

## Independent review (2026-10-24) — every finding and what changed

An independent reviewer read the change set in a fresh context and attacked its claims. It found no blocker
in the change; the major findings were false claims in the documentation and one adjacent pre-existing
config defect. Every finding is recorded here with what changed.

- **MAJOR (process) — the tree was not frozen during the review.** The reviewer started at 23:34 and files
  changed after that (the alignment record, the admin drawer, the value helper and its tests, the
  checklist); it re-read the final state and re-ran the focused suites. **Fixed:** the final tree is frozen
  for the second review and no file changes while it runs; the alignment record is staged with the commit.
- **MAJOR — the configuration page's `/gamerule` sentence was false twice.** It called the name "the key in
  lowercase snake case" (but `KeepInventory` is addressed as `save_inventory`) and claimed the whole
  `[Respawn]` section (but `KeepSkills` is not a host rule). **Fixed** in both languages: the name is the
  rule's own snake-case name, the four `[Respawn]` rules that travel as host rules are listed, and an
  unknown name is sent to the refusal that lists them all.
- **MAJOR — the ticket claimed a compile-time switch guarantee that does not exist.** The plugin's switch
  has a discard arm, so a new rule compiles and fails at the console instead. **Fixed:** the design
  paragraph states the runtime refusal and the missing build-time check, and the limit is recorded.
- **MAJOR — live documents still described the deleted `/hostrules`.** `review/cheat-permission-and-op-commands.md`
  (the verb list and the stale-switch sentence), `review/mod-native-binding-handshake-parity.md` (the console
  command, plus a pre-existing dangling `docs/api/mod-api.md` path) and the console selfcheck's structure
  bullet still named the old verb or the deleted types as current. **Fixed in part:** the three pages
  point at `/gamerule` (with a value where an edit is meant) and at the current types. The ticket's
  `docs/api/mod-api.md` citations are NOT re-pointed here — the first review's note that this change fixed
  that path was wrong; the whole family (nine references in four live tickets, including
  `docs/api/advanced-modification-policy.md`) is filed as `todo/stale-api-doc-paths-in-live-tickets.md`.
- **MAJOR — the range and the closed sets were declared twice, and the config clamps.** The census declared
  them and `PluginDependencyRegistrar` re-declared them, so a drift could let the verb report a requested
  value while the entry held a clamped one. **Fixed:** the numeric range and both acceptable-value lists are
  bound from the census row and `HostRuleValueText.Choices`.
- **MAJOR (pre-existing, adjacent) — BepInEx clamps a hand-edited value to the list's first entry**, so the
  plugin's documented conservative fallbacks are unreachable and `Deny`/`Require` can silently become
  `allow`. Not introduced here; **filed** as `todo/config-clamp-silently-widens-rule-levels.md` with the
  decompiled evidence, and recorded as a limit above.
- **MINOR — the mod-api sentence named a query as an edit.** `/gamerule native_binding_parity` only queries;
  **fixed** to `/gamerule native_binding_parity <value>` in both languages.
- **MINOR — the register ticket misattributed the history.** The corpus audit left the register at 102,335
  bytes (≈12.3 KB headroom); 114,495 is later growth. **Fixed** in both the ticket and the index row, and
  the index row no longer offers "raise the budget" (the ticket rejects it).
- **MINOR — the `[HostRules]` table listed 9 of the 12 keys.** `SpectatorFreeFly`,
  `SpectateWhileUnconscious` and `SpectateWhileWaitingToJoin` were missing from both languages; **added**.
- **NIT — the census case-sensitivity assertion was weak.** `Find(row.Id.ToString())` differs by underscores
  for most rows; **fixed** by also asserting `Find(row.Name.ToUpperInvariant())` is null.
- **NIT — 0.1 and 1.2345 were not pinned.** **Added** to the canonical-number theory.
- **NIT — the checklist cited `HostRuleAnnouncementTests` for the announcement path.** The change edge is
  `HostRuleReplicationTests.AHostFlippingARule_ReachesTheMemberAtThatMoment`; **fixed** the citation.
- **NIT — "exactly four deviations" omitted the three-way value types.** **Added** as deviation 5, with the
  namespaced-spelling question left to the fetched citation's examples (bare names).
- **NIT — the command group's doc claimed a second host check behind every handler.** The rule write is
  local config and its gate is the console's; **fixed** the doc to say so.

The reviewer's judgement on the register budget: marking `docs/decisions/active.md` as `rework` with a
ticket is the gate's designed escape, not a workaround — but this change is what took the register over the
budget, so the split ticket is recorded as the register's next work item.

## Second independent review (2026-10-24) — every finding and what changed

A second reviewer read the FROZEN final tree (the first review's state had changed under it) and rebuilt the
solution: `dotnet build` 0 warnings / 0 errors including the plugin, `dotnet test --no-build` 777/777 gates
and 6300/6300 behaviour — the final log reproduced. No blocker; three major documentation claims and a list
of minor/nit inaccuracies, all fixed here in the same commit.

- **MAJOR — the `[Respawn]` intro claimed the whole section travels as host rules.** `KeepSkills` is not a
  host rule (no census row, not announced) and the page now contradicted its own new `/gamerule` sentence.
  **Fixed** in both languages: the four rules that travel are named and `KeepSkills` is stated host-local.
- **MAJOR — the first review's record said the dangling `docs/api/mod-api.md` path was re-pointed.** It was
  not; only the console-command sentence changed. **Fixed:** the record now says so and points at
  `todo/stale-api-doc-paths-in-live-tickets.md`.
- **MAJOR — the stale-path ticket's inventory was incomplete.** The real count is NINE references in FOUR live
  tickets, including `docs/api/advanced-modification-policy.md` and `docs/api/**` in
  `review/bilingual-human-docs.md`, and the mod-api citations are all §5 (not §5/§7). **Fixed:** the ticket
  lists all four tickets and both replacement pages, and its acceptance covers the live folders.
- **MINOR — the checklist still said "four deviations".** **Fixed** to five in both places, naming them.
- **MINOR — the checklist's and this ticket's "the clamp cannot disagree" over-claimed.** The true claim is
  narrower: a CANONICAL write cannot be clamped, and the accepted sets cannot drift. **Fixed** in the
  checklist, here, and in the plugin comment.
- **MINOR — the checklist's L2 claimed a log line per invocation.** The query logs at Debug. **Fixed:** L2
  covers the set/refusal paths; the query is read from the console answer.
- **MINOR — `HostAdminCommands` is 147 lines, not 144.** **Fixed.**
- **MINOR — the config text-form comments said the console read case-insensitively.** It matches ordinally;
  only the config does. **Fixed** in both text-form types.
- **MINOR — the fail-loud quote named a message the default arm does not produce.** The exception surfaces as
  `Command error: host rule has no config entry`. **Fixed.**
- **MINOR — this ticket called the give gesture's row 7 "unproven" while its ticket said `blocked`,** and that
  ticket still said no console verb writes the rule. **Fixed** here, and the give ticket's status/blocked-by
  now records the row as unblocked and waiting for the next batch.
- **MINOR — `/gamerule`'s refusals render in the console's success colour.** Pre-existing console-wide;
  **filed** as `todo/console-command-refusals-render-green.md` and recorded as a limit above.
- **NIT — `HostRuleId`'s doc still called the enum names the config keys.** **Fixed:** the console spelling is
  the census's, and a refusal names that.
- **NIT — the Admin drawer called two decimals the storage precision.** **Fixed:** display precision, and the
  console accepts the full form.
- **NIT — the census row doc over-claimed that the Admin page reads it.** **Fixed:** the member summary, the
  numeric bounds and the console read it; the host's rows stay explicit.
- **NIT — the ticket said the names derive from what the Admin page shows.** **Fixed:** they are the rule's
  own spelling, declared once in the census; the labels are localised and differ.
- **NIT — the index row's "193 bytes of headroom" was the pre-269 measurement.** **Fixed** to "over the
  budget since decision 269".
- **NIT — decision 269 cited 241 for the pre-release replacement.** 241 is the protocol-numbering policy;
  **fixed** to state the pre-release no-compatibility rule without the wrong number.
- **NIT — the deviations heading called all five "from the fetched shape".** Item 4 is a deviation from the
  console family; **fixed** (the heading and the item's note).
- **NIT — the clamp ticket's case example overstated `Allow`.** `Allow` clamps to the same rule; only
  `Warn`/`Require` widen. **Fixed.**
- **NIT — `HostRuleValueText`'s doc said all named choices are derived.** The flag's two names are its own;
  **fixed** to say the two three-way sets are derived.
- **NIT — the "KeepSkills property is gone" wording.** The `[Respawn]` config entry survives; only the
  editor property and its JSON caller are gone. **Fixed.**

The reviewer left three things uncheckable that this record should not claim: the wiki pages were fetched by
this session but not by the reviewer's (its `web_fetch` failed), the two-client acceptance is still unrun
(below), and the console's colour behaviour is code-read, not seen.
