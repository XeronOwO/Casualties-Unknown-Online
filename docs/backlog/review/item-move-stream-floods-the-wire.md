# The item move stream floods the wire while nothing moves

## Design

`docs/design/surfaces/remote-inventory.md` owns what an item report carries and when one is sent; this ticket is about a report that is sent far too often, which that page''s cadence rule already forbids.

- Status: Review — found 2026-11-02 during a speech investigation. The host''s own traffic monitor shows `send ... 98-228 KB/s` against `recv ~9 KB/s` with nothing happening in the world, and the item service''s own counter explains it: `[ItemTraffic] total 7575; Spawn=0; Drop=0; Move=7575; Destroy=0; Pickup=0; top: bandage×2744, ...`. So the whole stream is MOVE reports, dominated by one carried stack.
- Priority: High (bandwidth: ~150-230 KB/s of a co-op session''s traffic, from one player standing still)
- Category: Session / item replication
- Source: user report about the trader, whose investigation surfaced this; measured in the same session.

## The reading

    [ItemTraffic] total 7575; Spawn=0; Drop=0; Move=7575; Destroy=0; Pickup=0; top: bandage×2744, minibarrel×...
    [NetworkTraffic] send 717f/1502328B(150008B/s); recv ...

Two thousand seven hundred and forty-four bandages is not a stack anybody carries (`minibarrel` beside it is), so the top entry is worth checking against the body''s own slots before the count is taken as a per-item property — but the shape is unambiguous either way: only MOVE reports are being sent, thousands of them, while no player moves.

## What the fix must answer

1. What makes a move report fire when a position did not change: the report''s own change test, or a writer that re-reports on every snapshot.
2. Whether the cadence rule the design page states is enforced where the report is emitted, or only where it is consumed.
3. Why one item dominates: a stack that is re-seated every frame, or a container whose children are re-reported with it.
