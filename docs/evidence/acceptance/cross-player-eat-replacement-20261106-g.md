# Cross-player eat: the replacement lands in the owner's slot (2026-11-06, batch `20261106-g`)

Deployed artifact: `0.1.0+39300598bf40d11fefec8339a82cb80f854c0343` (acceptance build, verified against this tree).
Clients: host `76561198281246659` (port 18590) and guest `76561198863287957` (port 18591) in one world, lobby
`109775243630919022`. Instruments: `drive-in-process.ps1` for the lobby/console/state reads, the host's
`BepInEx/logs/latest.log` for the chain, and the user's own hand for the drag (the display clone carries no colliders, so
the agent cannot drive the pointer onto a teammate).

## What was fed

The host received one closed stone fruit (`/give stonefruitclosed` → `stonefruitclosed（#1245861496771）` placed in the
first slot; the answer is rendered in Chinese and translated here). The user dragged it onto the teammate's body and
released; the eat ran on the eater's client, as the design requires.

## The defect this batch re-read

On `0.1.0+e0ffff58` the same gesture produced:

- `[PickUpResult] stonefruitopen → world (slot 0) at (4.0,494.4)` — the replacement was refused by the owner's own slot
  and dropped on the ground (what the user saw: a peeled fruit falling from their body);
- `[ItemTrace] op=5 … origin=OnItemDestroyed result=Committed(1) events=[Destroyed]` — the old object's deferred destroy
  committed a terminal row for an id that still had an object;
- the repeating `[ItemCustody] rows=317 objects=317 mismatches=1 — the first ones: 1323170908099:TerminalRowWithObject:Terminal`.

## The reading after the fix (`39300598`)

```
20:12:17.555 [ItemUse] 76561198281246659 offers stonefruitclosed (id 1245861496771) to 76561198863287957 — the eat runs on the eater's own client.
20:12:17.559 [DragUse] used stonefruitclosed on 76561198863287957 (instance 1245861496771).
20:12:17.602 [ItemUse] 76561198863287957 ate stonefruitclosed (id 1245861496771) of 76561198281246659; condition 0.00.
20:12:17.602 [ItemUse] local item 1245861496771 condition set to 1.00 by cross-player use.
20:12:17.604 [ItemUse] 76561198863287957 replaced stonefruitclosed (id 1245861496771) … with stonefruitopen in place (the admission's one follow-up report).
20:12:17.605 [PickUpResult] stonefruitopen → slot (slot 0) at (4.0,493.3).
20:12:17.605 [ItemUse] local item stonefruitclosed (id 1245861496771) became stonefruitopen in place at the same slot.
```

`[ItemTraffic]` at 20:12:26 read `Spawn=0; Drop=0; Destroy=0`, and no `[ItemCustody] … TerminalRowWithObject` line
followed. The pickup now names the slot instead of the world, and nothing reports the item's end.

## The user's judgement

The user was the witness for the visible rows and confirmed the result: the fruit in their own backpack becomes the peeled
one in place, nothing drops to the ground, and the teammate's backpack shows no flash of the replacement.

## Limits

The full-inventory variant of the feed (a teammate with no free hand slot) and a captured frame at the eating moment were
not re-read in this batch; the ticket keeps them in its Limits section.
