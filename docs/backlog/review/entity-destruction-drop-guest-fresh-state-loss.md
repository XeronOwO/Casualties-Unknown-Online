# Entity destruction drops lose fresh-drop presentation/initial motion on the guest view

- Status: Review (batch `20261002-e`: row 5 passes; rows 1–4 stay unproven — both directions now provably
  carry `FreshItemDrop` on host, guest and alt inside its window (census `fresh:3` / `fresh:1`, ids and
  world positions identical across clients, each peer materializing from the breaker's own velocity), but
  the run's frames still cannot be tied to the drop sprites at the shipped zoom: the census-projected
  points land tens of pixels off the frame content, so no crop can be said to show the drop — the ticket
  stays open)
- Acceptance records: `docs/evidence/acceptance/entity-destruction-drop-guest-fresh-state-loss-20261001-x.md`, `docs/evidence/acceptance/entity-destruction-drop-guest-fresh-state-loss-20261001-y.md`, `docs/evidence/acceptance/entity-destruction-drop-guest-fresh-state-loss-20261002-e.md` (batch scope: `docs/evidence/acceptance/20261002-e-scope.md`)
- Priority: Medium
- Category: Item sync / entity destruction presentation
- Source: User report (2026-09-04); rejected by user (2026-09-05) with a jump-pad trap destruction reproduction.

## Rejected

User re-tested the entity/trap destruction drop scenario with the host
destroying the support block under a jump-pad trap. The previous fix was still
rejected:

- Host view: the dropped items have the white border/highlight and the
  no-gravity/fresh-drop effect.
- Guest view: the dropped item does not have the white border/highlight, and
  because only one item is visible on the guest side it cannot be confirmed
  whether no-gravity/fresh-drop is present.
- The guest also has a separate quantity/desync issue for the same destruction
  (see `done/trap-destruction-drop-quantity-desync.md`), which may affect
  observable presentation.

This ticket is re-opened as TODO. The fresh-drop presentation must match the
host side on all peers, and the missing/desynced drops must not hide the
verification.

## Rejected again (2026-09-05 acceptance)

User re-tested entity destruction drop presentation in both directions:

- Host destroys an entity:
  - Host view: the dropped items show the white fresh-drop highlight.
  - Guest view: the same dropped items do not show the white highlight.
- Guest destroys an entity:
  - Guest view: the dropped items show the white fresh-drop highlight.
  - Host view: the same dropped items do not show the white highlight.

This is the same fresh-drop presentation family, and it is bidirectional:
the non-destroying peer is missing the highlight regardless of which side
destroyed the entity. The ticket stays open in `todo/` until both directions
(and third-party views) are fixed and verified on the deployed artifacts.

## Goal

Make destruction drops from building/trap/entity death appear on every peer with the same fresh-drop presentation and the same initial motion phase as the attacker side, regardless of which peer (host or guest) destroyed the entity, without causing the non-destroying peer to first fall/step and then be yanked back by the authority stream.

## Current behavior / suspected root cause

- Block-break drops have a dedicated chain that preserves the full initial drop state:

  - `src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs` and `src/CasualtiesUnknownOnline.Runtime/Session/Items/BlockDropSync.cs:60-62` — `BlockDropEntryMsg` is converted directly to a `WorldItem` including `FreshItemDrop`, position, velocity, rotation and angular velocity.

- Building/entity destruction drops use the atomic trap/building-death path instead:

  - `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemWorldSync.cs:215-241` — a building-death drop is captured into `TrapDropEntryMsg` with `FreshItemDrop`, position, velocity, rotation and angular velocity.
  - `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/TrapDropEntryMsg.cs` — the full initial drop state is present on the wire.
  - `src/CasualtiesUnknownOnline.Runtime/Session/World/TrapStateRegistry.cs:123-137` — the host folds those drops into the atomic kernel batch by creating only `SpawnItemCommand`s; the spawn command carries `ItemData` (save-shaped item state) but not `FreshItemDrop`, velocity, rotation or angular velocity.
  - `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelBatchItemProjection.cs:353-365` — `ToWorldItem` materializes the projected world item with `FreshItemDrop = false`, zero velocity, zero rotation.
  - `src/CasualtiesUnknownOnline.GameAdapter/World/EntityEventSync.cs:133-141` — on a host→guest entity-event relay, the guest only replays the trap/entity presentation; it does not materialize `EntityEventMsg.Drops` directly. The guest's dropped items are materialized from the kernel projection instead.

- The state stream (`WireItemMoveEntry`) later supplies position/velocity/rotation to the guest through `ItemPositionFollow`, but it does not restore the transient `FreshItemDrop` presentation flag. A newly projected guest item therefore starts without the glowing/floating fresh-drop effect and without the same initial physics phase; the later authority correction reads as a drop-then-pull-back.

- Relevant code:
  - `src/CasualtiesUnknownOnline.GameAdapter/Items/RemoteItemSceneOps.cs:347-406` — guest materialization; `FreshItemDrop` is added only when `WorldItem.FreshItemDrop` is true, and guest items are frozen until the first stream tick.
  - `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemPositionFollow.cs:129-176` — first stream tick switches a frozen guest item to local physics and aligns it to the host.
  - `src/CasualtiesUnknownOnline.Protocol/Wire/WireWorldItemState.cs:39` — the full state-stream snapshot does carry `FreshItemDrop`, but the kernel-projected world item used to build that snapshot already has it set to `false`.

## Missing behavior on the guest view

- No fresh-drop highlight/glow effect (`FreshItemDrop` component is not attached).
- No fresh-drop no-gravity/floating feel (as described by the user; needs runtime confirmation, but it points at the same missing `FreshItemDrop` presentation path).
- The item can fall locally and then be snapped/pulled back when the host's authority stream arrives, because the guest materializes at a different initial phase than the host.

## Required design direction (for the implementation cycle)

- Preserve the full transient spawn state for entity/building destruction drops across the wire/projection path without making continuous physics a kernel fact:
  - carry `FreshItemDrop`, initial velocity, rotation and angular velocity alongside the `SpawnItemCommand` / projected `WorldItem`, or
  - let the guest materialize the drop directly from `EntityEventMsg.Drops` during replay (the full state is already in the message), while the kernel remains the authoritative item fact source.
- Ensure the guest's materialized drop is marked fresh and starts frozen at the exact host spawn state, then switches to local physics on the first `ItemPositionFollow` tick.
- Keep the block-drop path working; do not regress its existing fresh-drop behavior.
- Add regression/runtime evidence for both host-triggered and guest-triggered entity destruction, and for a third-party peer view.

## Implementation (2026-09-04)

- Added `InitialDropStateMapper` (Runtime) as the single pure mapping for both
  block and trap/building initial-drop entries; `BlockDropSync` now reuses it.
- Added `ItemApplication.ApplyTrapDropPresentation`, which materializes a
  missing drop with the event's full state or enriches an already-projected
  world item with the missing fresh/velocity/rotation/angular-state facts.
- `EntityEventSync` now calls that path on the host-apply branch (guest-triggered
  traps) and on the guest-replay branch (host-triggered & third-party views).
- No wire/protocol change; `EntityEventMsg.Drops` remains the transient
  presentation source.
- Evidence: `docs/evidence/selfchecks/items/entity-destruction-drop-fresh-presentation-selfcheck.md`.

## Acceptance criteria (for the later implementation cycle)

- On a guest (and any other peer), entity destruction drops show the same fresh-drop highlight/floating presentation as the host/attacker view.
- The guest drops do not visibly fall-then-pull-back; they start at the same position/velocity phase and are later softly corrected by the normal item position stream.
- The fix works for destructive trap/building deaths, not only block breaks.
- Existing item/entity sync tests and repo gates remain green.
- No wire or authority regression; if a new field is needed, it must be documented and versioned appropriately.

## Landed (2026-09-05 second cycle)

The rejected jump-pad/support-block reproduction is now closed on the
support-loss building-death path:

- The non-breaker side is marked `RemoteEntityDeath` when a remote air-write
  removes a `requireGround` building's support block, so it no longer rolls its
  own (different) drop set.
- `BlockDamagedMsg.BuildingDrops` carries the breaker's building-death drops
  with the full transient initial state; `BlockBreakPendingState` collects them
  and `BlockDropSync` materializes them through `InitialDropStateMapper`.
- The existing destructive-trap `EntityEventMsg.Drops` presentation path remains
  unchanged for trap kinds.

Evidence:
`docs/evidence/selfchecks/items/building-support-drop-sync-selfcheck.md`.

## Acceptance criteria (for the later implementation cycle)

- On a guest (and any other peer), entity destruction drops show the same fresh-drop highlight/floating presentation as the host/attacker view.
- The guest drops do not visibly fall-then-pull-back; they start at the same position/velocity phase and are later softly corrected by the normal item position stream.
- The fix works for destructive trap/building deaths, not only block breaks.
- Existing item/entity sync tests and repo gates remain green.
- No wire or authority regression; if a new field is needed, it must be documented and versioned appropriately.

## Landed (2026-09-05 third cycle — ordinary building/entity death family)

The remaining rejected family is now closed at the shared item-spawn root:
ordinary building/entity death drops (melee attack, open, non-trap deaths)
previously went through the standalone `ItemSpawn` kernel path as zero/false,
so the non-destroying peer never got the white fresh-drop presentation or the
same initial velocity/rotation phase. The kernel spawn representation now
carries the transient initial-drop state:

- `SpawnItemCommand` / `ItemSpawnedEvent` / `WireCommand` / `WireEvent` carry
  `VelocityX`, `VelocityY`, `Rotation`, `FreshItemDrop`, `AngularVelocity`.
- Guest item-spawn reports, host item-spawn projections, trap-batch spawns and
  block-drop registration all preserve those fields into the committed batch.
- `KernelBatchItemProjection` materializes a newly projected world item from
  the event's transient fields instead of always zeroing them.
- `ProtocolVersion.Current` bumped 9 → 10; no new message type.
- This does not make continuous physics a kernel fact: the values are
  presentation metadata on the spawn command/event, not stored in `ItemState`.

Evidence:
`docs/evidence/selfchecks/items/item-spawn-presentation-kernel-selfcheck.md`.

## Non-goals

- Not adding continuous item physics into the kernel.
- Not changing entity/trap authority or the atomic composite design.
- The existing entity-event drop payload remains the transient presentation source for destructive trap kinds; support-loss building drops ride `BlockDamagedMsg.BuildingDrops` instead.

## Batch `20261025-y` — the destruction ran, the drops did not exist

A fresh session (relaunched after the deployment correction, lobby `109775242158345492`, all three clients in world) drove the
first half of the fixture: `building-destroy-at x=0,y=507` reported **`ok=true, healthBefore=300`**, so a real building died on this
client through the game's own death branch. The drop side then came up EMPTY: `item-world-read` around that cell answered
**`count=0` at radius 5 and at radius 15**, and the wider reads returned only two unrelated items ~500 units away (the same two
before and after the destruction). So this building rolled no drops at all — the fixture needs a destruction that HAS a drop table
before the fresh-guest half means anything, and the earlier projection problem (tying a census point to frame content) is
secondary to that. Named here instead of re-running the same destroy.

**Same batch, one more destruction, and the rule behind the emptiness.** A `medcrate` at `(19.66, 434.77)` was destroyed the same
way (`building-destroy-at` → `ok=true, healthBefore=250`) and `item-world-read` around it answered **`count=0` before and after, at
radius 10**. Two empty destructions in a row point at the ticket's own wording rather than at the probes: this page says
"support-loss building drops ride `BlockDamagedMsg.BuildingDrops`", i.e. a building drops when it loses SUPPORT, not when its health
is zeroed directly. The fixture therefore has to break the supporting block under a building and read the drop that follows, not
destroy the building itself — and the fresh-guest half starts only after a drop exists.

**Same batch, the support half, and where it actually is.** `building-support-cells` answered for the whole layer (`total=1873`,
`requireGround=1329`, 45 multi-building cells), and NONE of those support cells lies within 40 blocks of the pod area the clients
stand in (`x` in ±40, `y` in 440–560 → **0 matches**); the listed cells are elsewhere on the layer (`508,412`, `927,176`,
`884,952`, `709,265` …). So the pod's own props are not `requireGround` buildings, and staging a support-loss drop means reaching
one of those buildings — travel or a placement this station cannot do reliably. That is the fixture's real blocker, one level
earlier than the fresh-guest half and two levels earlier than the projection problem.

**Batch `20261025-ag` — SUPPORT LOSS DOES DROP, and the fixture finally has its drops.** The pod's props are not `requireGround`
buildings, so the operator was teleported to a real one: `body-place x=508,y=412` answered `ok=true` and the body STAYED there
(`(508.085, 412.288)`, alive) — so travel is not needed after all, a placement is enough. `building-support-cells` confirmed a
two-building support cell exactly at `(508,412)`, and breaking it (`block-break-at x=508,y=412, dmg=999` → `blockBefore=2,
blockAfter=0`) produced **five world items** where there were none (`item-world-read` radius 12: `0 → 5`, ids
`1207206791107 / 1202911823811 / 1198616856515 / 1194321889219 / 1190026921923`). Note the items report positions around
`(-3.8, -98.7)`, so `item-world-read`'s item coordinates are NOT the block-cell space of `block-break-at` — that is the space
question an earlier batch chased, and it is answered here: the two probes speak different coordinate systems. What remains for the
ticket's rows is the FRESH guest: a member who enters after these five drops exist must see the same ids at the same places.

**Batch `20261025-ah` — the fresh member was made, and the first read is INCONCLUSIVE, not a pass or a fail.** The `alt` client
left the world (`leave-world` → `ok=true`) and rejoined the same lobby (`join-lobby 109775242158345492` → `ok=true`); eight seconds
later it reported `sessionActive=true, role=Guest, host=76561198281246659`. Against the same cell the host read **5 items** and the
alt read **0** — but the alt's own body was not there to see anything: `body-read` returned no position and `body-place x=508,y=412`
answered with empty fields, i.e. that client had not finished materialising its body. So this read cannot be scored: an entry
whose body has not spawned is not a fresh guest looking at the ground. The next cycle verifies the alt's body first (`body-read`
position non-empty) and re-reads, and only then does a 0-vs-5 differ mean the ticket's defect is present.

