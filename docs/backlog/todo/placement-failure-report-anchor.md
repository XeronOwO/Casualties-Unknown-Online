# The placement failure report has no anchor

- Status: Todo — the give's residual moves custody on the receiver's bare report (decision 265).
- Priority: Low-Medium
- Category: Cross-player custody / member-report bounds / anti-duplication
- Blocked by: nothing external. The design needs the receiving client to report its placement OUTCOME, which is a
  second wire member and a host-side comparison.
- Source: The independent review of `fb782e5c` (2026-10-22, finding 6) on
  `docs/backlog/review/placement-outcome-never-returns.md`: the host can bound the transactional half of the
  report (a committed give to that sender, the item in its snapshot) but not the claim itself, so the census row
  for `NetMsg.PlayerPlacementFailed` is `unbounded`.
- Related: `review/placement-outcome-never-returns.md` (the design whose residual this bounds),
  `docs/architecture/member-report-validation.md` (the census row), decisions 184/265 (judgement belongs to the
  affected client; a report with no data anchor is named rather than given a behavioural bound).

## Problem (evidence)

The receiver's failure report says "the destination I answered is gone" and carries only the item id and a
reason. The host's own records prove the item was SENT there — which is equally true after a successful
placement — so it cannot check the claim. A receiver that keeps its materialized copy and sends the report anyway
makes the host remove the row from its snapshot and write a copy on the giver while the object stays on the
receiver; the receiver's next character report re-lists the id, so two copies persist. That is a durable
duplication from one frame, and MVP excludes anti-cheat (decision 184), so it is named rather than patched with a
reach/timing bound.

## The anchor it would need

An outcome the HOST can compare with its own row: the receiving client, after applying a give, reports the place
it ACTUALLY used (slot or container) — a statement about a fact the host already wrote, so the host can refuse a
mismatch by name and the census row becomes bounded. The residual's failure report then rides the same message as
a mismatch instead of a bare claim.

## Non-goals

- No reach/aim/timing bound (anti-cheat is out of the MVP).
- No change to the give gesture.

## Acceptance

1. A give applied at the answered destination reports the same place; the host sees a match and writes nothing.
2. A receiving client that changed its body reports a different place or a refusal; the host refuses the outcome
   by name, returns the row, and the giver sees the return.
3. A report naming a place the host never wrote is refused by name and moves nothing.
