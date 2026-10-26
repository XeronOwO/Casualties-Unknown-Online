# A dropped container arrives empty on the other side

- Status: Todo — observed 2026-10-26 in the paradise batch, and held by the custody projection afterwards.
- Priority: High — a user-visible loss of contents: the receiver's copy of the container has nothing in it.
- Category: Item custody / container contents / world materialization

## Design

`docs/design/model/item-custody.md` is the page this is judged against: a container's contents are rows like any other ("an item in CUO is one
authoritative identity"), and a client shows what its rows say. Its `Incarnations` rule is why the receiver's copy must build the children the row
describes, and the custody projection's `RowWithoutObject` is the instrument that found this.

## What the player does and expects

A player puts an item into a container (a toolbox), then drops the container on the ground. Every other player expects to pick it up, open it, and find
the item inside — the container's contents are part of what the item IS, and the game's own `Container` holds them as child `Item` objects
(`Container.LoadItem` reparents the child, `reversing/Assembly-CSharp/Assembly-CSharp/Container.cs:116-150`).

## What was observed

Driven in process on the guest: a water bottle loaded into a toolbox (`container-fill`, contents 0 → 1), then the toolbox dropped
(`item-provide mode=drop`). The HOST then:

- materialized the container: `[ItemSpawn] materializing toolbox (id 26672826005) at (10.1,470.3)` — **and nothing else**; the water bottle
  `18082891413` was never materialized on that side;
- and the custody projection has reported the disagreement every second since:

```
20:53:18.674 [ItemCustody] rows=358 objects=357 mismatches=1 — 18082891413:RowWithoutObject:Contained
20:53:24.729 [ItemCustody] rows=358 objects=357 mismatches=1 — 18082891413:RowWithoutObject:Contained
```

So the kernel row is right (the row says the bottle is CONTAINED in the toolbox) and the host's scene is missing the object. That is the projection's
`RowWithoutObject` family doing exactly its job.

## What the next cycle reads first

1. **Where the contents are dropped on the reporting side.** `RemoteItemSceneOps.SpawnWorldItem` does call
   `ItemStateCodec.RestoreContents(item, w.Item.Contents)`, so the receiver is ready to build children — which means the DROP REPORT most likely carried
   an empty `Contents`. The capture that builds a dropped container's `ItemData` is the place to read (the guest's own object had the child: the same
   client's projection stayed at `mismatches=0`).
2. **Whether the game's own save/load path materializes container contents at spawn** — `SaveSystem` restores a container's contents as child items
   (`reversing/Assembly-CSharp/Assembly-CSharp/SaveSystem.cs`, the item restore), so a world copy without them is not the game's own shape.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A container dropped with one item inside is picked up on the other side with that item inside (open it, see it) |
| 2 | The custody projection reports `mismatches=0` on every client across that drop, and stays there |
| 3 | The same holds for a container dropped with a NESTED container inside it |

## Limits

- Observed on the deployed build `0.1.0+705821d`; the reproduction is committed in this ticket's log lines, but the drop report's own capture has not
  been read yet, so the cause is not yet named as a code path.
