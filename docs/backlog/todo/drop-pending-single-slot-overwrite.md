# A same-frame second drop overwrites the pending report of the first

- Status: Todo — Rejected (batch `20261006-f`, 2026-10-06, drove the row's gesture on three clients and both
  rows failed. The release of a display proxy onto an OCCUPIED slot of the displayed clone aborts inside the
  game's own release body — `UnityException … Transform child out of bounds`, `Transform.GetChild` ←
  `Body.GetItem` ← `Body.SlotOf(item)` ← `PlayerCamera.TryPerformInventoryAction`, i.e. the R8 swap branch the
  viewer classifies that release into — so no intent is captured, no refusal line is written and the drag is
  left staged; the two-`Body.DropItem` plan this ticket names is never reached. The reachable shape (the
  owner's own container child released onto an occupied ring slot) does settle both same-frame departures
  correctly — the destination slot's occupant committed and reported, the child's unload cancelled by its
  re-pick — but the operator's and the third peer's clone-fact monitors each warn twice over the same
  gesture, so the zero-monitor criterion is not met either. Records:
  `docs/evidence/acceptance/drop-pending-single-slot-overwrite-20261006-f.md` (this batch) and
  `…-20261006-c.md` (the unit half); the monitor gap is filed as
  `todo/container-content-event-gap-on-repick.md`. The machine's own shape is unchanged and its unit pins
  stay green)
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

## Acceptance readings (batch `20261006-f`, 2026-10-06)

The batch drove the gesture on three clients (operator = physical-machine host, owner = sandbox guest,
third peer = the alternate sandbox). What it read, in the order it matters to this ticket:

1. **The named gesture cannot reach the named plan.** Releasing a proxy onto an occupied slot of the
   displayed clone takes the native R8 swap branch: while the remote view is open the native guards are
   answered by the displayed clone (`RemoteDragIntentCapture.ShouldAnswerFromDisplayBody`), so
   `PlayerCamera.cs:1614` sees both slots occupied and evaluates
   `body.SwapSlots(invButton.slot, body.SlotOf(dragItem))` — `RemoteIntentApplier.ApplySwapSlots`, never
   `ApplyPickUpToSlot`, whose two-`Body.DropItem` plan is the whole premise here. On this build the
   evaluation throws first: `Body.SlotOf` walks `Body.GetItem(i)`, whose `HoldingItem(i)` the predicate patch
   answers from the clone while the method body still indexes the LOCAL body's slot transform, so an empty
   local slot makes `Transform.GetChild(0)` throw (`UnityException … Transform child out of bounds`) and the
   release is lost whole — no intent, no refusal, `dragAfter` still set. The occupied-destination route is
   therefore not this ticket's producer at all; the applier's plan is only reachable when the classification
   body and the item's owner disagree.
2. **The reachable two-departure shape behaves the way the landed machine intends.** Releasing the owner's own
   container child onto an occupied ring slot replays `PickUpToSlot` with the destination slot held on the
   owner, which registers both departures inside one frame: the occupant's `OnItemDropped` (committed —
   `FlushPendingDrop result=Committed(1) events=[Drop, Flush]`, `[ItemDropped] emergencylight …`, and the
   third peer materialized it) and the child's `OnItemUnloadedFromContainer` (cancelled by its own re-pick —
   `result=Cancelled events=[RePick]`). One entry per item is what made that pair settle correctly.
3. **The ticket's second half of the expectation is wrong, and the reading says so.** On a successful
   pick-up the released item does not reach the world table: it lands in the destination slot (the native R9
   outcome). Only the item that left the destination slot reaches the world. A row written as "both reach the
   world table" is not the game's behaviour and should be rewritten when this ticket is re-scoped.
4. **The zero-monitor criterion fails on a carrier gap, not on the pending machine.** Two
   `[CharSync] divergence` lines per viewer per gesture (operator and third peer, in both the occupied and
   the empty configurations) — the container's contents change and the child's new carried slot reach the
   peers only through the periodic snapshot, because the cancelled departure sends no report. Filed as
   `todo/container-content-event-gap-on-repick.md` with its own acceptance.
5. **What is left for this ticket.** Re-scope the runtime row onto a shape that really produces two
   report-needing departures in one frame (the container-expansion loop with a child whose load is refused
   was named in the ticket's own Related note and remains undriven), and fix the carrier gap before a
   zero-monitor row can pass.

Since that batch the blocking seam asymmetry is fixed (decision 239, `remote-inventory-native-parity-rework`'s
`## The swap half's fix`): a release onto an occupied slot of the displayed clone reaches the R8 route again and
the owner replays `Body.SwapSlots` — two `Body.DropItem` and two `Body.PickUpItem` inside one frame
(`Body.cs:1413-1428`), the same-frame pair this ticket's per-item pending machine has to resolve. Whether that
shape produces the two report-needing departures the row asks for is this ticket's own next reading, not an
expectation carried from here.
