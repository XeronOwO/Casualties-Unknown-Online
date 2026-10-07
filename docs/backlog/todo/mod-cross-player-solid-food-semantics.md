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

## The design — settled 2026-10-08 by the user: objects follow data

**The principle.** If the synchronized data says an item exists, the item must also exist as a local
game OBJECT on every client that holds that data. Data and object are meant to correspond; today they do
not for items the local machine never had to draw or simulate (an item inside another player's
inventory has a data row on every client and no object anywhere except on its holder's). That gap is
what this ticket closes, and closing it is a platform change rather than a food-only change.

**Shape of the change (to be written up before code).**

1. **Materialize on the data signals.** Every place the item data learns about an item — the full table
   on world entry and reconnect, each committed batch's create/destroy/state-change, and container
   contents recursively — gets a corresponding object create / update / destroy on the client. The
   signals already exist; only world items are wired to them today (`ItemApplication`,
   `ItemReconcile`, `ItemSnapshotService`).
2. **Objects are kept aligned by an explicit apply path.** Data changes arrive; something writes them
   onto the objects. This path is mandatory rather than optional — without it the objects go stale while
   the data stays fresh. World items already have it (the position stream plus the item application);
   the rest need it.
3. **The objects are not the display proxies.** `RemoteCloneRender`-marked presentation copies of
   another player's items are a rendering concern with their own lifetime and are deliberately excluded
   from every authoritative report path; the new objects are the data's local incarnation and must be
   kept in a separate category from them, with that separation stated in the code, not implied.
4. **Then the food chain is ordinary.** With the item object present on the eater's client, the eat is
   the game's own `Body.UseItem` → `item.Stats.useAction(body, item)` run against the eater's own body
   and that object: the clamps, the random rolls, the talker reactions, the condition cost and the
   container swap all happen natively. The resulting change rides the existing report → host
   arbitration → broadcast path, so the feeder's own item (whose ownership never moved) receives the
   result. No new wire shape, no ownership transfer, no inventory-space precondition, and a meal that
   is interrupted or cut by a disconnect simply reports nothing and costs nothing.

**Side-effect investigation comes FIRST (next cycle, read-only).** Standing objects mean objects the
local machine never had before, and they enter the game's own item list (`Item.allItems`). Before any
code: enumerate every path that walks that list (physics and collision, rendering, sound, save/restore,
the game's own sweeps, and CUO's reconciliation/kill logic), and for each record what it does with an
object that is present but neither held nor in the world; confirm the reconciliation cannot mistake one
for a stale row and kill it; measure the scale (how many objects a session adds, what each batch has to
update, any per-frame cost); and hunt for counter-evidence — any path that would let a player click,
pick up, count or report one of these objects as their own. The result of that investigation, not an
assumption, decides whether the rule holds as stated or needs a scope limit.

**Rejected on the way here (recorded so they are not re-proposed).**

- *Give the item away first (a real transfer).* Rejected by the user: a bite of your own sandwich would
  become a handover, and a full inventory would block being fed.
- *Hand the delegate a scratch item.* Rejected by the user: it has no slot on the eating body, so the
  container-swap delegates (`stonefruit`, `bucketofchicken`, `popcorn`, `rosepod` — `Item.cs:2606-2618`,
  `:2790-2800`, `:2834-2844`, `:1437-1441`) would read `SlotOf` = -1 and silently skip their swap.
- *Materialize only at the moment of the meal.* Rejected by the user as the wrong shape: the object
  should exist whenever the data does, not be conjured for one action.

## The earlier candidate designs (kept for the record)

Both keep the invariants the migrated chains established: the host owns admission, the resource and the
arbitration; the affected side judges on its own picture; the numbers come from the game, never from a
CUO table.

**A — the affected side runs the item's own delegate.** *Its scratch-object form and its
transfer-the-item-first form are both rejected above.* What survives is the split the constraint
forces: the body half on the eater's client, the item half where the item is real — and with the
settled "objects follow data" rule the item half runs on the eater's own local object of the feeder's
item, whose change then rides the ordinary report path back to the real one.

**B — the operator measures the delegate's body deltas and the patient applies them.** The operator runs
the item's own `useAction` against the affected player's render clone inside a capture window, and the
body fields it moved become the effect the host carries; the patient applies that delta through the
existing health apply. Costs: the patient's half is CUO arithmetic rather than the game's own code
(the family's whole point), the clone is momentarily fed (a talker bubble, an eat animation, the
calories counter), the clamping/random branches are evaluated on the clone's state rather than on the
patient's own, and the item-level half (the condition cost, the container transforms) cannot come from
the clone at all, because the clone holds no item. Not needed under the settled design; kept as the
record of the alternative.

## Scope

Two things now, and the first is the platform change the settled design needs:

- **Objects follow data** (the section above): the item materialize / update / destroy path that today exists
  only for world items, extended to every item the synchronized data carries, with the side-effect
  investigation done before the code.
- **The food chain itself**: with the object present, the eat is the game's own `Body.UseItem` →
  `useAction`, and the routing predicate asks the game's own data (a `useAction`-bearing item whose
  class is not one of the other families) exactly the way the drink chain asks `usable`.

## Hard acceptance

Delete `RemoteConsumeCatalog` (both the table and the type) and every existing case of the chain stays
green, with each deleted case's disposition listed in the self-check — the standard the injection,
topical and drink chains were held to. The cases are
`RemoteConsumeApplicationTests.ApplyFood_AppliesBreadEffect`, `..._Catalog_ExposesTheCuratedFoodItems`,
`ItemUseTests.Host_UsesBreadOnGuest_AppliesFoodAndSendsResult`, and the `IsActuallyUsable` half of
`CarriedItemUseTree`. The platform half carries its own acceptance: the side-effect investigation's
findings are answered one by one, and a session shows the same item count and no new per-frame cost
against the pre-change baseline.

## Non-goals

- Not a mod-API surface change: a mod still cannot author an item's `useAction` (Part 3 A of the ceiling
  ticket), so this ticket's content win is the vanilla items the table never carried.
- Not a re-litigation of the drink chain's shape.
