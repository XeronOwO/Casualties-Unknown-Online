# The state streams flood the wire while nothing moves

## Design

`docs/design/surfaces/remote-inventory.md` owns what an item report carries and when one is sent; this ticket is about a report that is sent far too often, which that page''s cadence rule already forbids.

- Status: Review — CORRECTED 2026-11-02: the flood is NOT the item move stream. The traffic line already prints a per-family breakdown and it names the real carriers; the item counter is CUMULATIVE over the session, so reading it as a rate was wrong. — found 2026-11-02 during a speech investigation. The host''s own traffic monitor shows `send ... 98-228 KB/s` against `recv ~9 KB/s` with nothing happening in the world, and the item service''s own counter explains it: `[ItemTraffic] total 7575; Spawn=0; Drop=0; Move=7575; Destroy=0; Pickup=0; top: bandage×2744, ...`. So the whole stream is MOVE reports, dominated by one carried stack.
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

## The correction: it is the state streams, not item moves (2026-11-02, 16:35)

`NetworkTrafficWindowLog` already appends `top:` and `payloads:` to the periodic line. Read in full, the host's own line says:

    send 944f/2026283B(201982B/s); recv 309f/88785B(8850B/s);
    top: Send KernelEnvelope=1945532B, Receive KernelEnvelope=71547B,
         Send HostCharacterData=26820B, Send TraderState=20808B, Send CharacterData=17350B,
         Receive CharacterData=17150B, Send PlayerVitals=12616B, Send FluidRegion=3065B, Send Ping=44B;
    payloads: Send EnemyStateStream=889110B/222f p50=4005B p95=4005B,
              Send StateStream=865954B/144f p50=5499B p95=10796B, Send ItemSnap...

So:

- The bytes ride `KernelEnvelope`, and inside it two per-frame streams dominate: `EnemyStateStream` at a flat **4,005 B per frame** and `StateStream` at **5,499 B median / 10,796 B p95 per frame**. Together that is ~9.5 KB every frame — at 20-30 frames/s, the 200 KB/s the monitor prints.
- `ItemSnap...` is a payload in the same list but far below those two, and the `[ItemTraffic]` counter that first looked like the culprit is CUMULATIVE over the session (7,575 moves since the session began, 2,087 of them before the first log line I compared against), not a per-second rate. Reading it as a rate was my mistake and is corrected here.

## What the fix must answer now

1. Why `EnemyStateStream` is a flat 4,005 B every frame: how many enemies ride it and whether a full snapshot is sent each frame instead of a delta.
2. Why `StateStream` is 5.5 KB median and 10.8 KB at p95 while the players stand still: which world entities are re-sent, and whether the cadence rule for unchanged state is enforced where the stream is built.
3. Whether the two streams can share a delta base — they are already on the same adapter rate service, so the question is the payload, not the schedule.