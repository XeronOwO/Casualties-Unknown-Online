# The residual row's reading — the user's ruling (2026-10-23)

- Applies to: row 6 of `docs/backlog/review/placement-outcome-never-returns.md` and row 6 of
  `docs/backlog/review/give-item-to-another-player.md` — "a body that changes between the answer and the transfer
  ends with the item back with the giver".
- The ruling: after two staging attempts established that the row's window cannot be produced by a hand, the user
  ruled (2026-10-23) that the behaviour test plus the wired return chain is this row's reading. A second pointer
  device was ruled out by the user. The row is recorded **PASS by ruling**, never as a machine reading.

## Why a machine reading is impossible by hand

- **One pointer, one foreground window.** Both clients need the physical mouse and the foreground window for a
  gesture; the receiver's own body change cannot overlap the giver's release (batch `20261022-i`).
- **The window is 60 ms.** On the fixed artifact the answer→apply span measured
  `00:18:25.155` → `00:18:25.215`; a console `item-provide` aimed at the receiver right after the release arrived
  after the item had already landed (batch `20261022-j`). Every external call is ≥100 ms.

## What the reading rests on

- `PlacementAnswerTests.APlacementThatAppliedNothing_ReturnsTheItemToTheGiver` (renamed from
  `AFailedExactPlacement_ReturnsTheItemToTheGiver` when the placement outcome landed) — the host moves the row
  back to the giver when the receiver reports the answered destination gone, and the kernel sees the give and the
  return.
- `PlacementAnswerTests.TheTransferFlags_SurviveTheKernelProjection` — both flags (`ExactDestination`,
  `Returned`) cross the kernel boundary; a dropped flag would make the branch dead, which is what the review found
  in the earlier commit.
- `PlacementAnswerTests.AnOutcomeFromAnotherPeer_OrForNoCommittedGive_MovesNothing` (renamed from
  `AFailureFromAnotherPeer_OrForNoCommittedGive_ChangesNothing`) — the report moves nothing without a committed
  give and the right sender.
- The fix commit `e3cdc117` wires the report end to end (`PlayerPlacementFailed` → `HandlePlacementFailed` →
  `GivenItemReturnService.Return` → the returned transfer), which the batch records could not exercise because no
  client could produce the report at the earlier artifact.
- The consumer of a full giver's body is the `Returned` drop branch: the item lands at the giver's feet instead of
  being destroyed.

## What this record does NOT claim

- No machine session ever produced the receiver's failure report; the record says the behaviour is judged by the
  tests plus the wired chain, not observed on two clients.
- The report still has no data anchor (a malicious receiver can duplicate); that design item is filed as
  `docs/backlog/todo/placement-failure-report-anchor.md` and the census row stayed `unbounded` at this record's
  date. (Update 2026-10-23: the APPLIED arm is anchored now — `NetMsg.PlayerPlacementOutcome` has the receiver
  report the place its placement used and the host compares it with its own row — while the NOT-APPLIED arm still
  has none, so the ticket and its `unbounded` label remain for that half. This record's own claims are unchanged.)
