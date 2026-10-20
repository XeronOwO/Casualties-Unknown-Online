# The Item Fact Before the Character Snapshot — Self-Check (2026-10-20)

Delivery fact sheet for backlog item `review/snapshot-overtakes-item-reports.md`: the three inventory
gestures report their item fact before the immediate character snapshot, so the peers' clone fact table
applies the event first and the snapshot that follows has nothing to warn about.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `WearWearablePatch.Postfix` called `OnInventoryChanged` before its `OnItemWorn` / `OnItemPickedUp` report. | `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs` — the snapshot call now follows the fact block; the craft-scope branch is the sibling ticket named in row 8. |
| 2 | `SwapSlotsPatch.Postfix` called the snapshot before its two `OnSlotMoved` calls. | `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyItemPatches.cs` — the two slot facts, then the snapshot. |
| 3 | `SwitchHandsPatch.Postfix` did the same for the two hand slots. | `Patches/BodyItemPatches.cs` — same order. |
| 4 | The entry point's contract: the caller reports the fact first and the snapshot second; the pending-drop hold is asked of the item domain's own state and stays. | `GameAdapterBridge.OnInventoryChanged`, `IPatchBridge.OnInventoryChanged`; `RemoteIntentReportOrderGateTests` (the hold's own gate) stays green. |
| 5 | The peer-side observer: `CloneFactTable.ApplySnapshot` warns only against the table its events updated, and `ApplyCarriedSync` updates it. | `Character/CloneFactTable.cs`; `CloneFactTableDivergenceMonitorTests` (event-before-snapshot is quiet; snapshot-without-event warns). |
| 6 | The transport preserves the order: both sends use the reliable ordered channel (Steam channel 0 / TCP), and the host dispatches a received command and broadcasts its committed batch synchronously before it relays the snapshot. | `PacketSender`, `SteamTransport.SendTo`, `KernelProtocolService.SendCommand`, `KernelProtocolCommandHandler.Handle`, `CharacterDataHandler`; no separate lane and no per-frame batching between the two sends. |
| 7 | Six `OnInventoryChanged` callers exist in total; the other three send their fact first or are covered by the pending-drop hold. The `ReportInventoryChanged` receiver-side sites send no item fact (the host/kernel commit is the fact). | the ticket's "The fix (this cycle)"; `CraftingPatches` (report before snapshot), `DropWearablePatch` (pending-drop hold), `RemoteIntentApplier` (carriers inside the native call). |
| 8 | The craft-scope wear is NOT this ticket: the in-craft snapshot precedes the craft report, and the report retires only world rows from the peers' fact table. | `docs/backlog/todo/craft-carried-fact-retirement.md` (filed from the review with the code path and its acceptance row). |

## 2. The change

- `Patches/BodyPatches.cs`: `WearWearablePatch.Postfix` sends `OnItemPickedUp` / `OnItemWorn` before
  `OnInventoryChanged`.
- `Patches/BodyItemPatches.cs`: `SwapSlotsPatch` / `SwitchHandsPatch` send their two `OnSlotMoved` calls
  before `OnInventoryChanged`.
- `GameAdapterBridge.OnInventoryChanged` and `IPatchBridge.OnInventoryChanged` state the caller order in
  the words a future caller reads.
- `tests/CasualtiesUnknownOnline.Tests/Patching/InventoryChangeReportOrderPinTests.cs`: a comment-aware
  source-shape pin on the three postfixes, with positive, inverted and comment-only samples.

## 3. The independent review, and what it changed

NARROWED-tier adversarial review, fresh context, frozen tree. Its findings and their disposition:

| Finding | Disposition |
|---|---|
| MAJOR: the ticket's "the craft-scope wear is benign" is not supported — the in-craft snapshot still overtakes the craft report, and the report retires only world rows, so a destroyed carried material keeps its fact. | The ticket sentence is corrected; the defect is filed as `docs/backlog/todo/craft-carried-fact-retirement.md` with the code path and its acceptance row, and the code comment points at it. Not fixed here: its proof needs a craft fixture and a fact-table removal test this cycle's batch does not cover. |
| MINOR: stale `todo/` pointers in five done records. | All ten references updated (`done/pickup-early-return-kernel-relocation.md`, `done/drop-pending-single-slot-overwrite.md`, `done/remote-inventory-native-parity-rework.md`). |
| MINOR: the pin stripped only whole-line comments. | `StripComments` removes line, trailing and block comments while keeping string/char literals; a test feeds it a body whose fact call lives only in comments. |
| MINOR: the audit sentence named `ReportInventoryChanged` sites as `OnInventoryChanged` callers. | Rewritten: six `OnInventoryChanged` callers, the receiver-side sites named as the `ReportInventoryChanged` family. |
| NIT: the ticket did not say the acceptance row is unrun. | The bullet now says the row is unrun until the batch that follows the commit. |
| NIT: the bridge summary said both batches fired the pair. | Corrected: `20261005-e` read the drop's single line, `20261025-b` the pair. |
| NIT: `IPatchBridge`'s first clause named only SwapSlots/SwitchHands. | Now names the whole change family (slot move, wear, pickup, craft report). |

## 4. What this cycle did not verify

- The runtime half: the ticket's three-client acceptance row (the batch after the commit) — the member
  wears a bag child, then the hand swap and the `SwapSlots` swap over occupied slots, and every viewer's
  window must stay at zero divergence with the carrying facts applied.
- The host-owner direction was not driven: the same patch code runs on both roles (the role only chooses
  `SendItemCarriedSync` vs `SendItemSlot`), and `20261006-g`'s host-owner reading was the pre-fix shape.
  A host-owner repetition is useful but is not this row.
- The craft path (filed as its own ticket) and the `EnsureId`-fails-during-generation path (an id-less
  guest item reports no fact; the monitor skips id 0 by design).
