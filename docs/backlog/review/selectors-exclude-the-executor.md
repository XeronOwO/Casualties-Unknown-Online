# `/heal @a` skips the player who runs it

- Status: Review — the code landed in `7fb1556d` (2026-10-25) and the suite migrated with it; the rows below are the
  release-cycle acceptance a two- or three-client session has to judge.
- Priority: Medium
- Category: Console / cheat verbs / player selectors
- Source: user report (2026-10-25). The measured reply on the host: `/heal @a` answered `Reset 76561198863287957: … Reset
  76561199526807662: …` — the two MEMBERS only — and the host's own body was untouched.

## Goal

`@a` selects every player in the session, the one who typed the command included, on every verb that takes a player selector
(`/heal`, `/give`, `/summon`, `/gamerule`). `@s` stays the executor alone; `@p` the nearest.

## Design

The surface's own record is `docs/design/cross-cutting/permissions-and-commands.md` (a cheat verb's selector is a chain command's
argument, and the meanings are Minecraft's because the family follows that template — `AGENTS.md` rule 17), with the surface row in
`docs/architecture/surfaces.md` naming `CommandSelectorResolver` as the one Unity-free expansion. The design question the ticket
settles is the one that page left to the command family: a selector set that drops the sender makes the most convenient form of
every cheat verb unusable on the client an operator debugs most, so the meanings are the template's, and a bracketed filter narrows
the candidate set BEFORE the selector picks from it (Minecraft's own order).

## Current behaviour

The host's `/heal @a` reset the two members and not itself. The selector resolution therefore enumerates the remote members without
the local player, which also means a solo host can never be reset by `@a` at all.

## Measured fallout (a first attempt, reverted the same session)

Changing `CommandSelectorResolver.SelectBase` to Minecraft's meanings (`@a`/`@e` = every player including the executor, `@p` = nearest
including the executor, `@r` = one of every player) is a ONE-LINE change in the resolver and a FAMILY-WIDE semantic migration
everywhere else. The attempt was made on 2026-10-25 and measured before it could ship: **32 behaviour tests failed**, every one of them
because the console command tests use `@a` to mean "the members" and the commands correctly refuse a selector that now matches the
executor as well —

- `CommandConsoleItemGiveTests` (9), `CommandConsoleHealResetTests` (11), `CommandConsoleItemDataTests` (5),
  `CommandConsoleMemberItemGiveTests` (2), `CommandConsoleMemberHealResetTests` (1), `CommandConsoleWorldSummonTests` (1), and
  `BacklogIntegrityGateTests.EveryDocumentedTestAnchorIsStillDeclared` (1, an anchor whose test name would move).

The attempt was reverted (`git checkout -- src/ tests/`) rather than half-shipped, so the tree is green and this ticket carries the plan
instead of a broken suite. What the cycle needs, in order:

1. Decide the test vocabulary for "one named member": with `@a` including the executor and `@p` answering the executor at distance
   zero, a test that means one member targets it by NAME (`@a[name=…]`), which is also what a player does.
2. Migrate those 32 sites and the command docs that quote `@a`'s meaning, in the same change as the resolver.
3. Re-read the affected tests' intent one by one — several assert "the host's own body did NOT change", which is exactly the
   behaviour this ticket reverses; they must be rewritten to assert the new behaviour, not renamed.

## Why this is the wrong shape

The command family follows Minecraft's template (root `AGENTS.md` rule 17): there `@a` is **all players, including the executor**,
`@s` is the executor, `@p` the nearest. A selector set that silently drops the sender makes the most convenient form of every cheat
verb unusable on the one client an operator debugs most — the host.

## Done in code (2026-10-25)

The resolver now carries Minecraft's meanings, and the suite is migrated to them (`7fb1556d`):

- `@a` and `@e` answer EVERY player, the executor included; `@s` the executor alone; `@p` the nearest — the executor at
  distance zero; `@r` one of everyone.
- The bracketed FILTER now narrows the candidate set BEFORE the base selector picks from it, which is Minecraft's own order:
  `@p[distance=1..]` is "the nearest player who is not standing on me", not "the nearest player, if it happens to be far
  enough". The old order made every filtered `@p`/`@r` answer empty as soon as the executor stood nearest.
