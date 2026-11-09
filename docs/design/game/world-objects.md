# The game's world objects (1.0)

Cited from the 1.0 decompile (`reversing/release-1.0/Assembly-CSharp/`).

## Buildings are entities with health

`BuildingEntity` is the base every placed building carries: a health value with a setter, a display name and the
break particle (`BuildingEntity.cs:7-67`). Its `health` is the number the world's building entity table stores and the
number the interaction paths read; a building at health 0 is destroyed (the openable marks itself destroyed exactly that
way, `Openable.cs:12`).

## Openables: two kinds, two gates

`Openable.OnUse` (`Openable.cs:8`) is the whole interaction:

- A **lock** checks the player's carried `lockpickingkit` (`Openable.cs:24-25`) and starts `LockpingMinigame` with a
  precision that scales with the run setting `lockpickprecision` (`:30`); completing it sets the building's health to 0
  (`:12`), i.e. the container is "opened" by being destroyed.
- An **instant open** bypasses the minigame (`instantOpen`, `:34`); a keypad openable (`isKeypad`, `:37`) is the
  keypad path instead.

The census of 1.0's prefabs (230 openables in five container prefabs — medcrate, dropcapsule, containercrate, foodbox,
lifepodchest) means "openable" is a container property, not a door.

## Containers are item-side

A `Container` is a component on an item, not a separate object: it counts its items (`Container.cs:11`), caches the
item component (`:26`) and keeps a per-item weight cap (`:31`). Loading and unloading go through the container's own
methods; `ContainerUnloader` is the automatic path. A carried container's contents are part of the item's state, which is
why the save and the clone renderer both treat children specially.

## Traders

`TraderScript` is the trader's own object: a reputation value (`:22`), a hostile flag (`:32`), per-item preferences
(wants-trade / indifferent / wants-keep) that colour items in the trade UI (`:41-51`), a value multiplier (`:59`) and the
open action (`:65`). The trade prices are derived from the item's value times the preference multiplier, so a trade is a
pure function of the two sides' item values and the trader's reputation.

## Loot pools

`ItemLootPool` builds a category → item-id table once from the whole item registry (`ItemLootPool.cs:10-27`); the
world's loot rolls draw from a category. That makes a loot table's content a function of the item registry, not of a
per-object list.

## What the mod must respect

- An opened lockable is destroyed (`health = 0`), so a restored world must not expect it to exist; a save's world-entity
  rows for consumed lockables are satisfied by their absence.
- A container's contents are the item's own state; a replicated item must carry them.
- A trader's stock is generated on each use against the registry; the mod syncs the trade's outcome, not the stock.

## Not covered here

- The scavenging tables' numbers and the category list: they come from the item registry's categories; this page maps
  the mechanism.
- The corpse loot path (`CorpseScript`/loot windows) and the shuttle/start-open objects.
