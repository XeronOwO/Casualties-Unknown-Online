# An item can be operated on before its creation is registered

- Status: Todo
- Priority: Medium-High
- Category: Network / sync coverage / items (creation-before-operation invariant)
- Source: User ruling 2026-09-18 (design alignment session): the 500 ms pickup hold is a design smell, not a latency fix — creation registration must always come first, and multiple messages/events may be composed into one atomic operation. The host must never execute, wait on, or guess about an operation on an item whose creation it has not yet judged.
- Related: `todo/guest-command-loss-reconciliation.md`, `review/block-break-first-writer-wins.md`, `review/guest-break-drops-recovery.md`

## Problem (evidence)

The host has a "wait and guess" window for an operation on an item it does not know:

- `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolCommandHandler.cs`
  enqueues an `ItemPickup` command whenever the named id is absent from the host's item
  table (`_authority.FindItem(...) is null`), keeping the whole envelope in
  `_pendingEnvelopes`.
- The window is a fixed guess: `src/CasualtiesUnknownOnline.Runtime/Session/Items/PendingPickupQueue.cs`
  `DefaultHoldMs = 500` (the timeout is unrelated to the peer's measured RTT, which the
  session already tracks per member). On expiry the host re-checks and otherwise answers
  `Rejected(UnknownAggregate)`, and the client rolls its already-completed pickup back
  (`src/CasualtiesUnknownOnline.GameAdapter/Items/ItemApplication.cs` `RollbackPickup`:
  "the item leaves the inventory back into the world, at the position it was picked up from").

Why the id can be unknown at all: item ids are PARTITIONED — the host grants each guest a
watermark, so a guest-created item carries an id the host has never seen until its
registration arrives. That registration is not a message of its own; it rides the operation
that produced the item:

- `src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs`
  `FlushPendingBlockBreak` registers the drops and sends ONE `BlockDamagedMsg` carrying
  break + drops ("break + drops = ONE message, one verdict"), one frame after the break
  ("the report holds one frame so the drops' `Item.Start` folds in"); the guest records its
  pending drop set before that send.

Two corrections to the earlier reading of this window, both established in the alignment
session and to be carried into the fix:

- The crossing is NOT a wire reorder. Reliable transport is ordered (Steam reliable sends
  in order; `src/CasualtiesUnknownOnline.Runtime/Networking/IpDirectTransport.cs` carries
  every frame over TCP), so an inversion has to be created BEFORE the send (the deferred
  drop report versus an immediate pickup report) or to come from two different senders,
  whose reports have no mutual order.
- The window conflates two different facts: "the creation is still in flight" and "the
  creation was refused / is dead". In the second case the host already knows the answer and
  still waits 500 ms, then answers with a less precise reason.

## Goal

One global invariant, stated once and enforced everywhere:

> The host executes an operation on an item only after that item's creation has been
> judged — accepted, or refused with a tombstone. It never executes, waits on, or guesses
> about an operation on an item whose creation is unjudged.

When it holds, the 500 ms window has no reason to exist and is deleted rather than kept as
a fallback.

## Design direction (decide at implementation)

1. Creation registration becomes its own step that precedes every operation on the item:
   either a separate message or a batch member ordered ahead of the operation, with the
   creation carrying the spawn position while the remaining state follows on the state
   path (the repo already reconciles absolute state afterwards).
2. The two halves commit atomically, so "one logical operation = one atomic committed
   batch" (the architecture rule) is kept without packing everything into one datagram:
   the creation and the operation(s) it precedes travel in one judged unit, and a refused
   creation refuses the operations in the same batch.
3. A REFUSED creation leaves a tombstone (the enemy domain's `Removed` terminal fact is the
   existing precedent), so a later operation gets an immediate precise refusal instead of a
   window.
4. Delete `PendingPickupQueue` / `_pendingEnvelopes` and their tests once the invariant is
   in place; a surviving "unknown item operation" path must fail loudly as a protocol
   violation.
5. Protocol version bump (behavior-changing wire/composition change) and an item-family
   regression pass: generation-time items, container contents, break/building drops,
   crafting, trade.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest creates an item, then operates on it | The host judges the creation first; the operation needs no wait |
| 2 | The creation is refused | Every later operation on that id is refused immediately with the precise reason |
| 3 | Creation and operation in one batch | Judged as one atomic unit (all or nothing) |
| 4 | Two senders, one item | No "unknown item" window is needed to order them |
| 5 | Reconnect / late join | Unchanged semantics; the tombstone is not resurrected |
| 6 | Unknown-item operation reaching the host | Recorded as a protocol violation, not silently held |
| 7 | Regression: generation-time items, container contents, drops, crafting, trade | Unchanged outcomes |

## Non-goals

- Changing the partitioned id allocation (it is sound; only the ordering rule is missing).
- Host arbitration of conflicting claims (user ruling: it stays host-authoritative).
- Making the transport ordered (it already is).