- A target's display name falls back to its steam id, and the `name` filter accepts that id, so `@a[name=7656…]` names a player
  whose persona has not arrived. This is the vocabulary the tests use for "one specific player" and it is what an operator types
  when the roster line shows an id.
- The 32 behaviour tests the earlier attempt broke are migrated: the console suites that meant "the other player" say so in the
  selector, the two tests that asserted the old meaning assert the new one, and the multi-body refusal counts three bodies because
  `@a` really names three. `BacklogIntegrityGateTests`' anchor to two renamed resolver tests was re-pointed in the same change.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | On a three-client session, `/heal @a` typed by the host resets all three bodies, read from each client's own body state | **PASS 2026-10-28** (deployed build, three clients in a `paradise` world): the host's `/heal @a` reply names all three ids — its own first (`76561198281246659`), then the two members — and every client's own `body-read` changed: `energy` 82.8/82.8/81.8 → 99.8/99.7/99.6 and `badSleepAmount` -110.9/-110.8/-111.7 → -3.3/-4.2/-5.0, so the host's own body was reset too (the reported bug was that it was not) |
| 2 | `/heal @s` on a member resets only that member; `/heal @p` resets the nearest player to the executor | **PASS 2026-10-28** (same session, the guest opped first): the guest's `/heal @s` and `/heal @p` each ordered `Reset 76561198863287957` — the guest's OWN id — and its console then reads that id fully reset, so `@s` is the executor and `@p` is the executor at distance zero, which is the template's meaning |
| 3 | The reply names every player it reset, including the executor, and the line reads in the operator's language (the localization ticket) | **PARTIAL 2026-10-28**: the naming half passes (all three ids appear, executor included). The language half fails: the host's own config is `Language = zh` and the reply is English — `Reset <id>: sent to that player's own client, which applies the full reset and re-reports the state.` These sentences are built by `PlayerHealResetService` as plain strings and never carry a catalogue key, which is also why the console's raw-answer census cannot see them: it counts `ConsoleAnswer.*($"…")` literals, and a string a SERVICE returns is invisible to it |
| 4 | A solo host with no session can use `@a` on a cheat verb (the degenerate case the current shape makes impossible) |

Rows 1-3 need a real two- or three-client session, which is the release-cycle acceptance; row 4's degenerate case is covered by
`CommandSelectorResolverTests.AHostAlone_IsStillSelectableByEveryBaseSelectorThatMeansEveryone` as far as a test host can reach it.

## Limits

- The reply text is a player-facing string, so this ticket inherits the localization requirement; the rows above only require that
  the same players are named in both places.

## The language half, fixed and swept (2026-10-28)

The reply's English on a Chinese host was not a missing key — the parts were keyed all along. It was the ANSWER SHAPE: a host's own answer never
crosses the wire, so only its assembled text reached the console, while a member's answer arrived as keyed parts. `ConsoleAnswer` now carries
`Parts` for exactly that case, the console prints one line per part through the reading client's catalogue (falling back to the answer's own sentence
when an entry is missing), and `PlayerHealResetService.Reset` returns those parts instead of a bare string.

**The same defect family, swept** — every host-composed answer that is built locally rather than received:

| Surface | Shape today | Same gap? |
|---|---|---|
| `/heal` | service returned a string; parts only on the wire | **fixed in this change** |
| `/give` | `PlayerItemGiveService` returns a string; `give.answer.*` keys exist for the wire answer | **yes** — the caller's own console prints the assembled text |
| `/summon` | `WorldSummonService` returns a string; `summon.answer.*` keys exist | **yes** |
| `/data` | `ItemDataCommands` answers text for the read verbs | **yes for the assembled lines** |
| `/players`, `/whoami` | the console assembles the list itself | the labels are keyed (`console.answer.session_facts`), the rows are data |

The rule the fix encodes: **an answer a command builds for its OWN console must carry parts or a key, exactly like one that crosses the wire** — the
sentences already exist in the catalogue, so each of the rows above is the same small change (service returns `ConsoleAnswer`, command returns it).
Verified on a captured frame the same evening: the host's `/heal @a` now draws the Chinese catalogue''s sentence for `heal.answer.refused_for` and its reason
part (`heal.answer.reason_host_not_in_world`) — the per-part rendering path — where the same answer used to print one assembled English sentence.
### `/give` attempted and reverted the same evening (2026-10-28)

