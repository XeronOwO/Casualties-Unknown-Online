# A same-frame second drop overwrites the pending report of the first

- Status: Review — the rejection's cause landed 2026-10-06: batch `20261006-h` left the re-scoped row's
  MACHINE half **passing** 2/2 (two departures register out of ONE frame and BOTH commit) and its WORLD half
  **failing** 2/2, because the receiving side adopted an id-less same-prefab copy at the reported position
  for the second report and that copy was destroyed ~10–30 ms later, leaving the id `terminal` in the host's
  kernel and the guest's own destroy command refused as `InvalidTransition`. That adopt path is fixed and
  attributed in `review/second-drop-report-loses-its-world-object.md` (`AdoptTargetRule` refuses a retired or
  proxy candidate, so the row materializes its own object); rows 3 and 4 await the next three-client batch,
  and the machine half this ticket owns is unchanged. Record: `docs/evidence/acceptance/
  drop-pending-single-slot-overwrite-20261006-h.md`. Earlier: the rejection of the previous row shape,
  `…-20261006-f.md`, whose premise this ticket's `## The row's producer` withdrew; the unit half,
  `…-20261006-c.md`. The drop-pending machine itself is unchanged and its unit pins stay green.
- Priority: Medium
- Category: Item sync / drop report carrier (the drop pending state)
- Source: the independent adversarial review of the drop report order fix (2026-10-05). It is promoted by
  the repository's own rule — a defect the work turns up is fixed in the same cycle, or filed with its
  evidence and its attribution. The mechanism is pre-existing, not introduced by that fix; what the fix
  changed is how loud its consequence is.
- Related: `done/container-move-snapshot-only-sync.md` (decision 236 stops the immediate re-report from
  covering a lost drop report, and its container-move pair is the second producer that made this ticket's
  single slot untenable: the pair's unload half registers a departure per expanded child, so a refused child
  load would have been overwritten by the next child), `review/second-drop-report-loses-its-world-object.md`
  (the failure batch `20261006-h` read on this row's world half — attributed to the materialization adopt
  path and fixed there), `todo/container-content-event-gap-on-repick.md` (the
  carrier gap a RE-PICK-cancelled departure leaves — the re-scoped row has no re-pick, see point 6 below),
  `docs/evidence/selfchecks/items/remote-intent-drop-report-order-selfcheck.md`

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
  the HEAD of that cycle with the then-current three-argument `EnterDrop` (the assertion it makes — the first
  departure's report survives a second departure in the same frame — is false on the single-slot machine); the
  committed form carries the `Source` argument this fix adds. The tree has carried the fix since, so
  `DropPendingState.EnterDrop` is the four-argument form today. Also
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

## The row's producer, read from the source (2026-10-06)

The reading this ticket owed was whether the R8 route decision 239 re-opened produces the two report-needing
departures the row asks for. It does not, and neither does the shape batch `20261006-f`'s `Limits` named. What
the producer really is follows from the two places a departure can be registered and the one loop that
registers more than one COMMITTED departure per frame.

**1. The R8 route registers no departure at all.** `RemoteIntentApplier.ApplySwapSlots` replays
`body.SwapSlots(targetSlot, body.SlotOf(item))`; `Patches/BodyItemPatches.cs`'s `SwapSlotsPatch` opens
`CallContext.Enter(CallContext.Origin.InternalReorder)` for the call, and the two departure registration hooks
read that scope — `DropItemPatch` fires only while `CallContext.Current != CallContext.Origin.InternalReorder`
(the same guard covers `Origin.Craft`) and `PickUpItemPatch` reports only outside it. On the owner's replay the
release window is not open, so no prefix skips the original and the scope covers the whole body. The native
body does make the pair the handoff counted — two `Body.DropItem` calls and, when both slots held an item, two
`Body.PickUpItem` calls inside one frame (`Body.cs:1413-1428`) — but every one of them runs inside that scope
and reports nothing: **zero departures, zero reports.** Nor can the second registration site be reached from
there: the swapped items are direct slot occupants, so the `container.UnloadItem(item, null)` inside
`Body.PickUpItem` never runs. The runtime agrees: the owner's window in batch `20261006-g`'s swap row reads two
`[PickUpResult]` lines (the `PickupFlowDiagnosticsPatch` postfix, which has no scope gate and states no fact —
it is a diagnostic, not a report) and two `[SlotMoved] … (Swap)` lines, with no `[ItemDropped]`, no
`[ItemTrace]` and no `[ItemSpawn]` on either viewer. The expectation that this shape is this ticket's producer
is withdrawn; that route's correctness is `todo/remote-inventory-native-parity-rework.md`'s row, not this one's.

**2. The `PickUpToSlot` route commits one departure and cancels one, and `Plan`'s two-drop list needs a
disagreement a gesture cannot create.** The `else` arm at `PlayerCamera.cs:1614` is reached when
`!(HoldingItem(slot) && HoldingItem(dragItem))` — a conjunction, so EITHER operand being false is enough — and
`HoldingItem(Item)` (`Body.cs:1301-1310`) is true only for a DIRECT slot occupant. A dragged item that is a
container child therefore makes that arm reachable with the destination slot OCCUPIED, which is exactly the
shape batch `20261006-f` drove as its passing row 2. There the owner's `RemoteIntentSlotRelease.Plan` reads
`slotIsHeld = true` and really does run `DropSlotItem` — the run's own `op=45 … origin=OnItemDropped` for the
occupant is that step's departure. What a gesture cannot produce is `Plan`'s TWO-drop list, `DropHeldItem` AND
`DropSlotItem`, because that needs `itemIsHeld` and `slotIsHeld` true together on the owner while the capture
ran the `else` arm: exactly a disagreement of the classification body (the display) with the owner's body, i.e.
the latency window and not a gesture. The frame's second departure on this route is the dragged container
child's own unload inside `Body.PickUpItem` (`Body.cs:1393-1397`), and it exists only when the pick-up
SUCCEEDS, in which case the re-pick cancels it (`result=Cancelled events=[RePick]`). A pick-up the native
guard refuses never reaches the unload, because the unload sits inside the same guarded block (`Body.cs:1390`)
as the re-parent. So this route registers two departures only as one commit plus one cancel, and batch f's
`Limits` shape — BOTH of that frame's departures reported — needs the same disagreement when the dragged item
is a direct slot occupant.

**3. Which replayed kinds reach a registration site, and where two COMMITTED departures can come from.** Two
sites register a departure and only two: `ItemWorldSync.OnItemDropped` (a body drop) and
`ContainerItemSync.OnUnloadedFromContainer` (`Container.UnloadItem`). Walking every kind `RemoteIntentApplier`
replays against them:

- `DropItem` and `DropWearable` register one body drop each; `TakeOutOfContainer` registers one unload, which
  commits; `MoveIntoContainer` registers one unload and consumes it when its load lands.
- `WearItem` registers one: `Body.WearWearable` unloads the item when it came out of a carried container
  (`Body.cs:1500-1505`), and the slot carrier's own report resolves it — the cancel edge `## What landed` added
  for exactly this path.
