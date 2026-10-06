# A same-frame second drop overwrites the pending report of the first

- Status: Todo
- Priority: Medium
- Category: Item sync / drop report carrier (the drop pending state)
- Source: the independent adversarial review of the drop report order fix (2026-10-05). It is promoted by
  the repository's own rule — a defect the work turns up is fixed in the same cycle, or filed with its
  evidence and its attribution. The mechanism is pre-existing, not introduced by that fix; what the fix
  changed is how loud its consequence is.
- Related: `review/container-move-snapshot-only-sync.md` (decision 236 stops the immediate re-report from
  covering a lost drop report), `docs/evidence/selfchecks/items/remote-intent-drop-report-order-selfcheck.md`

## Symptom (evidence, read from the code)

`ItemWorldSync.OnItemDropped` flushes a pending drop of a DIFFERENT item before it enters the new one, with
the comment "two drops in one frame (rare) — flush the first first":

```csharp
if (_dropState.Current == ItemDropState.Phase.Dropped && !_dropState.IsPendingFor(item))
{
    FlushPendingDrop();
}
...
_dropState.EnterDrop(itemId, item, (Vector2)item.transform.position, op);
```

That flush cannot succeed in the same frame: `DropPendingState.TryFlush` refuses `currentFrame <= pending.Frame`
on purpose (the throw velocity may still land — a zero-velocity report materialized a ghost), and
`ItemDropState.EnterDrop` then OVERWRITES the single pending slot. The first drop therefore never produces a
report at all: the item trace keeps a begin-without-end, the peers never materialize the item (the drop report
is what creates the host's world-table row; the keyframe `RefreshWorldItemStates` early-returns for an id the
table does not hold and the follow stream only moves copies that already exist), and the clone fact table
prints `left the inventory without an event sync` once the next snapshot drops the item.

`DropPendingStateTests.EnterDrop_OverwritesPriorPending` pins the overwrite as INTENDED behaviour ("the caller
flushed the different item first") — which is exactly the assumption that does not hold on a same frame.

## Why it is reachable from a normal gesture

`RemoteIntentSlotRelease.Plan` returns `DropHeldItem` AND `DropSlotItem` when the dragged item and the
destination slot are both occupied, and `RemoteIntentApplier.ApplyPickUpToSlot` runs both `Body.DropItem`
calls inside ONE frame. Batch `20261005-e`'s passing slot-release row drove the EMPTY-destination
configuration, which is precisely the shape that avoids the collision.

## Why decision 236 makes it louder

Before that decision a lost drop report was partly covered by the intent's immediate character re-report:
the peers saw the item leave the inventory without an event (which the monitor already called a defect, and
which the ticket above exists to remove) but at least the clone converged. The re-report is now held back
while a drop report is pending — deliberately, because a snapshot sent ahead of the report is the divergence
the whole ticket is about — so a swallowed report leaves the peers with no event and no immediate snapshot:
only the periodic snapshot moves them, and the monitor's warning is the sole trace.

## What a fix has to decide

- The pending state is one slot (`DropPendingState._pending`), so the fix is a shape decision rather than a
  guard: either the machine holds a SET of unsent reports (one per item, each with its own frame and op) and
  a flush settles every one of them whose frame has passed, or the drop report stops being a per-frame
  deferred single and the throw merge moves to a per-item record.
- The throw merge is why the deferral exists at all (one `DropItem` → `ThrowItem` pair = one report with the
  final velocity), so a set must keep that per-item merge and the same cancel edges (re-pick, container load,
  destroy, world left).
- The one-frame flush stays: a same-frame forced flush is the ghost this machine was built to prevent.

## Acceptance

A three-client run that drives a slot release onto an OCCUPIED destination slot (the gesture above) and reads
both peers: the item that left the first slot must reach the world table and be found by the third peer, and
the operator's monitor must stay at zero. The unit side is what `DropPendingStateTests` has to grow: two drops
entered in one frame, both reported after the frame.
