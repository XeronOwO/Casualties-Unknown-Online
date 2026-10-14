# Item and entity data commands: give, spawn and a property editor

- Status: Todo — the read half landed 2026-10-14 as `review/item-data-read-commands.md`; the stages below are
  what remains.
- Priority: Medium-High
- Category: Command surface / item and entity state
- Source: the user's 2026-10-07 backlog request — a Minecraft-style command family for CUO: item-specific
  properties (battery charge, liquid and millilitres, condition/durability and the rest) must be gettable,
  settable, mergeable and removable, and the family also covers handing an item out and creating an entity.
- Related: `review/item-data-read-commands.md` (the landed read half, and the survey these stages build on),
  `todo/mod-content-ceiling.md` (the property surface this family writes through: item qualities,
  `useLimbAction` and the wearable set),
  `review/in-game-command-console-interactive.md` (the console the verbs land in),
  `todo/give-item-to-another-player.md` (the gesture half of `give`; its finding — no path carries an item
  INTO another player's inventory today — is what this family's `give` faces too),
  `done/id-system-namespaced-ids.md` (how a mod-authored item id is addressed),
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

## What the attribute answered (it came first, as this ticket required)

`review/item-data-read-commands.md` is the read half, and the property-by-property survey lives there. The three
conclusions every stage below depends on:

- **What is addressable at all** is one kernel payload per item — `ItemData`'s condition, favourited, slot,
  liquid stacks and typed component fields — captured from the game's own `SavedItem` fields plus the codecs'
  whitelists, and restored the same way. Outside it there is nothing for a verb to address: an item's definition
  data is content rather than per-instance state, and a component field the codec does not carry never leaves
  its process.
- **Where a write goes** is that same row. The host writes its own authority
  (`ItemKernelAuthority.TryUpdateState`); a member's write of its OWN item rides the existing
  `ItemUpdateState` report path, which is the one a native item use already takes. What does not exist is a
  host-side edit of another player's CARRIED item: a carried item's state is its owner's report, and the
  correction machinery resolves world items only. A remote edit therefore needs its own mechanism — a carrier
  that tells the owner to apply the change — and that is a decision to take, not a detail to improvise.
- **Racing the host** is answered by the same model: a member's report is owner-predicted and host-validated,
  the kernel keeps one revision per item, and a command write is expected to take the identical path rather
  than a second one beside it.

Two gaps the read half found and deliberately did NOT close, both of which stage B meets first:

- **The host's own untouched carried items are not in the record — and have no instance id at all.** A member's
  client registers its carried inventory (starting supplies and worn items included) at the generation-finished
  edge; the host's supplies are its local authority, whose ids are minted lazily on first domain entry —
  `CarriedInventoryReporter` states exactly that as its reason for being guest-side. Every verb in this family
  addresses an instance id, so the gap has to be settled before a write or the cursor target can reach an item
  the player is actually holding at world entry: either the host registers its own carried inventory the way a
  member does, or the family gains a read that can name an item before it has an id.
- **No verb can address the cursor.** The console reads the kernel, which has no cursor, so "the item nearest
  the mouse" needs a new GameAdapter capability port. It belongs with stage B because the write half needs
  local-scene access anyway.

Still open, and belonging to the stage that meets it: whether a command-written property survives a
reconnect the way a played one does (stage B's acceptance), and whether a command-spawned entity can reuse the
accepted-creation path unchanged or needs its own admission, including what a later joiner sees of it
(stage D's first question).

## The remaining stages

1. **Stage B — the write verbs** (`data set` / `merge` / `remove`) over the properties the survey lists, and the
   local-scene answer the two gaps above need. The write is the owner's own: apply it to the local item and
   report it exactly as a native use does, so the row a command writes is indistinguishable from a row the game
   wrote. Acceptance: a set value survives the next snapshot, a save/restore and a rejoin on both peers, and a
   refused write is answered by name. The CURSOR target the user asked for lands in the same stage, on the same
   new capability port.
2. **Stage C — `give`**: put an item into another player's inventory with its properties already set. The
   missing half is materialization on the receiving side — today an item reaches another player only by the
   receiver taking it or by dropping it for a pickup — so this stage owns that carrier, and it shares the
   finding with `todo/give-item-to-another-player.md`.
3. **Stage D — `spawn`**: create an entity in the world through the accepted-creation path, with the
   late-joiner question answered before the verb lands.
4. **The permission story, inside every stage.** Reads are `Anyone` today (the read half's own reasoning is in
   its ticket). Writes are the host's call, and the family expresses that as a host rule following the
   existing `HostRulesOptions` flags — enforced on the HOST side, because a member's write reaches the kernel
   as a wire command and the member's own console cannot be the gate.

## Non-goals

- Not a scripting language or a command language design: the family is property-level.
- Not a cheating surface by default: the permission story is part of the work, and it is the host's call who
  may run the family.
- Not a raw memory editor: only properties the game's own data model carries are in scope.
