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
| 1 | On a three-client session, `/heal @a` typed by the host resets all three bodies, read from each client's own body state |
| 2 | `/heal @s` on a member resets only that member; `/heal @p` resets the nearest player to the executor |
| 3 | The reply names every player it reset, including the executor, and the line reads in the operator's language (the localization ticket) |
| 4 | A solo host with no session can use `@a` on a cheat verb (the degenerate case the current shape makes impossible) |

Rows 1-3 need a real two- or three-client session, which is the release-cycle acceptance; row 4's degenerate case is covered by
`CommandSelectorResolverTests.AHostAlone_IsStillSelectableByEveryBaseSelectorThatMeansEveryone` as far as a test host can reach it.

## Limits

- The reply text is a player-facing string, so this ticket inherits the localization requirement; the rows above only require that
  the same players are named in both places.
