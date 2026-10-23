# The placement failure report has no anchor

- Status: Todo — half landed. The APPLIED arm is anchored (the receiving client reports the place its own placement
  used, the host compares it with the row it wrote and refuses anything else by name); the NOT-APPLIED arm — the
  failure claim this ticket was filed for — still reverses custody on the receiver's word, and per decision 265 it
  stays named rather than given a behavioural bound. Row 1's machine reading was attempted in batch `20261023-d`
  (the drag could not be started; record `docs/evidence/acceptance/placement-failure-report-anchor-20261023-d.md`)
  and `20261023-e` (the give ran and the receiver placed the item, but no outcome reached the host: the kernel wire
  dropped the two flags, fixed with this ticket's commit). **Row 1 now reads on the fixed artifact**: batch
  `20261023-f` saw the receiver's `[Placement] item …: placed it at slot 1 — reporting it to the host.` and the
  host's `[Placement] item … is where this host wrote it (slot 1): the placement outcome matches, nothing moved.`
  (record `docs/evidence/acceptance/placement-failure-report-anchor-20261023-f.md`). The ticket stays open for the
  NOT-APPLIED half, which is undeveloped work.
- Priority: Low-Medium
- Category: Cross-player custody / member-report bounds / anti-duplication
- Blocked by: nothing external. The remaining half needs an anchor for a NEGATIVE claim ("my live body placed
  nothing"), which is another client's own frame; nothing in the host's records can establish it.
- Source: The independent review of `fb782e5c` (2026-10-22, finding 6) on
  `docs/backlog/review/placement-outcome-never-returns.md`: the host can bound the transactional half of the
  report (a committed give to that sender, the item in its snapshot) but not the claim itself, so the census row
  for `NetMsg.PlayerPlacementFailed` was `unbounded`.
- Related: `review/placement-outcome-never-returns.md` (the design whose residual this bounds),
  `docs/architecture/member-report-validation.md` (the census row), decisions 184/265 (judgement belongs to the
  affected client; a report with no data anchor is named rather than given a behavioural bound),
  `docs/evidence/sync-coverage-matrix.md` (row P14).

## Problem (evidence)

The receiver's failure report said "the destination I answered is gone" and carried only the item id and a reason.
The host's own records prove the item was SENT there — which is equally true after a successful placement — so it
could not check the claim. A receiver that keeps its materialized copy and sends the report anyway makes the host
remove the row from its snapshot and write a copy on the giver while the object stays on the receiver; the
receiver's next character report re-lists the id, so two copies persist. That is a durable duplication from one
frame, and MVP excludes anti-cheat (decision 184), so it is named rather than patched with a reach/timing bound.

## The anchor it needs

An outcome the HOST can compare with its own row: the receiving client, after applying a give, reports the place
it ACTUALLY used (slot or container) — a statement about a fact the host already wrote, so the host can refuse a
mismatch by name and the census row becomes bounded. The residual's failure report then rides the same message as
a mismatch instead of a bare claim.

## What landed (2026-10-23) — the APPLIED arm

- **One outcome message, not two.** `NetMsg.PlayerPlacementOutcome` 160 replaces `NetMsg.PlayerPlacementFailed`
  160: the receiving client reports the place its placement really used, or that it placed nothing (`Applied`
  false). The failure is one arm of the same message now, so a receiver that materialized the item has to name
  where it put it.
- **The host compares against data it already holds.** `CommittedPlacement` records the slot or container the host
  wrote from the answer. A report whose place equals it settles the give and writes nothing; a different place, an
  applied report naming no place, or nothing applied is refused by name and `GivenItemReturnService` moves the row
  back to the giver and publishes the return (the return transfer is the answer, and it removes the receiver's own
  copy). A report matching no committed give — an item this host never wrote, or a sender it never wrote that row
  to — moves nothing.
- **One outcome per give.** The committed record is taken before anything moves, so a repeated or late report
  cannot return the same item twice.
- **The receiver reports the place the PLACEMENT used, never the request.** `PlayerInventoryPlacement` answers a
  `LocalPlacement` (slot, container, or not placed) from the same code that does the placement; the adapter reports
  it after the character re-report. The container fallback that used to be a local-only line now reaches the host
  as a mismatch, so a fallback can no longer pass as the answered destination.
- **The not-placed arm's end report was the other half of the old answer.** A handed-over item that found no place
  used to destroy its object and let `Item.OnDestroy` report the id dead — a statement that contradicts the host
  returning that same id to the giver. `UnplacedGiveItemOrigin` now keeps the destroy hook silent (read by
  `ItemPatches.ItemOnDestroyPatch`, the shape `LimbAppliedItemOrigin` already uses), and only the give's own arm
  attaches it: a RETURNED item that finds no place has nothing behind it and keeps its honest end report.
- **The host clamps the reason itself.** `ClampReason` now runs on receipt as well as on send: a peer that skips
  its own clamp cannot write control characters or an unbounded string into the host's log.
- **The wire bookkeeping.** Sync-coverage row P14 now carries three messages and 7 evidence anchors; the enum and
  the message are indexed there; the direction classification names `PlayerPlacementOutcome` as guest→host; the
  give gesture's shape gate pins the not-applied arm, `outcome = placed`, the marker reader and
  `SendPlacementOutcome`; the protocol number is untouched before the first release (decision 241 — the change is
  recorded by this ticket and its commit).
- **Tests.** `PlacementAnswerTests` grew to 13 cases: the answered slot recorded, the receiving-rules refusal, a
  destination that changed before custody, no resolver, a MATCH settling silently, a container give matching its
  container, a placement that landed elsewhere being refused and returned, the not-applied return, an
  applied-but-place-less report being refused and returned, the unknown/wrong-sender report moving nothing, the
  host-side clamp of a member-authored reason, the give transfer carrying the answered-destination flag to the
  receiver (off the serialized frames), and the return transfer carrying its own flag (both flags decoded from the
  two committed batches). Two `KernelWireMapper` round trips pin both flags on the kernel wire itself — the gap the
  earlier codec-only test left open (batch `20261023-e` found it on the machine).

## The machine run's finding (2026-10-23, batch 20261023-e)

The give was performed for real on a two-client session (host = giver, first sandbox = receiver) against
`0.1.0+aaf89655…`: the receiver's own client answered `slot 1`, the host committed the row, and the receiver placed
the bandage in slot 1 — but **neither client wrote a `[Placement]` line**: no `… reporting it to the host`, no
`… is where this host wrote it`. The defect was in the carrier, not the receiver: `WirePlayerInteraction` carried the
transfer's `Destination` but not `ExactDestination`/`Returned`, so
`KernelPlayerInteractionWireMapper.FromWireInventoryTransfer` rebuilt the event with both false and
`PlayerInteractionApply` skipped its exact arm — the outcome was never sent. The old
`TheTransferFlags_SurviveTheKernelProjection` only ran `PlayerInteractionKernelCodec.ToTransferMessage` on an
in-memory event, which is why the suite stayed green.

Fixed in the commit that carries this record: two ProtoMember fields (28/29), both mapped on the way out and in, three
tests (two kernel-wire round trips and the give/return flags decoded from the serialized frames), and sync-coverage
row P7 now declares the two payload fields with their own anchors (14). The machine re-read of row 1 is still owed and
was NOT run in this round: the user asked for tickets and code only on 2026-10-23 (they were taking the machine for a
co-op session), so the row is unread on the fixed artifact — this is the one outstanding acceptance row of the
ticket.

## What remains — the NOT-APPLIED arm

The failure claim itself still has no data anchor. When `Applied` is false there is no place to compare, so
`HandleOutcome` goes straight to `ReturnToGiver`: the host reverses its own commit on the receiver's word. The
original exploit is therefore narrowed, not closed — a receiver that keeps its copy and reports `Applied = false`
still makes the host write a second copy on the giver, and its next carried-set report keeps the duplicate alive.

Per decision 265 that stays UNBOUNDED and is named (the census row says so). An anchor would have to establish a
negative fact about another client's body — that the item is not materialized there — and no data the host holds
can do that without a second, weaker simulation of that client's frame. The options a future cycle may weigh: an
echoed identity plus a place the receiver must name even when it placed nothing (a "where it is NOT" claim is
still unreproducible), or accepting the duplication as the named MVP limit. **Not started; this is the half the
ticket keeps.**

## Independent review (2026-10-23) — every finding and what changed

The change was reviewed in a fresh context against the frozen tree before its commit. Every blocker and major is
recorded here with the code it cites; all of them were fixed in the same commit.

- **BLOCKER — the suppressor had no reader, and its scope was wrong.** `ItemPatches.ItemOnDestroyPatch` read only
  `__instance.GetComponent<LimbAppliedItemOrigin>() != null`, so `UnplacedGiveItemOrigin` was dead code and the
  deferred destroy still reported the item's end. Worse, `DiscardUnplaceableItem` is also reached from the RETURN
  path (`msg.ExactDestination == false`, where no outcome is sent), so silencing the hook for every unplaceable
  item would have destroyed a returned item while the host's row stayed standing. Fixed: the hook reads both
  markers (`|| __instance.GetComponent<UnplacedGiveItemOrigin>() != null`), and the flag is the CALLER's
  (`suppressUnplacedEnd = msg.ExactDestination`), so only the give's arm attaches it — pinned by
  `GiveGestureGateTests` (`GetComponent<UnplacedGiveItemOrigin>() != null`, `suppressUnplacedEnd`,
  `PlaceCarriedItem(body, item, msg.TargetParentItemId, msg.ExactDestination)`).
- **MAJOR — the census label over-claimed.** `if (msg.Applied && PlaceMatches(committed, msg))` runs only for the
  applied arm; `ReturnToGiver` reverses custody on the not-applied arm with no comparison at all. The row was
  `bound:` while that arm is a bare claim — the same defect this ticket exists for. Fixed: the row is
  `unbounded:` again for the not-applied arm and names the applied one's bound, and the sentences "the host never
  writes a second copy from the reporter's word" and "one arm of a comparison instead of a bare claim" are gone.
- **MAJOR — the host logged a member-authored reason unclamped.** `DescribeMismatch` interpolated `msg.Reason`
  straight into `ReturnToGiver`'s `_log.LogWarning`, while `ClampReason` ran only on the sending side (the deleted
  handler had clamped on receipt). Fixed: `ClampReason(msg.Reason)` on receipt, pinned by
  `AMemberAuthoredReason_IsClampedOnTheHostSide` (a raw control-character reason sent past the sender's clamp
  reaches the host's line stripped and capped).
- **MAJOR — the batch this ticket named was an existing, closed one.** `20261023-a` is the cross-player item-use
  batch on `0.1.0+44e263cc…`; no deployed artifact could carry this message before the commit, so the "scheduled
  for batch 20261023-a" claim was false. Fixed: row 1 is stated OUTSTANDING, the attempt is recorded in
  `docs/evidence/acceptance/placement-failure-report-anchor-20261023-d.md`, and the checklist's L1 line says the
  same.
- **Minors taken.** The message doc's "capped before it is logged" claim is true now that the host clamps; the
  outcome is keyed by the item id because the transfer the receiver applies does not carry the query id (recorded
  as a limit above, not fixed); the receiver half is shape-pinned and stated as such; row 3's reading is stated in
  its verdict; P14's loss cell and the "nothing diverges" line were corrected for the not-placed arm;
  `20261022-h-scope.md` now says the tickets were under `todo/` at that time (the paths were re-pointed, the
  readings unchanged); the container-fallback doc names which ruling governs the race arm (the 2026-10-22 one);
  the checklist says "no NEW surface" rather than "no change"; and the applied-but-place-less arm has its own
  test (`AnAppliedOutcomeThatNamesNoPlace_IsRefusedAndReturned`).

## Non-goals

- No reach/aim/timing bound (anti-cheat is out of the MVP).
- No change to the give gesture.

## Acceptance

1. A give applied at the answered destination reports the same place; the host sees a match and writes nothing. —
   **PASS by test**: `AMatchingPlacementOutcome_SettlesTheGiveAndMovesNothing` (and the container half,
   `AContainerGiveThatStayedInTheContainer_SettlesTheGive`). The MACHINE reading is **outstanding**: batch
   `20261023-d` attempted it on `0.1.0+1bf7104c…` and could not start the give drag (the created item sat in a HUD
   cell the press path does not reach; record `docs/evidence/acceptance/placement-failure-report-anchor-20261023-d.md`).
   Batch `20261023-e` performed the give on `0.1.0+aaf89655…`: the receiver answered `slot 1` and placed the bandage
   there, but no outcome line appeared on either client; the cause was the kernel wire dropping the two flags, fixed
   with this ticket's commit. **The re-read on the fixed artifact is `PASS`**: batch `20261023-f` repeated the
   gesture on `0.1.0+9226c020…` and read the receiver's own `placed it at slot 1 — reporting it to the host.` beside
   the host's `is where this host wrote it (slot 1): the placement outcome matches, nothing moved.` (record
   `docs/evidence/acceptance/placement-failure-report-anchor-20261023-f.md`).
2. A receiving client that changed its body reports a different place or a refusal; the host refuses the outcome
   by name, returns the row, and the giver sees the return. — **PASS by test**:
   `APlacementThatLandedSomewhereElse_IsRefusedAndReturned` (a container that refused on the receiver, so the
   placement used a slot), `AnAppliedOutcomeThatNamesNoPlace_IsRefusedAndReturned` (the malformed applied arm) and
   `APlacementThatAppliedNothing_ReturnsTheItemToTheGiver` (all end with the giver holding the item, the
   receiver's copy gone, and two transfer events — the give and the return).
3. A report naming a place the host never wrote is refused by name and moves nothing. — **PASS by test**:
   `AnOutcomeFromAnotherPeer_OrForNoCommittedGive_MovesNothing` (a report from a peer the row was not written to,
   and one for an item this host never committed, both leave the committed give standing). The reading taken here:
   "a place the host never wrote" is the no-committed-give case; a place the host did not write FOR a committed
   give is row 2's mismatch, which returns the row by design.

## Limits (recorded, not claimed away)

- The not-placed arm stays the receiver's own body judgement; the comparison bounds the applied arm only, and the
  census row says so.
- The receiver half of rows 1-3 is shape-pinned (`GiveGestureGateTests`), not driven by a behaviour test: the test
  host has no Unity body, so the adapter's reporting of the place is a source-shape assertion, and batch
  `20261023-d` could not produce the machine half either (its record says why).
- The kernel wire carried the transfer without the give's two flags until this commit, so no real receiver could
  report at all; batch `20261023-e` is the attempt that found it (record
  `docs/evidence/acceptance/placement-failure-report-anchor-20261023-e.md`), and batch `20261023-f` read both
  clients' lines on the fixed artifact (record
  `docs/evidence/acceptance/placement-failure-report-anchor-20261023-f.md`).
- A return the host cannot make (the item is no longer in the receiver's snapshot, or the giver has no free slot)
  is logged at Error and the reporter is not answered — the limit `review/placement-outcome-never-returns.md`
  already records.
- A lost outcome leaves the committed row matchable for its 60 s window and then drops it. On the MATCH arm
  nothing diverges (the row already states the answer's place); on the NOT-APPLIED arm the receiver's object is
  already gone, so the row survives as the only copy until the session ends — the price of not reporting the end
  on that arm, and the reason the message is reliable.
- The outcome is keyed by the item's instance id. The query id would tie it to one committed give, but the
  transfer the receiver applies does not carry that id, and adding it would be a second wire change; a duplicated
  frame within the 60 s window is the only way to mismatch, on a reliable channel.

## Decision point (2026-10-24) — waiting on the user

The APPLIED arm is anchored and accepted; the NOT-APPLIED arm can only be bounded by a new claim, and the
options are a design decision:

1. **Keep it named** (recommended): decision 265 already says a report with no data anchor is named rather
   than given a behavioural bound; the census row says `unbounded` and the exploit is narrowed. Close the
   remaining half as a recorded limit instead of developing it.
2. **A "where it is NOT" claim**: the receiver must name the place it did not use even when it placed nothing;
   that is still a negative fact about another client's body and cannot be reproduced by the host, so it would
   be a second, weaker simulation.
3. **A per-give echo with a nonce**: narrows replay, not the absence claim.

Recommendation: option 1, with the duplication recorded as the named MVP limit (the ticket's own text already
leans there). No code is started because the choice decides what the protocol claims.
