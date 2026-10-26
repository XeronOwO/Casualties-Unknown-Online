# A dropped container arrives empty on the other side

- Status: Review — fixed 2026-10-26 (`MaterializeRowContents`); the machine row (a dropped container arriving with its content) is owed to the next batch.
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

## The cause, read from the code (2026-10-26)

The capture and the wire are both fine — the loss is in what the receiver is GIVEN:

1. `ItemReportCommitter.SendDropReport` logged `[ItemDropped] toolbox … — container contents 1`, so the sender knew about the child;
2. `ItemStateCodec.CaptureItem` recurses into a container's children and fills `CharacterItemMsg.Contents`;
3. `ItemKernelCodec.ToKernelData` then maps that message to `ItemData` — and **`ItemData` has no contents field by design**: "each contained item is its
   own `ItemState` with a Contained location so the container graph is authoritative and acyclic" (`GameState/Domains/Items/ItemData.cs`). So the drop
   command's payload legitimately carries only the container's own state, and the child stays a row of its own with a `Contained` location;
4. the receiver materializes the world item from the COMMAND PAYLOAD (`RemoteItemSceneOps.SpawnWorldItem` → `ItemStateCodec.RestoreContents(item,
   w.Item.Contents)` with `w` built from that payload), so its `Contents` is empty and no child object is created — while the child's row still says
   `Contained`, which is the mismatch the projection reports.

So the defect is the one stage 2 of the custody work exists to end: the receiver builds a VIEW from the message instead of from the rows it already holds.
The fix belongs at that boundary — when a world item is materialized, its contents are the rows whose parent is this id (`ItemCustodyRows` already
answers parent lookups for the standing path), not the payload's list.

## The fix (2026-10-26)

`RemoteItemSceneOps.MaterializeRowContents` materializes a container's contents from the authority's rows (`ItemCustodyComparison.ContainedIn`), each
child through the ordinary landing path — so nesting recurses, an object already present is not duplicated, and the game's own `Container.LoadItem`
attaches it. The message's own `Contents` list stays as the fallback the restore paths still need; on a live world spawn the rows win.

The rule is on `model/item-custody.md` and in audit ledger row 190. One test pins the query (`ContainedIn_AnswersOnlyTheDirectChildrenInIdOrder`:
direct children only, id-ordered, so two clients build the same tree).

**Owed**: acceptance rows 1-3 on a batch — the same drop that reproduced it (a toolbox with a water bottle, dropped and picked up on the other side).