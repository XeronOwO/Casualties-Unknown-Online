# The state streams flood the wire while nothing moves

## Design

`docs/design/surfaces/remote-inventory.md` owns what an item report carries and when one is sent; this ticket is about a report that is sent far too often, which that page''s cadence rule already forbids.

- Priority: High (was: bandwidth; closed)
- Status: Done — both carriers fixed and measured on the deployed build (2026-11-02, 17:05). The session's send rate fell from **213,462 B/s to 20,403 B/s** (~10.4x) with nothing moving in the world: `EnemyStateStream` p50 4,005 B -> **34 B** per frame, `StateStream` (the world-item moves) p50 7,489 B -> **107 B** per frame. What remains is deliberate: the keyframe cadences (enemy every 20 ticks, item snapshots on their own schedule) show up as p95 spikes of 3.3 KB and 11.9 KB, which is the price of bounding a dropped unreliable packet's staleness.
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
## The enemy half, fixed and measured (2026-11-02, 16:36)

Two changes, each measured on the deployed build:

1. The broadcast now sends only the enemies whose stream fields changed, with a full keyframe every twentieth tick. The first version still shipped a flat 4,005 B — a crawler's `SpiderLegTargets` are re-solved every tick, so every enemy read as changed.
2. The change test now covers what a viewer sees of the BODY (position, velocity, rotation, health, stun flag, crystal line) and leaves the leg animation and windup amount to ride along whenever the enemy is sent for a real reason and on every keyframe.

Read on the traffic line after both, on a freshly launched session:

    send 987f/558078B(55719B/s)   (was 213462B/s)
    Send EnemyStateStream=21986B/90f p50=157B p95=157B   (was p50=4005B p95=4005B)

That is the enemy stream's payload down by a factor of ~25 and the session's send rate by ~4×, with the frame cadence unchanged.

## What is left: the state stream

    Send StateStream=298181B/37f p50=7489B p95=12791B

`StateStream` is now the largest payload in the session — 7.5 KB median, 12.8 KB at p95, per frame, while the players stand still. It deserves the same treatment the enemy stream just got: find what the world stream carries per tick and whether change-based selection applies there.
## The state stream IS the item move stream (2026-11-02, 16:40)

The payload named `StateStream` is built in `ItemService.SendItemMove` as `new WireStateStream { ItemMoves = [.. items] }` and broadcast as `WirePayloadType.StateStream`. So the first reading was right about the CONTENT and wrong about the counter: the bytes are item moves, the `[ItemTraffic]` line that misled me is just cumulative.

`ItemPositionAuthority` sends items every `AdaptiveStreamId.WorldItemMoveStream` tick (10 Hz) unless `ItemMotionState.IsSettled(velocity.sqrMagnitude, angularVelocity)` says otherwise, in which case a settled item drops to 1 Hz. At `p50=7,489 B` per frame and roughly 40 B per entry, that is ~190 items going out on every full-rate tick — which says almost nothing in that world is reading as settled.

So the question the fix has to answer moved one step down: why ~190 lying items are not settled by `ItemMotionState`'s thresholds. The likely candidates are a velocity floor that ground contact never drops below (jitter), or items whose rigidbody is not simulated and whose velocity is stale rather than zero. The reading that decides it is a probe over a sample of world items: velocity, angular velocity, weight and the settled verdict side by side.