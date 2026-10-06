# Acceptance record — Remote inventory operations: run the native path end to end

- Ticket: `remote-inventory-native-parity-rework` — verdict: **back to `todo/`** (status field
  `- Status: Todo — Rejected (…)`): row 6's swap half fails on the deployed artifact; rows 7 and 8 stay
  undriven
- Batch: `20261006-f` — tickets `drop-pending-single-slot-overwrite`, `remote-inventory-native-parity-rework`
- Commit: `f53b2d20` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+f53b2d20527472b9fbd8f91cd94d6b0274638c8f`
- Run: 2026-10-06 13:53 → 14:02 local · Host: physical machine · Guest + third peer: Sandboxie sandboxes
- Dependencies: the eleven the batch's preflight reported present
- Artifacts: `20261006-f/` in the directory named by `acceptance-artifacts-dir`

## The rows this batch drove

| # | Row (matrix row) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 6a | Slot move onto an EMPTY destination slot: the owner's body ends with the item in that slot | machine | **pass** | F4: `m5-host-slotitem-onto-empty.json` — `PickUpToSlot captured for item 22377858709 of 76561198863287957 (… slot 2 …)`; the owner's window — `[ItemTrace] op=49 begin … OnItemDropped event=Drop`, `op=49 … Cancelled events=[RePick]`, `[SlotMoved] bandage (id 22377858709) → slot 2 (Drag) reported.`, `[PickUpResult] bandage → slot (slot 2)`, `[RemoteIntent] item bandage now holds the owner's slot 2.` Zero `divergence` lines on either viewer |
| 6b | Slot move onto an OCCUPIED destination slot: the swap runs on the owner's body | machine | **fail** | F1: the release aborts inside the game's own release body — `m1-host-release-onto-occupied.json` (`calls`: `TargetInvocationException…`, `dragAfter: "Item"`, no intent) and the staged reproduction `p-f1-diag2.json`: `UnityException \| Transform child out of bounds` — `Transform.GetChild` ← `Body.GetItem` ← `Body.SlotOf(Item)` ← `PlayerCamera.TryPerformInventoryAction` ← `TryPerformUIActions` ← `HandleReleaseDragging`, `dragAfter: "bandage"`. Nothing reaches the owner: no `[RemoteIntent]` replay line and no item change in its tree (`d1-guest-tree-after-f1.json` differs from `d0-guest-tree.json` only by the item a later world-path release dropped) |
| 4/6c | A container child released onto a ring slot of its owner lands there and the item that left the destination slot reaches the world on every client | machine | **pass with a monitor finding** | F2/F3: the child lands (`[PickUpResult] dogfood → slot (slot 3)` / `(slot 5)`, `[SlotMoved] dogfood → slot …`), the occupant leaves (`[ItemDropped] emergencylight (id 5197989525)`, `[ItemSpawn] materializing emergencylight` on both viewers) — but two `[CharSync] divergence` lines appear per viewer per gesture (see the sibling record; filed as `todo/container-content-event-gap-on-repick.md`) |
| 7 | Use / wear / combine / battery load-unload / favourite | machine | **unproven — not driven** | the battery-load half, the combine half and the favourite sub-kind keep the limits batch `20261005-e` recorded; this session staged no battery receiver, no combineable pair and no favourite key |
| 8 | Held remote item used from the medical panel | machine | **unproven — not driven** | this session staged no treatable limb fixture and no wound-view staging |

## What this run says about the family

- **The slot-move half of row 6 keeps working** through the `PickUpToSlot` replay, in the shape batch
  `20261005-e` and `20261006-a` read (empty destination, one drop-then-re-pick pair, no divergence).
- **The swap half of row 6 cannot run at all on this build.** `PlayerCamera.TryPerformInventoryAction`'s
  R8 branch is reached for a release onto an occupied slot of the displayed clone — that much the run
  confirms, because the throw happens in that branch's own argument list — and it aborts there:
  `Body.SlotOf(item)` walks `Body.GetItem(i)`, whose `HoldingItem(i)` is answered by the displayed clone
  while the method body still indexes the LOCAL body's slot transform, so an empty local slot makes
  `Transform.GetChild(0)` throw. The whole release is lost with it (no intent, no refusal line) and
  `HandleReleaseDragging` never reaches its own `dragItem = null`, so the drag stays staged until the ring
  closes.
- **The same proxy released over the world instead of over a ring button still works** — the diagnostic
  run's ring-less release classified as `DropItem` and the owner replayed it: `[RemoteIntent] replayed native
  DropItem on item 13787924117`, `[ItemDropped] bandage (id 13787924117) at (4.8,493.0)`,
  `op=43 … Flush result=Committed(1)`. The failure is specific to the ring-button path, not to the capture
  seam or to the owner-side replay.
- **The container-child route's carried facts are under-carried** (the sibling record's rows 2/3 and the new
  ticket).

## Residuals for the user

None: every row this batch drove is a machine row. The rows it did not drive (7's battery/combine/favourite
halves and 8's medical row) are named as unproven rather than handed to the user, and their fixtures are the
development work the rejection reopens.

## Limits

- **One owner direction and one reading per row**, as the sibling record states: the owner was the sandbox
  guest, the operator the host; F2/F3 were the two configurations driven, F4 the control, F1 the failure
  (twice).
- **The failing branch's own fix is not designed here.** The record names the frame that throws and the seam
  asymmetry behind it (`HoldingItem` redirected, `GetItem`'s body not), which is the input a fix needs; the
  shape of the fix is the development cycle's decision.
- **No visual or feel judgement.** The frames `w1`–`w3-host-*.png` record the ring and the clone before and
  after the gestures; whether the failed swap reads as a dead gesture in the hand was not judged (the run
  read `dragAfter` and the ring state instead).
- **No wire or save check**: the run read no messages and no saves.