- `CombineItems` registers one, and it commits: `Body.CombineItems` unloads the receiving item when the
  container is above its holding weight or the item exceeds `maxWeightPerItem` (`Body.cs:1285-1289`), and no
  load follows.
- `UseItem` registers whatever the item's own `useAction` drops. Of the 139 `useAction` delegates in `Item.cs`
  exactly ONE makes two `Body.DropItem` calls inside one frame — `stonefruitclosed` (`Item.cs:2606-2621`),
  which drops the slot-2 occupant and then itself. Its second departure is the fruit's own, and the same call
  sets `item.condition = 0f` on an item whose `ItemInfo` carries `destroyAtZeroCondition`, so the destroy that
  follows (`Item.cs:157-177`) cancels it: that frame commits the occupant and cancels the fruit.
- `SwapSlots` registers none (point 1); `PickUpToSlot` registers the destination occupant when the drag is a
  container child, and the child's own unload only when its pick-up succeeds, where the re-pick cancels it
  (point 2).
- drain, battery load, battery unload, favourite and trader give register none.

The one loop that unloads more than one item inside one frame is R5's container expansion —
`RemoteIntentApplier.ApplyMoveContainerChildren`, the native loop at `PlayerCamera.cs:1585-1593` — which per
child runs `source.UnloadItem(child, null)` and then `target.LoadItem(child)`: a load that LANDS consumes its
child's departure and reports the move, a load the target REFUSES leaves the departure registered. **This
ticket's producer is an expansion in which two or more children's loads are refused after
`Container.CanHoldItem` admitted them** — the only frame a classified gesture can drive that registers two
departures which BOTH commit. The `UseItem` frame above registers two and commits one, which is the
one-commit/one-cancel shape batch `20261006-f` already read.

