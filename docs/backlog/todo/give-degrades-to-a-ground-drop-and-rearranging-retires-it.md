# A give degrades to a ground drop, and rearranging that item then retires it

- Status: Todo — opened 2026-10-25 from two user reports in one session: "我把东西给了别的玩家,然后在别的玩家身上给物品换位置,换完物品被吞了", and
  earlier the same chain losing a flashlight ("我拖动了手电筒…连着拖动了几次,手电筒不见了").
- Priority: Critical — an item the player gave is destroyed without reaching anybody.
- Category: Cross-player item transfer / placement ladder / item arbitration
- Source: user reports (2026-10-25) plus the host's own log from the batch that produced them.

## Design

`docs/design/mechanics/give-and-take.md` states the give's landing ladder and that a REFUSAL by the receiving side is answered and
reported, never silently degraded; `docs/design/mechanics/death-and-drops.md` and `docs/design/cross-cutting/saves-and-recovery.md`
hold the rule that an item's ownership never leaves every side at once, and `docs/design/mechanics/carry.md` the placement query the
receiver answers.

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
   row held it: the "吞了" the user reports.

## What BOTH clients' logs show (host + guest, 2026-10-25 16:25)

The give itself LANDED — the first reading above ("degraded to a drop") was wrong, and the two logs together correct it:

```
host  16:25:07.535 [Placement] asked <guest> where scrapmetal lands
guest 16:25:07.550 [Placement] answered query 2 for item scrapmetal (id …)
guest 16:25:07.590 [PickUpResult] scrapmetal → slot (slot 4)          <- the guest's own body took it
host  16:25:07.616 [StandingItem] materialized scrapmetal … for <guest>   <- the host ALSO made a world object for it
```

so the give landed in slot 4 AND a standing duplicate was materialised on the host, because the host learned of the new carried row only
from the 1 Hz snapshot (`[CharSync] divergence … a new carried item the fact table never saw`). The duplicate is what "换了位置后被吞" is
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
