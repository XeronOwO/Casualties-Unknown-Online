# An item can be operated on before its creation is registered

- Status: Review
- Priority: Medium-High
- Category: Network / sync coverage / items (creation-before-operation invariant)
- Source: User ruling 2026-09-18 (design alignment session): the 500 ms pickup hold is a design smell, not a latency fix — creation registration must always come first, and multiple messages/events may be composed into one atomic operation. The host must never execute, wait on, or guess about an operation on an item whose creation it has not yet judged.
- Related: `review/guest-command-loss-reconciliation.md`, `review/block-break-first-writer-wins.md`, `review/guest-break-drops-recovery.md`, `todo/carried-inventory-registration-re-report.md`

## Problem (evidence)

The host had a "wait and guess" window for an operation on an item it did not know:

- `KernelProtocolCommandHandler` enqueued an `ItemPickup` command whenever the named id was
  absent from the host's item table, keeping the whole envelope in `_pendingEnvelopes`.
- `PendingPickupQueue` held it for `DefaultHoldMs = 500` — a fixed guess unrelated to the
  peer's measured RTT. On expiry the host re-checked and otherwise answered
  `Rejected(UnknownAggregate)`, and the client rolled its already-completed pickup back
  (`ItemApplication.RollbackPickup`).

Item ids are PARTITIONED (the host grants each guest a watermark), so a guest-created item
carries an id the host has never seen until its registration arrives — and that
registration is not a message of its own; it rides the operation that produced the item
(a block break's drops ride `BlockDamagedMsg`, one frame after the break so the drops'
`Item.Start` folds in). The crossing is therefore created BEFORE the send: a deferred
creation report versus an immediate operation report on the same item from the same
sender.

## Goal

One global invariant, stated once and enforced everywhere:

> The host executes an operation on an item only after that item's creation has been
> judged — accepted, or refused with a remembered reason. It never executes, waits on, or
> guesses about an operation on an item whose creation is unjudged.

## What landed

**1. The rule is enforced by ORDERING on the reporting side, not by a composite batch.**
The reported race is between two DIFFERENT wire channels — the deferred creation rides the
world-event channel (`BlockBreakSync` → `SendBlockDamaged`, `EntityEventSync`,
`ItemWorldSync.FlushPendingDrop`) while the operation rides a kernel command envelope — so
one judged datagram cannot carry both halves. What the invariant needs is that the creation
report leaves first, and that is what the sender now guarantees:

- `PendingItemCreations` (Runtime) is the gate. `ItemMessageFlowService` settles it at the
  top of every OPERATION report — `SendItemPickedUp`, `SendItemUse`, `SendItemSlot`,
  `SendItemContainerContent`, `SendItemDropped`, `SendItemDestroyed` — and not for the
  creation reports (`SendItemSpawned`, `SendItemCooked`, `SendCarriedInventory`).
- `IPendingItemCreationSource` is implemented by the Game Adapter
  (`PendingItemCreationReports`: the drop flush, the trap building-death drop flush, the
  block-break flush) and registered at composition in `GameAdapter`, so the Runtime owns
  the rule while the adapter owns the knowledge of what it is holding back.
- The gate is guest-only (the host never reports an operation to itself) and is ordering,
  never a window: it waits for nothing and measures no time. A creation still deferred at
  that instant cannot be named by the operation — the item has no instance id on the
  reporting side until the frame whose end carries its report.

