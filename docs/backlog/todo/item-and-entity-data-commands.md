# Item and entity data commands: give, spawn and a property editor

- Status: Todo
- Priority: Medium-High
- Category: Command surface / item and entity state
- Source: the user's 2026-10-07 backlog request — a Minecraft-style command family for CUO: item-specific
  properties (battery charge, liquid and millilitres, condition/durability and the rest) must be gettable,
  settable, mergeable and removable, and the family also covers handing an item out and creating an entity.
- Related: `todo/mod-content-ceiling.md` (the property surface this family writes through: item qualities,
  `useLimbAction`, the wearable set, and the content kinds with no provider),
  `review/in-game-command-console-interactive.md` (the console the verbs land in),
  `review/id-system-namespaced-ids.md` (how a mod-authored item id is addressed),
  `done/runtime-entity-creation-rejection.md`, `done/runtime-entity-spawn-backfill.md` (the admission path a
  spawned entity must go through), `docs/en/reference/mod-api.md`

## What is asked

Four capabilities over the same data model, driven from CUO's console:

| Capability | Meaning |
|---|---|
| give | put an item into a player's inventory with its properties already set |
| spawn | create an entity in the world (the game's own summon: an enemy, a trap, a container, a loose item) |
| data get | read the properties of a targeted item or entity |
| data set / merge / remove | change those properties |

The user's own examples of "properties" are the ones the game already stores per item: a battery's charge, a
container's liquid and its millilitres, and condition/durability. The stated intent is open-ended — an
operator or a mod author should reach the properties the game itself keeps, not a curated subset.

## What is not known yet

- Which properties actually cross the session boundary. The item state codec whitelists what
  `CustomItemBehaviour`'s `state`/`data` payload carries, and the limb/item component codecs are whitelists
  too, so a property a command sets locally may never reach the host or the other members. The cycle's first
  job is to read those codecs and write down what is addressable today.
- Whether a property write is local-only, host-authoritative or both, and what happens when it races the
  host's own state for the same item.
- Whether a command-spawned entity can reuse the accepted-creation path unchanged or needs its own
  admission, and what a member that joins later sees of it.

## Required work

1. Attribute first: list, per item domain and per entity kind, which properties have a reader, which have a
   writer, and which of those survive replication, a save/restore and a reconnect.
2. Design `give` / `spawn` / `data` as one family: one syntax, one selector vocabulary (the console already
   has player selectors), one permission story.
3. Every write goes through the same host path a gameplay action uses — a property set by a command must
   survive replication and restore exactly as a property set by playing does.
4. Report each verb's result to the caller (what changed, on which id, applied or refused) so a refused
   write cannot be mistaken for a silent one.

## Non-goals

- Not a scripting language or a command language design: the family is property-level.
- Not a cheating surface by default: the permission story is part of the work, and it is the host's call who
  may run the family.
- Not a raw memory editor: only properties the game's own data model carries are in scope.