**Batch `20261025-ai` — a rejoin lands the member in the SESSION but not in the WORLD, and that is why there was no body.** The same
result came from the guest client, and a captured frame settles it: the guest's screen shows the CUO Online panel open on its Home
tab with `Role: Guest Handshake: active — Steam network`, `Members: 3`, `You are already in an online session.` and the world's
own sky behind it. So `join-lobby` restores MEMBERSHIP, and `body-read` answering `no-local-body` is correct — the member is sitting
at the menu, and the world entry (the run load that produces a body) is a SEPARATE drive step this batch never performed. The
fresh-entry reading therefore needs one more step before it means anything: rejoin, then drive the world entry while in session, and
only then read the drops. Recorded with the frame as the evidence rather than another blind re-read.

**Batch `20261025-aj` — the entry GATE waits for every other member, which is why a lone rejoin never gets a body.** Driving the
world entry from the rejoined guest answered `continue-not-entered` ("LoadRun was called but the world never started generating"),
and the panel's own state explains it: `inWorld=false`, `gateWaiting=false`, and `gateText: "Waiting for 萧稔小号… (0s). The host is
already in the world."` — the entry gate is waiting on the OTHER member (the alt), which is itself still sitting at the menu after
its own rejoin. So a fresh member cannot enter alone while another member of the same lobby has not entered; the fresh-entry read
needs both men driven to the entry gate together, not one at a time. That is the missing step, named here with the gate's own text.

