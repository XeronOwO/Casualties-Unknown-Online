# A give degrades to a ground drop, and rearranging that item then retires it

- Status: Todo — opened 2026-10-25 from two user reports in one session (translated from the Chinese originals): "I gave an item to another
  player, then rearranged items on that player, and when the move finished the item had been swallowed", and
  earlier the same chain losing a flashlight ("I dragged the flashlight, dragged it a few times in a row, and the flashlight was gone").
- Priority: Critical — an item the player gave is destroyed without reaching anybody.
- Category: Cross-player item transfer / placement ladder / item arbitration
- Source: user reports (2026-10-25) plus the host's own log from the batch that produced them.

## Design

`docs/design/mechanics/give-and-take.md` states the give's landing ladder and that a REFUSAL by the receiving side is answered and
reported, never silently degraded; `docs/design/mechanics/death-and-drops.md` and `docs/design/cross-cutting/saves-and-recovery.md`
hold the rule that an item's ownership never leaves every side at once, and `docs/design/mechanics/carry.md` the placement query the
receiver answers.

## Root cause 2, read from the refusal (2026-10-25, after the swap fix)

The swap fix closed the occupied-slot loss, and the retest still lost the item — the guest's own log named the second cause directly:

```
18:02:01.242 [ItemTrace] op=9 begin item=1091242674115 origin=OnItemDropped event=Drop
18:02:01.244 [ItemTrace] op=9 item=1091242674115 origin=OnItemPickedUp result=Cancelled events=[RePick]
18:02:01.269 [SlotMoved] emergencylight (id 1091242674115) → slot 5 (Drag) reported
18:02:01.328 Kernel command rejected by host for item 1091242674115: ItemUpdateState refused (InvalidTransition…)
```

and the host's own log shows what made the item terminal four minutes earlier, at the GIVE:

```
18:02:00.149 [ItemTrace] op=37 item=1091242674115 origin=OnItemDestroyed result=Committed events=[Destroy…]
18:02:00.182 [StandingItem] materialized emergencylight (id 1091242674115) for <guest>
```

`PlayerInventoryPlacement.TryDestroyById` removes the giver's copy of a transferred item with `Object.Destroy` and **leaves its instance id
on the object**, so `ItemWorldSync.OnItemDestroyed` reports a destroy for an item that did not die — it moved. The kernel marks the row
terminal, and every later state update from the new owner is refused (`ItemUpdateState refused (InvalidTransition…)`), after which the row
leaves the inventory and the item exists nowhere.

The fix zeroes the instance id before the destroy — the same step `KillRemoteItem` takes for a remote deletion — so the destroy of a
TRANSFERRED item reports nothing: the authoritative transfer already states where the item went.

## Root cause, read from the game (2026-10-25, fixed in the same cycle)

The native body-slot release is **two** branches, and CUO had applied one of them to both cases. `PlayerCamera.cs`'s own release:

```csharp
if (this.body.HoldingItem(invButton.slot) && this.body.HoldingItem(this.dragItem))
{
    this.body.SwapSlots(invButton.slot, this.body.SlotOf(this.dragItem));   // BOTH HELD → swap, both items stay
}
else
{
    if (this.body.HoldingItem(this.dragItem)) { this.body.DropItem(this.dragItem); }
    if (this.body.HoldingItem(invButton.slot)) { this.body.DropItem(invButton.slot); }
    this.body.PickUpItem(this.dragItem, invButton.slot, false);             // only ONE pickup
}
```

`RemoteIntentSlotRelease.Plan` emitted the **else-branch trio for every case**, including the both-held one. In that case the trio drops the
occupant of the destination slot (`DropSlotItem`) and then picks up **only the dragged item** — the occupant is never picked back up, so it
stays on the ground and its row leaves the owner's inventory. That is the loss, exactly and deterministically:

- The host's log of the failing session shows the rearranges landing on the destination slot the freshly given item sat in
  (`PickUpToSlot … slot 5`, then `… slot 4`, `… slot 1`, `… slot 0`, `… slot 3` for the other item on that body).
- The item that vanished was always **the one that had just been given** — i.e. the occupant that had not been there before — while an item
  dragged onto an EMPTY slot was safe, because that path never drops an occupant.

