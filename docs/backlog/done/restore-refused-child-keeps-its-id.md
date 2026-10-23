# A restored container's refused child is destroyed and its instance id reported dead

- Status: Done — the refused child is dropped into the world and reported (`52e3bde3`); batch `20261023-i` read
  all three rows (record `docs/evidence/acceptance/restore-refused-child-keeps-its-id-20261023-i.md`).
- Priority: Low-Medium
- Category: Items / restore paths / item identity
- Source: Split out of the give ticket (`review/give-item-to-another-player.md`) when its row-4 loss was fixed (2026-10-19,
  batch `20261021-a`). That fix split one policy in two: the give ladder now HANDS ON the object a refused
  container materialized, while the bulk restore keeps discarding it. The discarded half is this ticket.
- Related: `review/give-item-to-another-player.md` (the fix and its live reading),
  `docs/evidence/acceptance/give-item-to-another-player-20261021-a.md` (the loss this family produces when a
  refused child is destroyed), `done/item-identity-across-transforms.md` (an item keeps its id across the
  moves this family makes)

## What it is

`ItemStateCodec.RestoreContentDiscarding` is the policy of the restore paths — the bulk
`ItemStateCodec.RestoreContents` (character restore, a world item's contents, a wearable's contents, the
scene apply) and `RemoteItemSceneOps.ApplyAuthoritativeState` — for a content item whose container refuses it:

```text
Restore: toolbox (id 9492956821) does not fit plasticbag(Clone) — refused by the container's own rule, not loaded.
```

The child is materialized (with its `ItemInstanceId` already stamped), the container's own `CanHoldItem`
refuses it, and the object is destroyed. Destroying an id-carrying object is not a local act: Unity defers it,
`ItemPatches.ItemOnDestroyPatch` reads the id at the end of the frame and `ItemWorldSync.OnItemDestroyed`
reports it, so the host's kernel stores the item TERMINAL — the same chain that lost the handed-over toolbox
in the give ticket's row 4, one call site over.

## Why it is filed rather than fixed in the same cycle

The give ladder has somewhere to put a refused object (a slot, then the world), and the user's ruling names
that rung. The restore paths have no destination the client may choose on its own: the snapshot states "inside
this container", the local rules state it cannot be, and inventing a body slot or a ground drop from the
receiving side would be a placement decision about data the other side owns. Whether the right answer is
"discard and announce the end", "place it in the nearest local slot and let the snapshot converge", or "refuse
the restore row by name" is a design question, not a mechanical one — and it is unread on the machine: the
refusal needs a snapshot whose contents the local rules reject (a mod that changes a limit, a hand-edited or
truncated archive, two peers on different builds).

## Acceptance

A two-client run on the deployed artifact:

1. A restore row whose container refuses one child (forge the archive or the container's limit): the child's
   instance id is either placed somewhere the run can read, or its end is announced — never silently absent
   from both the local tree and the host's table.
2. The same restore with every child accepted: unchanged, every child present with its id.
3. A world item's contents restored on the receiving client with one child refused: the host's item table does
   not lose the id without an announced end.

## The ruling (2026-10-23)

**A refused restore child is DROPPED INTO THE WORLD.** The user's words: a legitimate item cannot just vanish, so
destroying it is wrong; nor may it be pushed into "the nearest empty slot", because that slot can itself be a
container with no room, so a slot is not a destination known to work. It falls to the ground.

The three options the round had drafted (discard and announce the end, a local free slot, a whole-row refusal)
were all rejected: the first two were read off the code's own structure instead of the player's experience, and
the third is out of proportion. The destination that always exists is the world.

What it means for the implementation: the object the restore already materialized stays alive, becomes a world
item at the place it was being restored to, and its CREATION is reported through the same report the give
ladder's drop rung and the console `/summon` verb use (`WorldItemCreation`) — the host's item table then keeps the
id as a world item instead of storing it terminal. The destroy is removed from the restore policy entirely.

## What landed (2026-10-23)

- `ItemStateCodec.RestoreContentDropping` replaces `RestoreContentDiscarding`: a content item the container refused
  is handed to a late-bound drop seam instead of being destroyed. Unbound (a test host, or a composition that
  forgot), it is logged and LEFT ALIVE — never destroyed.
- `WorldItemCreation.DropAsWorldItem` reports the existing object as a world item at its own position, with the
  same `SendItemSpawned` the give ladder's drop rung and `/summon` make, so the host's kernel keeps the id as a
  world item instead of storing it terminal.
- `GameAdapterDomains` binds the seam at composition (`ItemStateCodec.BindRefusedContentDrop(...)`).
- `RestoreContentGateTests`: three facts (the policy does not destroy, the composition binds the seam, the drop
  reports the creation) plus a three-sample matcher; red on the pre-change tree (three failures), green after.

## Where the change lands (surveyed from the fix that split it)

- `ItemStateCodec.RestoreContent` already answers `RestoredContent` (the object, whether the container took it,
  and the container's reason), so the policy can change without touching the codec.
- `ItemStateCodec.RestoreContentDiscarding` is the one place that destroys a restored content item today; every
  other call site either loads it or hands it on.
- `RemoteItemSceneOps.ApplyAuthoritativeState` is the second caller of that policy and applies an authoritative
  tree onto an object that already exists — it may need the same answer as the bulk path or a different one,
  which is why the ticket asks the question before the edit.