**2. The host holds nothing.** `PendingPickupQueue`, `PendingPickupPump`,
`_pendingEnvelopes` and the whole expiry path are deleted (also `PumpPendingPickups` /
`PendingPickupCount`, the DI registration and the cut's `pickup-queue` transient row). An
operation whose creation the host has not judged is refused AT ONCE: with the remembered
reason when the creation was refused, and otherwise as a logged protocol violation
(`KernelProtocolCommandHandler.TryRefuseUnjudgedOperation`). `ItemDrop`, `ItemTransfer`,
`ItemDestroy` and `ItemUpdateState` go through the same check, so the rule is not a pickup
special case.

**3. A refused creation leaves a tombstone.** `RefusedItemCreations` (bounded, oldest
evicted first; cleared at session end because ids are session-partitioned) remembers the id
and the reason. It is recorded where the host refuses a creation: `SendItemReject` (a break
that lost first-writer-wins destroys its drops) and a rejected `ItemSpawn` command. A later
operation on that id is answered with that reason — precisely, immediately, and never
confused with "the creation was never reported".

**4. The carry-registration heal is an EXPLICIT EXCEPTION to this invariant, not a use of
it** (adversarial review 2026-09-19). Two paths keep their behaviour, and both are now
documented as exceptions instead of silently bypassing the rule:

- `HandleMissingCarriedUpdate` (an `ItemUpdateState` for an id the host has never judged)
  adopts the reporter's carried fact: the report IS the creation judgement for that item
  (derived from the operation instead of from a prior registration), and then the operation
  runs. Nothing is waited for.
- `ItemContainerSync` for an unknown parent is answered by the kernel the same way — it
  MATERIALIZES the parent as the reporter's own carried item (`ItemDomainModule`'s
  `DecideSyncContainer`) and then applies the contents. This kind is therefore deliberately
  NOT in `IsItemOperation` on the judging side (`KernelProtocolCommandHandler`), while
  `SendItemContainerContent` still settles on the sending side: the sender's ordering is
  right either way, and the judging side keeps the heal. The earlier wording here ("keeps
  its kernel verdict") was wrong — the kernel's verdict IS the materialization.

Both exist because a swallowed `CarriedInventory` report leaves the host with no
registration for a guest's own carried items, and
`todo/carried-inventory-registration-re-report.md` pins them (its acceptance matrix row
"Accepted-first path still works"). What the host takes on trust is that the item belongs to
the REPORTER; the wire carries no proof of that, and that trust boundary is owned by the
heal ticket and by `future/strict-validation-anti-cheat.md` — not by this one. A refused
creation on either path is still answered with its precise reason.

**5. Protocol bumped 26 → 27** (`ProtocolVersion.Current`, `docs/decisions/active.md` #137,
`docs/api/mod-api.md`): the judging behaviour of an operation on an unknown item changed,
and a mixed session would have one side waiting on a window the other no longer fills.

## Verification

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1 | Guest creates an item, then operates on it | The host judges the creation first; the operation needs no wait | `PendingItemCreationsTests.AGuestSettlesItsDeferredCreationsBeforeItReportsAnOperation`, `...EveryOperationReportSettles_TheWholeOperationFamily`; the ordering is by construction inside `ItemMessageFlowService` |
| 2 | The creation is refused | Every later operation on that id is refused immediately with the precise reason | `CreationBeforeOperationTests.AnOperationOnARefusedCreation_IsAnsweredWithThePreciseReason`; `RefusedItemCreationsTests` (record/replace/bound/reset) |
| 3 | Creation and operation in flight together | Judged as one unit: the creation's verdict governs the operation (all-or-nothing outcome) | `CreationBeforeOperationTests.TheLateCreationStillRegisters_TheUnjudgedRefusalIsNoTombstone` + row 2 — the literal one-datagram reading was NOT adopted, see What landed §1 |
| 4 | Two senders, one item | No "unknown item" window is needed to order them | A second sender only sees the item after the host relayed the creation, so the host has judged it already; `ItemRaceTests.PickupRace_G2First_G2Wins` unchanged |
| 5 | Reconnect / late join | Unchanged semantics; the tombstone is not resurrected | `RefusedItemCreationsTests.AnIdLessReport_IsNeverRemembered_AndTheSessionEndForgetsTheRest` (session end clears the table) |
| 6 | Unknown-item operation reaching the host | Recorded as a protocol violation, not silently held | `CreationBeforeOperationTests.AnOperationOnAnUnjudgedItem_IsRefusedInTheVeryFrame_NotHeld` (the answer exists before a single frame is pumped); the log line is `Protocol violation: {Sender} reported {Kind} on item {ItemId} whose creation this host has never judged.` |
| 7 | Regression: generation-time items, container contents, drops, crafting, trade | Unchanged outcomes | Replays `pickup-spawn-inflight.replay` (rewritten to the new semantics), `ItemReplayTests` over the whole item replay archive; `ItemRaceTests` seeded lifecycle oracle (queue removed from the model); focused run 60/60 and the full suite |

## Known coverage gaps (declared, not silently carried)

- The loud half of the protocol violation is an `Error` log line
  (`Protocol violation: {Sender} reported {Kind} on item {ItemId} whose creation this host
  has never judged.`). No test asserts the log itself — the on-the-wire refusal is what is
  pinned.
- The tombstone recorded when the KERNEL refuses an `ItemSpawn` command has no separate
  assertion: the client-side mapping (`ItemService.OnCommandRejected`) collapses every
  reason except `BlockAlreadyBroken` into `ItemRejectMsg.Reason.UnknownItem`, so that path
  is wire-indistinguishable from the plain unjudged refusal. Its observable value is the
  log line plus the precise reason carried in the envelope.
- The carry-registration heal's trust boundary (a peer may claim any unjudged id as its own
  carried item) is deliberately unchanged here and belongs to
  `todo/carried-inventory-registration-re-report.md` / `future/strict-validation-anti-cheat.md`.
- Dual-client acceptance stays the user's release-cycle action: the second operator's
  screen, real two-machine latency, and the production live-capture branch are not covered
  by the simulation harness.

## Non-goals

- Changing the partitioned id allocation (it is sound; only the ordering rule was missing).
- Host arbitration of conflicting claims (user ruling: it stays host-authoritative).
- Making the transport ordered (it already is).