A third multi-report path exists and is NOT this machine's: `Container.UnloadAllItems` (a container breaking,
`Container.cs:46-66`) sets every child's parent directly without `Container.UnloadItem`, and
`ContainerItemSync.OnUnloadedAll` sends one `SendItemDropped` per child itself — no pending entry, no same-frame
reconcile, and no replayed intent kind reaches it. It is named here so a run does not read its several reports
as this ticket's frame.

**4. Which refusals a fixture can aim at.** `Container.LoadItem` (`Container.cs:116-151`) refuses after
`CanHoldItem` passed in three ways, plus the distance test in its own final condition:

- (a) the child is itself a `Container` with `GetHoldingWeight() > 0f` — a non-empty container child. This is
  also the check that makes such a child unloadable in the first place, so a carried container cannot acquire
  one through the loading path at all: only a world-generated container (a spawned container holding a
  container with contents) or a declared scene fixture produces that state.
- (b) the target container is itself inside another container (`this.mItem.TryGetParentContainer`) — it
  refuses EVERY admitted child, so two ordinary children are enough. Its cost is the cast: the target must be
  a container shown on an `InvButton`, and a nested container is on a button only while a container panel
  shows its parent (the operator's remote view of the owner's container, `container-panel mode=remote`).
- (c) the child IS the target (`item == this.mItem`) — it commits that one departure and lets the rest land.
- (d) `Vector2.Distance(child, target) < 10f` inside the final condition — the one refusal the applier cannot
  predict, because its own pre-check is `CanHoldItem` alone. Both containers ride the same body in every
  reachable configuration, so it is not a distance a fixture can aim at; it is named so a run does not read
  an unexpected refusal as a broken fixture.

**5. The fixture the row needs** (candidate, not yet driven): the owner carries bag C; bag B (empty) sits
inside C; bag A — a second carried container — holds two or more light items; the operator opens C as a
remote container panel, holds the expansion key (`PlayerCamera.cs:1557`, `KeyBinds.GetBind("expanddesc")`)
and releases proxy A onto proxy B's panel button in ONE invocation. Route (b) then refuses both children on
the owner. The readings the row asks for are the owner's two `[ItemTrace] … origin=OnItemUnloadedFromContainer`
lines settling through `FlushPendingDrop`, both ids in the host's world table, both materialized by the third
peer, and both viewers' clone-fact monitor at zero. The staging is a run's step, not code: `item-provide`,
`container-fill`, `container-panel mode=remote`, `key-hold` and `remote-gesture mode=release` already carry
every piece of it.

