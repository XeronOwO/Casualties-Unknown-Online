# Dragging an item out of a displayed backpack to USE it dropped it on the ground instead

- Status: Todo — opened 2026-10-25 from the user's report (translated): "I handed the emergency flashlight to the guest, dragged it in the guest's
  backpack to use it, and the flashlight fell out — the use did not succeed".
- Priority: Critical — the player asks for a USE and the item leaves the inventory.
- Category: Cross-player item use / drag release classification
- Source: user report (2026-10-25), reproduced in the batch that was still running (`.acceptance/batch-menuclick`, host log).

## What the log says (host, 2026-10-25 20:59–21:00)

```
20:59:22.033 [RemoteIntent] DropItem captured for item 1065472870339 of 76561198863287957 (container 0, slot -1, body …)
20:59:22.105 [StandingItem] retired emergencylight (id 1065472870339, owner 76561198863287957)
20:59:22.113 [ItemSpawn] materializing emergencylight (id 1065472870339) at (-0.2,500.8) …
21:00:05.406 [PickUpResult] emergencylight → slot (slot 3) at (-5.9,501.6)     ← the item was picked back up
```

So the item was owned by the guest (the host was operating the guest's displayed backpack), the release was classified as **`DropItem`** — the
world fallback W2 (`body.DropItem`) — and the item became a world object. No use intent was ever captured.

## Why that classification is the defect

The user's gesture was "drag it out and use it": the native path for that is R10 — a release inside an OPEN radial circle
(`PlayerCamera.cs:1640`: `body.WearWearable` / `body.UseItem`, consumed either way). A release that reaches the world fallbacks instead means the
R10 branch did not consume it, and there are exactly two ways that happens:

1. **The radial was not open** (the native release only reads the centre when `this.radialOpen`), so the gesture really was a drop — the player's
   expectation and the control scheme disagree, which is a UX finding, not a classification bug;
2. **The radial was open and the pointer was inside the circle, yet the centre branch did not answer** — the capture bracket's displayed-body
   predicates (`RemoteDragQuerySeamGateTests`' seam) or the radial probe (`PlayerCameraRadialActionProbePatch`) is wrong for a proxy drag, which is a
   classification bug in the design's own stage 3.

The two are told apart by one reading: whether `PlayerCamera.radialOpen` was true at the release, and whether the radial probe recorded a consumed
release. Neither is in the log today, which is why this ticket opens with the log line rather than a cause.

## What the fix must not do

- It must not "use the item on a drop": the drop was the game's own answer to the gesture it classified, and the item was recoverable (it was picked
  back up). The fix is to make the intended gesture reach R10, or to make the control scheme's requirement visible — never to guess the use from a
  drop-shaped release.
- The item survived, so nothing about this needs a repair path; it needs the classification to be right.

## Design

Read before the change: `docs/design/surfaces/member-menu.md` (the pointer rule) is not the surface here; the one that applies is the drag/radial
half of `docs/design/mechanics/give-and-take.md` plus the capture-bracket rules on `docs/design/mechanics/remote-inventory-entries.md` — R10 is the
release the player expected, and the bracket's displayed-body answers are what R10 needs to see the proxy. The record this ticket was opened from is
the user's two reports and the host log quoted above.
## Acceptance rows

| Row | The check |
|---|---|
| 1 | With the radial OPEN and the pointer inside the circle, releasing an item dragged from a displayed backpack produces a USE intent (`UseItem`/`WearItem`), not a drop — read from the captured intent plus the owner-side apply |
| 2 | With the radial CLOSED (or the pointer outside it), the same release keeps the world drop it has today, and the two cases are distinguishable in the log |
| 3 | The item never leaves the owner's inventory when the intent was a use, read from the custody projection on both clients |

## Limits

- The batch that saw this had no radial state in its log; the first run of this ticket adds that reading, not a rerun of a guess.
- Which of the two causes is true decides whether this is a code fix or a design/UX decision, and the user is the one who knows what they expected to
  see on screen at that moment.