The fix is the missing branch: both held → `RemoteIntentStep.MoveBySwap` → `Body.SwapSlots(targetSlot, body.SlotOf(item))`, the game's own call
(which drops the two and picks **both** back). Everything else keeps the else-branch order. `RemoteIntentSlotReleaseTests` pins both branches and
names the regression (`ACarriedItem_OntoAnOccupiedSlot_NeverDropsTheOccupant`).

Process note, recorded because it cost the user several rounds: the same conclusion was available from the decompiled release branch and the
plan's own shape; it was instead chased through instrumented re-runs. `AGENTS.md` now carries the rule that a cause is established by reading,
with the live eval/patch channel as the only sanctioned instrument.

## Superseded: the reproduction attempts that preceded the fix

## Second reproduction (2026-10-25, deployed `53baa1c1`) — the picture changed

The user retested and reported: the item lost after a rearrange is **the one just given**, while an item that was ALREADY on that player does
**not** get lost, and this time the host logged **no `PickUpToSlot` intent at all** for the rearrange — only the two gives
(`[Take] … emergencylight`, `[Take] … plasticchunk`) and, separately, two id-less pickups the host SKIPPED:

```
[ItemTrace] op=0 item=0 origin=OnItemPickedUp result=Skipped events=[NoId]
[PickUpResult] emergencylight → slot (slot 3) at (-0.8,507.3)
```

So the authoritative row was never asked to move. What the host sees vanishing is the **display proxy**, and the likely mechanism is the one the
`review/nested-container-clone-proxy-leaks-as-world-item.md` ticket already names: a drag on a proxy item runs the native body on the viewer,
which DROPS the proxy into the viewer's world; the row is intact and the other client still holds the item, while the viewer's own screen loses
it and its clone table then diverges.

**Narrowed next step (not a guess — the two tickets now meet):** the give's own arm must be measured first, because the item that is lost is the
one the give just created on this side: log the give's landing (slot/container/drop), the proxy's lifetime, and the release's classification for a
drag that produced no intent. If the proxy leaks, the fix belongs to the proxy leak rather than to the slot plan — which is why the slot plan's own
change (`53baa1c1`) did not move the symptom.

