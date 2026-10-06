# Acceptance record — A same-frame second drop overwrites the pending report of the first

- Ticket: `drop-pending-single-slot-overwrite` — verdict: **back to `todo/`** (status field
  `- Status: Todo — Rejected (…)`): row 1's gesture aborts inside the native release body and row 2's
  monitor criterion is not met
- Batch: `20261006-f` — tickets `drop-pending-single-slot-overwrite`, `remote-inventory-native-parity-rework`
- Commit: `f53b2d20` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+f53b2d20527472b9fbd8f91cd94d6b0274638c8f`
- Run: 2026-10-06 13:53 → 14:02 local · Host: physical machine · Guest + third peer: Sandboxie sandboxes
- Dependencies: the eleven the batch's preflight reported present
- Artifacts: `20261006-f/` in the directory named by `acceptance-artifacts-dir` (the release probes, the
  diagnostic probe, the per-row byte windows, the item trees and the divergence span files are named per row
  below)

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A slot release onto an OCCUPIED destination slot drops two items inside one frame (the gesture the ticket names) | machine | **fail** | `m1-host-release-onto-occupied.json`: `calls` = `TargetInvocationException…`, `dragAfter: "Item"`, and the operator's window carries `[RemoteIntent] the release of item 13787924117 (owner 76561198863287957) produced no intent — unclassified native gesture.` (13:55:38.995). Reproduced with the ring staged (`p-f1-diag2.json`): `UnityException \| Transform child out of bounds` — `Transform.GetChild` ← `Body.GetItem` ← `Body.SlotOf` ← `PlayerCamera.TryPerformInventoryAction` ← `TryPerformUIActions` ← `HandleReleaseDragging`; `dragAfter: "bandage"`. The owner's log has no replay line for that release (`span-guest` read of the F1 mark, `LogOutput.log` fallback) |
| 2 | The reachable two-departure shape settles correctly: the item leaving the destination slot is reported and the item re-picked in the same bracket is not | machine | **pass** | F2 (`m2-host-child-onto-occupied.json`): the owner's window — `[ItemTrace] op=45 begin item=5197989525 origin=OnItemDropped event=Drop`, `[ItemTrace] op=46 begin item=18082891413 origin=OnItemUnloadedFromContainer event=Unload`, `[ContainerUnload] dogfood … left its container into the world`, then `[ItemTrace] op=46 … result=Cancelled events=[RePick]` and `[ItemTrace] op=45 … origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]` + `[ItemDropped] emergencylight (id 5197989525) at (2.3,494.1)`. Both other clients materialized it: `[ItemSpawn] materializing emergencylight (id 5197989525) at (2.3,494.1)` in the operator's and the third peer's windows, and the host's authoritative read `m4-host-tables-after.json` lists the same id in the world table |
| 3 | The operator's and the third peer's clone-fact monitor stays at zero over that gesture | machine | **fail** | `span-host-divergence.txt` / `span-alt-divergence.txt`: two `[WRN] [CharSync] divergence for 76561198863287957's trashbag (id 9492956821): nested container contents changed without an event sync` + `… dogfood (id 18082891413): a new carried item the fact table never saw — a pickup without an event sync` per viewer at 13:59:13.029/.056 (F2) and again at 13:59:47.427/.451 (F3). The owner itself logs none, and the slot-item control F4 (`m5-host-slotitem-onto-empty.json`) produces zero on either viewer |
| 4 | The machine holds one entry per item: a second departure in one frame does not swallow the first | unit | **pass** | unchanged from batch `20261006-c`: `DropPendingStateTests.ASecondDepartureInTheSameFrame_DoesNotSwallowTheFirst`, `…TwoDeparturesInOneFrame_BothSettleAndReportAfterTheFrame`, `…EnterDrop_SameItemTwice_ReplacesThatItemsOwnEntry`, `…ResetAll_ReturnsEveryOpAndClears` |

## What the run read about the row's premise

- **The gesture the ticket names does not reach the plan the ticket names.** The ticket's premise is that a
  release onto an occupied destination slot runs `RemoteIntentSlotRelease.Plan` with both slot occupants
  held and therefore issues two `Body.DropItem` calls from `RemoteIntentApplier.ApplyPickUpToSlot`. The
  viewer classifies such a release through the native R8 swap branch instead: while the remote view is open,
  `RemoteDragPredicatePatches` answers `HoldingItem(int)`, `HoldingItem(Item)`, `GetItem(int)` and
  `GetWearable` from the displayed clone (`RemoteDragIntentCapture.ShouldAnswerFromDisplayBody`), so the
  native guard at `PlayerCamera.cs:1614` sees both slots occupied and calls
  `body.SwapSlots(invButton.slot, body.SlotOf(dragItem))` — `ApplySwapSlots`, never `ApplyPickUpToSlot`
  (`RemoteDragIntentCapture.CaptureSwapSlots` is the only capture that branch can produce). `SlotOf` is what
  the run watched throw first, before any CUO seam could absorb the call.
- **The reading also corrects the row's second half.** With the reachable shape (the owner's own container
  child released onto a ring slot) the `PickUpToSlot` replay's `DropSlotItem` step really does drop the
  destination slot's occupant into the world — reported, materialized by both peers — while the dragged item
  lands in the destination slot and never reaches the world table (the native R9 outcome, `Body.PickUpItem`
  with the native guards). "Both reach the world table" is not what the game does; one departure commits and
  the other is cancelled by its re-pick.
- **The machine's own behaviour is the part that passed.** Two departures registered inside one frame each
  kept their own entry: the destination slot's occupant committed (`Committed(1) events=[Drop, Flush]`) and
  the container child's unload was cancelled by its re-pick (`Cancelled events=[RePick]`) — the single-slot
  shape the ticket replaced would have overwritten one with the other.
- **The monitor criterion failed on a carrier gap, not on the machine.** The cancelled departure sends no
  unload report, and the slot carrier's own report does not carry the container's contents change, so both
  facts reach the peers only through the periodic character snapshot and the clone-fact table says so. Filed
  as `todo/container-content-event-gap-on-repick.md` with these readings and their attribution.

## Residuals for the user

None: every row of this ticket is a machine row, and the two that failed name their evidence.

## Limits

- **One session, one owner direction, one reading per row.** The owner was the sandbox guest
  (`76561198863287957`) and the operator the physical-machine host in every row; the mirrored direction (a
  guest operating the host's inventory) was not driven, and no row was repeated except the failing release,
  which threw twice.
- **Only one of the two same-frame departures is a world release.** The reachable shape drops the destination
  slot's occupant; a shape in which BOTH same-frame departures must be reported (the dragged item's pick-up
  refused after its own drop) was not produced — the native guard admitted the pick-up in every replay this
  run drove.
- **The reference point the divergence reader needs was not isolated.** The run shows the warning follows the
  container-child route and not the slot-item route, but it did not drive a purely local container-child
  move on the owner to separate "the intent replay" from "any container-child pick-up"; that control is the
  new ticket's own acceptance item.
- **The world table was read on the host only** (`m4-host-tables-after.json`); the two sandboxes' own world
  tables were read as clone views (`d4-alt-clone-after.json`, 3 proxies, `orphanCount: 0`) plus their
  `[ItemSpawn]` lines, not as authoritative tables.
- **No wire, save or feel check.** The run read no messages, no transfer tables beyond the host's own read,
  and no saves; latency and hand-feel were not judged.
