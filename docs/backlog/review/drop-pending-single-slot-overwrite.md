# A same-frame second drop overwrites the pending report of the first

- Status: Review (the machine holds one entry PER ITEM since 2026-10-06 — `DropPendingState` is a set keyed by
  item id, `ItemDropState` maps each entry to its game-side place, and the frame-end flush settles every entry
  whose frame has passed; batch `20261006-c` verified the unit half and did NOT drive the runtime row — a slot
  release onto an OCCUPIED destination slot, read on both peers — because the session's scenarios went to
  `done/container-move-snapshot-only-sync.md`'s row A1g. No row of this ticket failed; the reading is what is
  missing. Record: `docs/evidence/acceptance/drop-pending-single-slot-overwrite-20261006-c.md`)
- Priority: Medium
- Category: Item sync / drop report carrier (the drop pending state)
- Source: the independent adversarial review of the drop report order fix (2026-10-05). It is promoted by
  the repository's own rule — a defect the work turns up is fixed in the same cycle, or filed with its
  evidence and its attribution. The mechanism is pre-existing, not introduced by that fix; what the fix
  changed is how loud its consequence is.
- Related: `done/container-move-snapshot-only-sync.md` (decision 236 stops the immediate re-report from
  covering a lost drop report, and its container-move pair is the second producer that made this ticket's
  single slot untenable: the pair's unload half registers a departure per expanded child, so a refused child
  load would have been overwritten by the next child), `docs/evidence/selfchecks/items/remote-intent-drop-report-order-selfcheck.md`

## What landed (2026-10-06)

The shape decision this ticket asked for, taken as `Set` rather than a guard, and taken together with the
container-move pair that needed it:

- `DropPendingState` (Runtime, pure) keeps a `Dictionary<ulong, Pending>` — one entry per item id, each with its
  own frame, op id and `Source` (where the item stood before it left). `TryTake` resolves one entry,
  `TrySettle(itemId, frame, alive, standalone, out pending)` settles one and KEEPS an entry that cannot report
  yet, and `ResetAll` hands every entry back so the trace stays balanced.
- `ItemDropState` (GameAdapter shell) holds the game-side place per item id beside the machine and maps each
  entry to the departure the flush reports. `EnterDrop` re-enters the SAME item's entry (an item cannot leave
  twice) and no longer touches another item's.
- `ItemWorldSync.OnItemDropped` no longer forces a same-frame flush of a different item first — that flush could
  not succeed (`TrySettle` refuses `currentFrame <= Frame` on purpose) and was exactly the assumption that lost
  the first report. `FlushPendingDrop` reports every due entry, and its due set is taken OUT of the machine
  before the first report goes out, because a report RE-ENTERS the flush (the item domain settles its deferred
  creation reports before an operation goes out, and this flush is one of those owners).
- The throw merge is unchanged and still per item (`TryConsumeByThrow`). The cancel edges are the same SET of
  sites — a re-pick, a container load, a destruction, a replacing unload and the world-left reset — but the set of
  departures that need one grew with the container unload, so ONE edge was added: the slot carrier
  (`ItemSlotSync.ReportCarried`) resolves a departure for the item it reports, because a wear straight out of a
  carried container is detached by `Body.WearWearable` and then parented to a limb — no container load, no pickup —
  and an entry left registered there can never settle. `ContainerMovePairGateTests` now pins the pairing: every
  carrier that can re-home an item resolves a pending departure (census floor 6 across the four carriers).
- The re-report hold asks whether a report is still OWED (`ItemDropState.HasReportOwed`: the entry's item is a
  standalone world item right now) rather than whether an entry merely exists, so an entry that can never settle
  cannot turn into a permanent hold on the owner's immediate snapshots — the consequence this cycle's independent
  review found on the wear path before the edge existed.
- Unit pins: `DropPendingStateTests.ASecondDepartureInTheSameFrame_DoesNotSwallowTheFirst` and
  `DropPendingStateTests.TwoDeparturesInOneFrame_BothSettleAndReportAfterTheFrame` — the first was read RED against
  HEAD with the then-current three-argument `EnterDrop` (the assertion it makes — the first departure's report
  survives a second departure in the same frame — is false on the single-slot machine); the committed form carries
  the `Source` argument this fix adds and therefore no longer compiles against HEAD. Also
  `EnterDrop_SameItemTwice_ReplacesThatItemsOwnEntry`, `ResetAll_ReturnsEveryOpAndClears`.

## Symptom (evidence, read from the code — the PRE-FIX shape, 2026-10-05)

`ItemWorldSync.OnItemDropped` flushed a pending drop of a DIFFERENT item before it entered the new one, with
the comment "two drops in one frame (rare) — flush the first first":

```csharp
if (_dropState.Current == ItemDropState.Phase.Dropped && !_dropState.IsPendingFor(item))
{
    FlushPendingDrop();
}
...
_dropState.EnterDrop(itemId, item, (Vector2)item.transform.position, op);
```

That flush could not succeed in the same frame: `DropPendingState.TryFlush` refused `currentFrame <= pending.Frame`
on purpose (the throw velocity may still land — a zero-velocity report materialized a ghost), and
`ItemDropState.EnterDrop` then OVERWRITES the single pending slot. The first drop therefore never produces a
report at all: the item trace keeps a begin-without-end, the peers never materialize the item (the drop report
is what creates the host's world-table row; the keyframe `RefreshWorldItemStates` early-returns for an id the
table does not hold and the follow stream only moves copies that already exist), and the clone fact table
prints `left the inventory without an event sync` once the next snapshot drops the item.

The unit side used to PIN the overwrite as INTENDED behaviour (an `EnterDrop` test asserting that the caller "flushed
the different item first") — which is exactly the assumption that does not hold on a same frame. That pin is replaced
by one that folds only a repeated departure of the SAME item, beside the regression pins
`DropPendingStateTests.ASecondDepartureInTheSameFrame_DoesNotSwallowTheFirst` and
`DropPendingStateTests.TwoDeparturesInOneFrame_BothSettleAndReportAfterTheFrame`.

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

## What a fix had to decide (the questions as they stood on 2026-10-05 — all three answered in `## What landed`)

- The pending state was one slot (`DropPendingState._pending`), so the fix was a shape decision rather than a
  guard: either the machine holds a SET of unsent reports (one per item, each with its own frame and op) and
  a flush settles every one of them whose frame has passed, or the drop report stops being a per-frame
  deferred single and the throw merge moves to a per-item record. **Decided: the set.**
- The throw merge is why the deferral exists at all (one `DropItem` → `ThrowItem` pair = one report with the
  final velocity), so a set must keep that per-item merge and the same cancel edges (re-pick, container load,
  destroy, world left). **Decided: kept per item, and the edge list grew by the one re-home carrier that had
  none (the slot carrier's wear/drag report).**
- The one-frame flush stays: a same-frame forced flush is the ghost this machine was built to prevent.
  **Decided: unchanged.**

## Acceptance

A three-client run that drives a slot release onto an OCCUPIED destination slot (the gesture above) and reads
both peers: the item that left the first slot must reach the world table and be found by the third peer, and
the operator's monitor must stay at zero. The unit side is what `DropPendingStateTests` has to grow: two drops
entered in one frame, both reported after the frame.