**6. The carrier-gap dependency is measurement, not construction.**
`todo/container-content-event-gap-on-repick.md` files the gap a RE-PICK-cancelled departure leaves — the
source container's content change reaches the peers only on the periodic snapshot. The re-scoped row has no
re-pick: the refused children's own drop reports are the events that explain the source container's change,
and the target container's contents do not change at all. Whether that is enough for a zero-monitor row is
what the run reads; this ticket may not claim it before the run says so, and the row is not blocked on that
ticket by construction.

## Why decision 236 makes it louder

Before that decision a lost drop report was partly covered by the intent's immediate character re-report:
the peers saw the item leave the inventory without an event (which the monitor already called a defect, and
which the ticket above exists to remove) but at least the clone converged. The re-report is now held back
while a drop report is pending — deliberately, because a snapshot sent ahead of the report is the
divergence the whole ticket is about — so a swallowed report leaves the peers with no event and no immediate
snapshot: only the periodic snapshot moves them, and the monitor's warning is the sole trace.

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

A three-client run that drives R5's container expansion onto a target which refuses every admitted child's
load, with two or more children admitted — the route (b) fixture of `## The row's producer`, or any other
shape that really registers two departures that both commit in one frame — and reads both peers. The row
passes when BOTH children reach the world table and are found by the third peer, the owner's trace shows both
departures committed out of the same frame, and the operator's and the third peer's clone-fact monitor stays
at zero. The unit half is what `DropPendingStateTests` has to grow, and already has: two drops entered in one
frame, both reported after the frame.

Two shapes may NOT be read in this row's place. A release onto an occupied slot of the displayed clone with a
DIRECTLY HELD drag runs the native swap and registers nothing at all (point 1) — but the same release with a
CONTAINER CHILD as the drag runs `PickUpToSlot`, registers the destination occupant and COMMITS it, so it is
excluded because one is not two, not because no departure exists (point 2). And a slot release whose pick-up
the native guard refuses leaves the frame with that same single departure: the child is never unloaded, so its
own departure is never opened (point 2). The `UseItem` route of `stonefruitclosed` registers two departures in
one frame and commits one (point 3) — it may be driven as a second reading, but it does not by itself satisfy
this row.

## Acceptance readings (batch `20261006-f`, 2026-10-06)

The batch drove that row's gesture on three clients (operator = physical-machine host, owner = sandbox guest,
third peer = the alternate sandbox). What it read, in the order it matters to this ticket:

1. **The named gesture cannot reach the named plan.** Releasing a proxy onto an occupied slot of the
   displayed clone takes the native R8 swap branch: while the remote view is open the native guards are
   answered by the displayed clone (`RemoteDragIntentCapture.ShouldAnswerFromDisplayBody`), so
   `PlayerCamera.cs:1614` sees both slots occupied and evaluates
   `body.SwapSlots(invButton.slot, body.SlotOf(dragItem))` — `RemoteIntentApplier.ApplySwapSlots`, never
   `ApplyPickUpToSlot`, whose two-`Body.DropItem` plan is the whole premise here. On that build the
   evaluation threw first: `Body.SlotOf` walks `Body.GetItem(i)`, whose `HoldingItem(i)` the predicate patch
   answers from the clone while the method body still indexes the LOCAL body's slot transform, so an empty
   local slot makes `Transform.GetChild(0)` throw (`UnityException … Transform child out of bounds`) and the
   release is lost whole — no intent, no refusal, `dragAfter` still set. The seam asymmetry was fixed by
   decision 239 (`todo/remote-inventory-native-parity-rework.md`), and point 1 above is what the repaired
   route does with the ticket's premise.
2. **The reachable two-departure shape behaves the way the landed machine intends.** Releasing the owner's own
   container child onto an occupied ring slot replays `PickUpToSlot` with the destination slot held on the
   owner, which registers both departures inside one frame: the occupant's `OnItemDropped` (committed —
   `FlushPendingDrop result=Committed(1) events=[Drop, Flush]`, `[ItemDropped] emergencylight …`, and the
   third peer materialized it) and the child's `OnItemUnloadedFromContainer` (cancelled by its own re-pick —
   `result=Cancelled events=[RePick]`). One entry per item is what made that pair settle correctly.