**Batch `20261025-ak` — the gate is SYMMETRIC, and the two drives collide on the driver itself.** Driving the ALT's entry produced
the same answer on the other side: `continue-not-entered` with its own `gateText` naming the guest, so each member waits for the
other and neither can enter alone. Running the two `continue-run` calls in parallel then failed on the transport instead
(`发生了内部 WebSocket 错误` on both), because one invocation of the driver cannot serve two clients at once — the collision is in
the tool, not in the game. So the fresh-entry read needs one of two things before it can be judged: a driver that can hold two
clients' requests at the same moment (a unique temp/probe per invocation), or a gate that lets one member enter while the other is
still at the menu. Both are capabilities, not readings, and this ticket's rows 1-4 stay open on that boundary.

**Batch `20261025-al` — a FAILED batch, recorded as one.** Driving the two `continue-run` calls genuinely in parallel (background
processes, no foreground read competing for the socket) made both clients answer `continue-not-entered` with `active=false,
hostId="0"` and `gateText: "Waiting for the host to load… The host is not in the world yet."` — while the HOST itself still reports
`inWorld=true`. So the two men are no longer in an active session at all: the leave/rejoin/continue sequence left them outside it
while the host stayed in the world. No row was judged by this batch, its root cause is the capability named above (the rejoin path
loses the session when a member then drives `continue-run`), and the next attempt starts by establishing ONE member's fresh entry
against a host that is in world, rather than driving both men's entry from a menu they reached through a leave/rejoin.
