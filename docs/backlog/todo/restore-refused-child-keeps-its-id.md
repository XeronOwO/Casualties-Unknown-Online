# A restored container's refused child is destroyed and its instance id reported dead

- Status: Todo
- Priority: Low-Medium
- Category: Items / restore paths / item identity
- Source: Split out of the give ticket (`todo/give-item-to-another-player.md`) when its row-4 loss was fixed (2026-10-19,
  batch `20261021-a`). That fix split one policy in two: the give ladder now HANDS ON the object a refused
  container materialized, while the bulk restore keeps discarding it. The discarded half is this ticket.
- Related: `todo/give-item-to-another-player.md` (the fix and its live reading),
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

## Where the change lands (surveyed from the fix that split it)

- `ItemStateCodec.RestoreContent` already answers `RestoredContent` (the object, whether the container took it,
  and the container's reason), so the policy can change without touching the codec.
- `ItemStateCodec.RestoreContentDiscarding` is the one place that destroys a restored content item today; every
  other call site either loads it or hands it on.
- `RemoteItemSceneOps.ApplyAuthoritativeState` is the second caller of that policy and applies an authoritative
  tree onto an object that already exists — it may need the same answer as the bulk path or a different one,
  which is why the ticket asks the question before the edit.
