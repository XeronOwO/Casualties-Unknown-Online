# Cross-player solid food from the game's own data

- Status: Todo — **cut 2026-10-08** out of `mod-cross-player-native-semantics.md` when that ticket's
  consume (drink) chain landed and its "Eat / drink" row split: the drink half had the native shape the
  migration is built on, the solid-food half does not.
- Priority: High
- Category: Mod platform / cross-player item use / architecture
- Parent: `docs/backlog/todo/mod-cross-player-native-semantics.md` (Part B)
- Related: `docs/backlog/todo/mod-content-ceiling.md` (Part 2 stage 2, its pre-cut statement),
  `docs/evidence/selfchecks/players/cross-player-native-drink-semantics-selfcheck.md` (the sibling
  chain that landed), `docs/evidence/selfchecks/players/cross-player-native-topical-semantics-selfcheck.md`,
  `docs/decisions/active.md` (184: the affected side judges on its own picture)
- Source: the 2026-09-25 mod-ceiling inventory (Part 2); this branch was named in the parent's chain
  table and cut on 2026-10-08 because its native shape is a different one.

## Why this ticket exists

`RemoteConsumeCatalog.Food` is the last hand-transcribed content table of the cross-player consume
family: 25 item ids, each with hunger/thirst/weight/happiness/sickness/radiation deltas and a condition
cost, transcribed out of the items' decompiled `useAction` delegates. The drink half of the same file —
and the whole `RemoteDrinkMedicineCatalog` beside it — was deleted on 2026-10-08
(`mod-cross-player-native-semantics.md` Part B), so the ceiling this table imposes (no mod food, no
vanilla food the table never listed, a game update silently moving the arithmetic) is now the family's
only remaining one.

## What the game itself offers (measured 2026-10-08, `reversing/Assembly-CSharp/Assembly-CSharp/`)

| Native fact | Where | What it gives us |
|---|---|---|
| `ItemInfo.useAction` is `delegate void Use(Body body, Item item)` | `ItemInfo.cs:188`, field at `:109` | the per-item behaviour is a DELEGATE, not data; the body it feeds is the first parameter |
| The native use dispatch | `Body.cs:2475-2481` (`if (item.Stats.usable) item.Stats.useAction(this, item)`), called from `PlayerCamera.cs:1646` | one entry point, and its target is always the caller's own body |
| The eating body methods | `Body.cs:2259-2294` `Eat(float hungerAmount, float weightGain)`, `Body.cs:3680-3685` `Drink(float amt)` | the bulk of a food effect is two native calls with numeric arguments; `Eat` also clamps at 125, converts overflow into sickness and rolls vomit/burp |
| The rest of a food effect | e.g. `Item.cs:2380-2389` geofruit, `:2408-2414` bread, `:2430-2437` cake, `:3614-3693` browncap | direct body-field writes (happiness, temperature, sickness, radiation, adrenaline, septic shock), a per-item `item.condition -= X`, item transforms (a bitten sprite, a bucket becoming an empty bucket) and a `Sound.Play` at the eating body's position |
| No food data exists | `ItemInfo.cs:87-185` (no nutrition field), `LiquidItemInfo.cs:1-15` (capacity/autoFill/defaultContents only) | the amounts are literals in the delegate; the only exception is the `NonDescriptCan` component (`NonDescriptCan.cs:67-83`), which is not an `ItemInfo` |
| The table is incomplete | **41** vanilla items whose `useAction` calls `body.Eat` / `body.Drink` (plus the component-driven `nondescriptcan`), against the table's 25 ids | mod food and the unlisted vanilla food have no cross-player semantics today |

The fact that decides the design: **a food delegate writes the eating body AND the item it is handed**
(`item.condition -= 0.34f`, `item.GetComponent<SpriteRenderer>()`, `body.DropItem(item)` in the two
bucket cases), and the affected side's client has no item instance. The drink chain did not have this
problem: `WaterContainerItem.Drink(Body body, float amount, string sound)` is a single divertible call
whose effect is a liquid delegate that takes a `Body`, so the operator could measure it and the patient
could run it.

## The two candidate designs

Both keep the invariants the migrated chains established: the host owns admission, the resource and the
arbitration; the affected side judges on its own picture; the numbers come from the game, never from a
CUO table.

**A — the affected side runs the item's own delegate against a scratch item.** The operator measures the
item's cost (its own real item, run against the affected player's displayed body), and the patient
materializes a hidden instance of the item from the wire state, runs `useAction(ownBody, scratch)`, then
destroys it. Native execution on the body that eats, including the clamping, the random rolls and the
talker reaction. Costs: a real Unity item instance is created and destroyed on the patient (CUO's
item-fact carriers must not report it, which needs a scope or a marker), the delegate's item writes go
to a copy (the authoritative cost has to come from the operator's run), and a delegate that moves or
drops the item (`bucketofchicken`, `popcorn`) does so on a scratch object whose slot is -1.

**B — the operator measures the delegate's body deltas and the patient applies them.** The operator runs
the item's own `useAction` against the affected player's render clone inside a capture window, and the
body fields it moved become the effect the host carries; the patient applies that delta through the
existing health apply. Costs: the patient's half is CUO arithmetic rather than the game's own code
(the family's whole point), the clone is momentarily fed (a talker bubble, an eat animation, the
calories counter), and the clamping/random branches are evaluated on the clone's state rather than on
the patient's own.

The design question — how much fidelity the food branch is worth, given that the alternative to both is
leaving a 25-row table in place — is the user's to settle at this ticket's start; the drink chain's
landing does not decide it.

## Scope

The row also names the item-level gesture routing: with the table gone, the routing predicate should ask
the game's own data (a `useAction`-bearing item whose class is not one of the other families) exactly
the way the drink chain asks `usable`. Whatever routing the design needs is part of this ticket.

## Hard acceptance

Delete `RemoteConsumeCatalog` (both the table and the type) and every existing case of the chain stays
green, with each deleted case's disposition listed in the self-check — the standard the injection,
topical and drink chains were held to. The cases are
`RemoteConsumeApplicationTests.ApplyFood_AppliesBreadEffect`, `..._Catalog_ExposesTheCuratedFoodItems`,
`ItemUseTests.Host_UsesBreadOnGuest_AppliesFoodAndSendsResult`, and the `IsActuallyUsable` half of
`CarriedItemUseTree`.

## Non-goals

- Not a mod-API surface change: a mod still cannot author an item's `useAction` (Part 3 A of the ceiling
  ticket), so this ticket's content win is the vanilla items the table never carried.
- Not a re-litigation of the drink chain's shape.