The same change was applied to `/give` (`PlayerItemGiveService.Give` returning `ConsoleAnswer.Ok(text, parts)`, the control and the command forwarding
it) and reached a green build with **six behaviour tests failing**, all of them because their assertions quote the ASSEMBLED sentence while the parts
carry the catalogue's own wording:

- `CommandConsoleItemGiveTests` (five: the state/property refusals and the drop-at-position reply),
- `CommandConsoleWorldSummonTests` (one, which shares the give fixture).

The change was reverted rather than half-shipped (`git checkout --` on the six files), so the tree is green and this note is the plan:

1. teach each test helper to read EVERY line an answer printed (the heal cycle already learned this: the joined text, not the last line);
2. re-point the six assertions at the parts' sentences (they are the catalogue's, which is the point of the change);
3. then the same for `/summon`, and `/data` last.
### The second `/give` attempt found the real decision (2026-10-28, reverted again)

Applying the same shape a second time (with the joined-line test helper) made the failures readable, and they name a DESIGN choice rather than a test-update
chore: the wire answer deliberately replaces a refusal whose reason is free text with the generic keyed line `answer.refused_see_host_log` ("Refused — the
host's log names the reason."), and keeps the detailed sentence in the host's log (`PlayerItemGiveService`, the two `GiveOutcome` refusal branches: the
keyed one passes its `labelKey`, the unkeyed one passes the generic line). The host's own answer prints `Refused: {reason}` with the real reason.

So converting the local answer needs one of two shapes, and the choice is the user's because it changes what a player reads:

1. **generic line** — the host sees the same keyed sentence a member sees; the detail stays in the host's log (consistent, one sentence to translate, the
   local player loses the reason on screen);
2. **keyed shell + the reason as an argument** — `answer.refused_detail` = "Refused: {0}", the shell translated, the free-text reason stays English
   (the host keeps the detail; the technical sentence is not translatable anyway, which is WHY the wire answer went generic).

The second is recommended and is one key plus one call site; the first is zero work but deliberately blindfolds the host. Either way the six test
assertions follow the chosen wording, and `/summon` shares the same helper (`AnswerPart("answer.refused_see_host_log")`), so it takes the same decision.
### The keyed-origin slice, attempted and mapped (2026-10-28)

The first slice of the redesign's rule 1 (a refusal is a key at its origin) was written and reverted: a `GrantRefusal` type in
`Runtime/GameAdapter`, `ItemDefinitionTable.TryResolve` returning a key plus its arguments, `IItemGrantSource`/`ItemGrantSource` carrying it,
`PlayerItemGiveService` passing it into the answer part, and five catalogue entries per language. The build then named exactly three call sites the slice
must also touch, which is the useful result:

| Site | What it needs |
|---|---|
| `WorldSummoner.Refused(string)` | `WorldSummonOutcome` carries a reason STRING and no parts, so a keyed refusal has nowhere to travel; the outcome gains a parts list (the heal/give answers already have that shape) |
| `ItemStateDocument.DefinitionSchema` | probes `TryResolve` for a template only; it takes the two new `out` parameters |
| `UnavailableItemGrantSource` | the no-adapter source also implements the port; its refusal becomes keyed too |

None of it is design work — it is the mechanical half of a slice whose design is settled. Reverted rather than half-shipped (the tree is green and the
deployment matches `778d43fd`), and this table is the next attempt's starting point.
### The keyed-origin slice landed (2026-10-28)

`ItemDefinitionTable` now answers a `GrantRefusal` (sentence + catalogue key + arguments) for each of its four grounds, `IItemGrantSource`/`ItemGrantSource` carry it, and `PlayerItemGiveService` renders the key — so `/give`'s creation refusals read in the operator's language on the operator's own client, which is the half that used to be English while only a member's answer was translated. Six catalogue entries per language; `LocalizationCatalogueGateTests` learned the `new GrantRefusal(…)` shape as a read, so the keyed-refusal rule is enforced for the next one. The summon port keeps the text for now: its outcome carries no parts, and that is the next slice.
