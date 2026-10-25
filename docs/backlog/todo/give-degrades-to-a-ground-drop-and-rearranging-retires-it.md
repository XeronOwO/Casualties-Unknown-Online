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