**Also recorded:** `Body.SwapSlots` is NOT world-free — `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:1413` is
`DropItem(slot1); DropItem(slot2); PickUpItem(...); PickUpItem(...)`, so the swap is a native drop-then-pick pair like the trio it replaced
(with `PickUpItem(..., true)`'s flag and a "switch" sound as the differences). The plan change therefore keeps the native ordering semantics and
removes the applier's hand-rolled trio, but it cannot by itself explain or fix the loss.

## What the log shows (host, 2026-10-25, build `1fa171f0`)

```
16:25:07.534 [RemoteIntent] GiveToBody captured for item 1250156464067 of 76561198281246659 (container 0, slot …)
16:25:07.535 [Placement] asked 76561198863287957 where item scrapmetal … lands
16:25:07.570 [ItemArbitration] Item 1250156464067 adopted into 76561198863287957's transfer table
16:25:07.570 [Take] 76561198863287957 takes scrapmetal (id 1250156464067)
16:25:07.583 [ItemTrace] op=23 item=1250156464067 origin=OnItemDestroyed result=Committed
16:25:07.616 [StandingItem] materialized scrapmetal (id 1250156464067) for 76561198863287957
16:25:07.616 [Placement] item 1250156464067 is where this host wrote it
…
16:25:29.026 [RemoteIntent] PickUpToSlot captured for item 1250156464067 of 76561198863287957
16:25:29.943 [StandingItem] retired scrapmetal (id 1250156464067, owner 76561198863287957…)
```

So the chain is:

1. The give was captured as `GiveToBody` and the item was transferred to the guest — but the placement did not put it in the guest's
   backpack: it was **materialised as a STANDING item in the world** ("materialized … for 76561198863287957", "is where this host wrote
   it"). The give had already degraded to its last rung before the player did anything else, and nobody was told: the member's answer
   says "gave", not "dropped".
2. The item then sat in the world as a standing item owned by that player. Moving it around inside the displayed backpack produced a
   `PickUpToSlot` intent for an id the world holds, and the materialiser **retired** the standing item — after which no body and no world
   row held it: the swallowing the user reports.

## What BOTH clients' logs show (host + guest, 2026-10-25 16:25)

The give itself LANDED — the first reading above ("degraded to a drop") was wrong, and the two logs together correct it:

```
host  16:25:07.535 [Placement] asked <guest> where scrapmetal lands
guest 16:25:07.550 [Placement] answered query 2 for item scrapmetal (id …)
guest 16:25:07.590 [PickUpResult] scrapmetal → slot (slot 4)          <- the guest's own body took it
host  16:25:07.616 [StandingItem] materialized scrapmetal … for <guest>   <- the host ALSO made a world object for it
```

so the give landed in slot 4 AND a standing duplicate was materialised on the host, because the host learned of the new carried row only
from the 1 Hz snapshot (`[CharSync] divergence … a new carried item the fact table never saw`). The duplicate is what the "swallowed after a
move" report is
built on: the move then succeeded —

```
host  16:25:29.026 [RemoteIntent] PickUpToSlot (item …, slot 5)
guest 16:25:29.057 [ItemTrace] op=27 … origin=OnItemDropped event=Drop        <- the native move's first half, on the guest
guest 16:25:29.058 [PickUpResult] scrapmetal → slot (slot 5)
guest 16:25:29.058 [RemoteIntent] replayed native PickUpToSlot on item … (slot 5)
guest 16:25:30.131 [Reconcile] 290 items: killed 1
host  16:25:29.942 [CharSync] divergence … left the inventory without an event sync
host  16:25:29.943 [StandingItem] retired scrapmetal …: the data no longer carries the row
```

— and then the item left the guest's inventory without an event: the guest's own reconcile killed one item and the host retired the
standing row, so at the end no body and no world held it.

**The chain, corrected by the guest's log (the guest's `Reconcile` is the killer):**

- The give landed in slot 4, and the HOST materialised a **standing duplicate for a carried row** (`[StandingItem] materialized scrapmetal … for
  <guest>`) because it learned of the row only from the snapshot. That duplicate is a rendering proxy that carries the SAME instance id as
  the carried row.
- The move to slot 5 succeeded on both clients (`[PickUpResult] … slot 5` on the guest, `slot 4 → 5` divergence on the host).
- The guest's world reconcile then counted that id among the world items it does not know and **killed one item**
  (`[Reconcile] 290 items: killed 1, spawned 0, not taken 0, dropped 0`), which is the moment the id was destroyed.
- Its end then reached the host as "left the inventory without an event sync", the host retired the standing row, and the carried row went
  with it: `[StandingItem] retired scrapmetal … the data no longer carries the row`.

**So the defect is the duplicate's IDENTITY, not the ladder:** a standing render proxy for a carried row must never share the authoritative
instance id, or its local death must never be reportable as the item's end. `RemoteIntentApplier` already has the shape for this — the
`HasPendingDropReport` guard refuses to announce a change while a report is in flight, and the give's unplaced arm keeps `OnDestroy`
silent with a marker — and the same suppression is what a standing proxy needs.

## The two defects, separately

- **D1 — the give's ladder falls to the drop rung without saying so.** The receiver answered the placement query with a destination the
  host resolved as "drop at that position" (`give.answer.dropped` in the catalogue is the sentence for it), and neither client's player
  was told. The question the first batch must answer: WHY the guest's backpack refused a 2-item inventory (a slot count, a layer or a
  range answer, a stale inventory row), because a give that cannot land in a nearly empty backpack is the defect, and the silent drop is
  the second half of it.
- **D2 — a `PickUpToSlot` intent can retire a standing item into nothing.** `StandingItemMaterializer` retired the item on the strength of
  an intent about a different carrier (a body slot), so the two records disagreed and both ended empty. This is the data-loss half and it
  is independent of why the give dropped: any intent naming an id whose carrier is the world must either land the item in the slot, or
  refuse by name and leave the standing item alone.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A give into a nearly empty backpack lands in the backpack: the target's client shows it in a slot and the giver's log shows no `StandingItem materialized` for that id |