3. **The ticket's second half of the expectation is wrong, and the reading says so.** On a successful
   pick-up the released item does not reach the world table: it lands in the destination slot (the native R9
   outcome). Only the item that left the destination slot reaches the world. A row written as "both reach the
   world table" is not the game's behaviour; `## Acceptance` above is written to the game's.
4. **The zero-monitor criterion failed on a carrier gap, not on the pending machine.** Two
   `[CharSync] divergence` lines per viewer per gesture (operator and third peer, in both the occupied and
   the empty configurations) — the container's contents change and the child's new carried slot reach the
   peers only through the periodic snapshot, because the cancelled departure sends no report. Filed as
   `todo/container-content-event-gap-on-repick.md` with its own acceptance.
5. **What was left for this ticket, and where it went.** Re-scope the runtime row onto a shape that really
   produces two report-needing departures in one frame, and fix the carrier gap before a zero-monitor row can
   pass. The re-scope is `## The row's producer` above; the carrier gap is
   `todo/container-content-event-gap-on-repick.md`'s own work, and point 6 above records why the re-scoped row
   does not depend on it by construction — only by measurement.

## Acceptance readings (batch `20261006-h`, 2026-10-06)

The batch drove `## The row's producer`'s route (b) fixture on three clients — operator = physical-machine
host, owner = sandbox guest, third peer = the alternate sandbox — twice: once on the first fixture and once
on freshly created light items. Full reading and evidence pointers:
`docs/evidence/acceptance/drop-pending-single-slot-overwrite-20261006-h.md`.

1. **The shape the ticket asked for is reachable and the machine half passes.** The operator's release of
   the owner's source bag onto the NESTED target's panel button, with the game's own `expanddesc` bind held,
   captures ONE `MoveContainerChildren` intent; the owner's applier reports `0 of 2 direct child item(s)
   entered the container; 2 did not`, and both children's departures register out of ONE frame
   (`op=24`/`op=25 origin=OnItemUnloadedFromContainer` at 22:40:41.221) and BOTH commit
   (`origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]` for each at 22:40:41.247). The
   single-slot machine this ticket replaced could not have settled both.
2. **The world half fails, and not on the pending machine.** On the operator and on the third peer alike,
   the first child materializes its own world object and the second is instead bound to an id-less
   same-prefab world copy at the reported position, which is destroyed ~10–30 ms later: the id ends
   `terminal` in the host's kernel and the guest's own destroy command is refused as `InvalidTransition`.
   One gesture, two sent and committed reports, one standing object. Filed as
   `review/second-drop-report-loses-its-world-object.md`, attributed and fixed there on 2026-10-06: the copy
   was a clone display proxy the renderer had retired in that same frame (`Container.UnloadItem` detaches it
   and moves it onto `AdoptTolerance`, then the renderer deactivates it and queues its deferred destroy —
   and the scan's `includeInactive: false` ancestor lookup could no longer see it).
3. **The monitor criterion holds on the gesture.** Both viewers' windows over either gesture read zero
   `[CharSync] divergence`; the divergences the logs do carry belong to the fixture staging before the
   marks (`item-provide mode=create` + `container-fill`), which is the known create shape.
4. **Two fixture corrections the run measured.** `Container.CanHoldItem` is asked BEFORE the refusal route,
   so a target that cannot hold the child at all (`pouch`: ceilings 2.5/1.0 against a 1.5 `dogfood`) leaves
   the native loop with nothing to move and the release falls through to the world path — the first attempt
   dropped the whole source bag. And `container-fill` cannot address a container by type when two carried
   containers share a definition, so the published fixture uses distinct ones (`trashbag` outer,
   `plasticbag` target, `duffelbag` source) with their capacities read off the prefabs.
