# Dragging an item out of a displayed backpack to USE it dropped it on the ground instead

- Status: Review — the use works on the machine (user, 2026-10-25); the ring-closed row is unjudged — opened 2026-10-25 from the user's report (translated): "I handed the emergency flashlight to the guest, dragged it in the guest's
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
| 2 | With the radial CLOSED (or the pointer outside it), the same release keeps the world drop it has today, and the two cases are distinguishable in the log | **PARTIAL 2026-10-27**: the two cases ARE distinguishable — the outside release logs `[DragFlow] release fell through to the WORLD path (no UI target hit)` and `the release of item 9492956821 … produced no intent — unclassified native gesture`, against the inside release's `UseItem captured`. **But the world-drop half is NOT met with the real pointer**: the proxy is not dropped at all (its row stays `Carried` with its owner), so "the world drop it has today" does not exist outside the synthetic cast. The earlier `DropItem captured` evidence for the same release came from `remote-gesture`'s SYNTHETIC `uiCasts` — the instrument this batch replaced — so that judgement was made on a weaker tool and is withdrawn here |
| 3 | The item never leaves the owner's inventory when the intent was a use, read from the custody projection on both clients | **PASS 2026-10-27**: `item-row` for 13787924117 after the use intent reads `{"kind":"Carried","owner":"<guest>","parent":0,"slot":-1,"revision":2}` — it never left its owner's inventory |

## Limits

- The batch that saw this had no radial state in its log; the first run of this ticket adds that reading, not a rerun of a guess.
- Which of the two causes is true decides whether this is a code fix or a design/UX decision, and the user is the one who knows what they expected to
  see on screen at that moment.

## The visible half is fixed and judged by a frame (2026-10-25, `.acceptance/batch-radial`)

The cause was one line: `RemoteBackpackView.AnchorRadialToFocus` disabled `radialCircle` for the whole remote view, which hid the game's own highlight
(`radialCircle.color`, `PlayerCamera.cs:1791-1800`) AND removed the `RadialCenter` tag from every UI raycast, so R10 could never consume the release.
It now enables it — the rationale that required disabling was written while R10 was suppressed, and stage 3 un-suppressed R10.

Deployed and read back with the remote backpack open on the host: `.acceptance/frames/radial-enable.png` shows the focused clone's native ring drawn
around it — the body-slot ring plus the INNER USE CIRCLE the user reported missing — where the previous build drew no circle at all.

What the same batch could NOT judge, and why: the interaction half. `drag-release` reported `mode=DisplayedBackpack`, `backpackOpen=True`,
`centreUnderPointer=False` with `centreDistance=3021`, `centreRadius=135` — and that distance is measured against `Input.mousePosition`, i.e. the REAL
OS pointer, which the in-process driver never moves. The reading therefore says nothing about a player's drag; the centre test has to be exercised by
a real drag, which is the user's next test.

**The interaction half is now judged by the user** (2026-10-25, same session, after the deploy): "it works normally now" — dragging an item out of the
displayed backpack onto the ring's centre USES it. The enable restored the whole chain: the circle is drawn, the pointer on it lights the highlight,
R10 consumes the release, and the owner replays the use.

Rows 1 and 3 are therefore judged (a use happens, and the user reports no drop). Row 2 — the ring CLOSED case — is still unjudged, and that is what
keeps this ticket in `review/` rather than `done/`.

## What the in-process release can and cannot drive (2026-10-26)

With `remote-gesture` composed (`mode=open` → `mode=list` → `mode=release`), a **slot** destination works end to end: the host gave a lantern into the
guest''s backpack and `<guest> takes lantern … from <host>` followed. The **centre** (the use target) does not: three attempts — `cast=-2`, a `mode=hover
cast=0` first, and a fresh `mode=open` immediately before — all answered
`the release of item … produced no intent — unclassified native gesture`, so `DragUseCenter.IsUnderPointer` is not satisfied by the staged pointer the
recipe can move. Rows 1 and 2 therefore still need either a recipe that stages the real pointer inside the centre circle, or a hand on the mouse.

## The pointer seam is deployed and the centre still classifies as NO-OP (2026-10-26 21:52)

`DragUseCenter` now reads a staged pointer (`StagePointer`, cleared after one release), and with it staged at the live radial circle's own position the proxy
release STILL answered `the release of item 18082891413 … produced no intent — unclassified native gesture`. The probe taken right after reads
`viewOpen=true radialOpen=true ringScale=1 uiScale=0.667 tag=RadialCenter radius=135`, so the ring is open and finished opening.

That points the next read at `ClassifyNoOp` / `RemoteDragIntentWindow.Open` — the classification is taken when the window OPENS (in the patch''s prefix),
and something in it answers no-op even though the centre guard''s own inputs look satisfied. The candidates, in the order worth reading: the ring''s scale
at the moment the classification runs (it animates), whether the classification runs before the staged pointer is visible to it, and whether the dragged
proxy object is the one `DragUseCenter` measures against.

## The world-drop half is a real gap, not a wording problem (2026-10-27, virtual pointer)

With the pointer OUTSIDE the ring, the release ends the drag and produces NO intent — the proxy stays with its owner and never lands. The window captures
nothing because the native world-drop steps are gated on the dragging body HOLDING the item, and a display proxy is not held by anyone; the synthetic
`uiCasts` release that produced `DropItem captured` earlier fired a different path.

So the next work item is the one this row names: a proxy released in the WORLD must become the take-and-drop the player performed — the same intent a real
take-out plus drop would produce — instead of nothing. Until it exists, the honest reading of row 2 is "distinguishable, and the world half is missing".