| 2 | When the receiving side really refuses, both players are told (the giver's answer says dropped/refused, the target's screen says why), and the item exists exactly once afterwards |
| 3 | Rearranging items inside a displayed backpack never loses one: after three moves the same ids exist, each in exactly one place, on both clients |
| 4 | If an intent names an id whose carrier is the world, the item either lands where the intent says or stays where it is — never both gone |

## Limits

- The first batch must read BOTH clients' logs: the placement answer is the receiver's sentence about its own inventory, and the host's
  log only shows what it did with the answer.
- The ids in this record are from the run; they must not be copied into a page (only into this ticket's evidence).
## Row 1 passes live on the current build; rows 3-4 stay open with the channel's reason (2026-10-29 23:55, build `0b8e185a`)

A give driven through the release seam (give-probe mode=give target=<guest> slot=2): the host's log reads `[RemoteIntent] GiveToBody captured …` →
`[Placement] asked <guest> where item lantern …` → `[Placement] item … is where this host wrote it (slot 2): the placement stands`, the guest's own log reads
`[PlayerInteraction] placed lantern (id 1452019926979) in local slot 2`, the kernel row is `kind: Carried, owner: <guest>, slot: 2`, and the world holds no lantern at
the origin. **Row 1 PASS** — the item reaches the backpack and exists once.

The REFUSAL half of row 2 was also observed: with the alt's six slots full the same give answered `[RemoteIntent] refused GiveToBody: waterbottle does not fit
any free slot on this client`, the giver kept the bottle in slot 0, and no row went terminal. What is NOT captured is the second half of row 2 — the target's own on-screen line — because
the target had no window open; it stays a named residual.

Rows 3 and 4 (rearranging inside a DISPLAYED backpack, three moves, every id in one place) could not be driven in this session, and the reason is the
acceptance channel rather than the feature: the three bodies stand at the same world position, the displayed backpack is opened by RANGE (CrossPlayerDragUse.AutoOpenFor), and
separating one leaves its body under the HUD, where the hover raycast finds no body at all (ing-buttons answers empty, drag-state still dragging). The code those rows test
is pinned by tests instead — `StandingItemGateTests.TheWorldTransition_RetiresTheStandingObjectAtEverySite` and
`.TheWorldMaterialization_RetiresAStandingIncarnationBeforeItsIdempotencyCheck`, plus the swap fix's own rows — and a batch that can separate the players on screen would judge
them live.
## Row 3 judged live: three moves, six ids, one place each, on both clients (2026-10-31, build `0c282f7f`)

The channel that blocked this row was the MOUSE: the displayed backpack opens by range and a separated body sits under the HUD, so the hover raycast found nothing. This batch drove the
other half of the same path — the native INVENTORY INTENTS the drag capture produces — through a new acceptance recipe `remote-intent` (IPlayerInteractionControl.SendRemoteInventoryIntent),
which is exactly the call the capture makes; the capture itself is pinned by RemoteDragIntentCaptureTests. What the row is about is what the OWNER does with those intents.

Setup: the host carried six local items (bandage 1417660188611 in slot 0, trashbag slot 1, waterbottle 1421955155907 slot 2, lantern slot 3, dogfood slot 4, waterbottle
1413365221315 slot 5). The GUEST then sent three intents against the host's inventory — `SwapSlots` (bandage, slot 3), `PickUpToSlot` (dogfood, slot 0), `SwapSlots` (waterbottle, slot 5). The
host's log shows each one accepted and replayed natively (`[RemoteIntent] <guest> → <host> SwapSlots (item 1417660188611 …)` followed by `replayed native SwapSlots`).

Read back afterwards, the host's own rows hold **six items, each exactly once**: dogfood slot 0, trashbag slot 1, waterbottle 1413365221315 slot 2, bandage slot 3, lantern slot 4,
waterbottle 1421955155907 slot 5 — every id still present, every id in one slot, nothing retired and nothing duplicated. The GUEST's display of the same inventory lists the same six
instance ids under the host's ownership. **Row 3 PASS** for the owner half and the both-clients half.

**Limit of this row:** the drag CAPTURE half (a real mouse pressing the displayed slots) is not what this batch exercised; it is pinned by the capture tests and would need the channel
this ticket already named. Row 4 stays pinned by StandingItemGateTests (the world-transition retirement rule at every site); this batch had no world-item fixture to name, so its
live variant is a named residual rather than a weaker check.
