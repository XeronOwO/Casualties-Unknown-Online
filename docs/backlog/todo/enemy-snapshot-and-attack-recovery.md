# Enemy snapshot and attack have no recovery path

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / enemies
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row N1)
- Related: `todo/runtime-entity-spawn-backfill.md`, `review/global-adaptive-report-rate-flow-control.md`

## Problem (evidence)

`EnemySnapshot` (NetMsg 81) is world-entry / reconnect only and `EnemyAttack`
(NetMsg 83) is a one-shot host-ordered command.

- Snapshot send: `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemySyncService.cs:292`
  (`_sender.Send(steamId, NetMsg.EnemySnapshot, payload);`), triggered by the
  world-entry fan-out `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntryFanout.cs:43`
  and by reconnect-while-InWorld `src/CasualtiesUnknownOnline.Runtime/Session/Handlers/HandshakeHandler.cs:145`.
- The 20 Hz stream is update-only and cannot rebind: its payload has no
  `PrefabId` (`src/CasualtiesUnknownOnline.Protocol/Wire/WireEnemyStreamState.cs:12-39`),
  the broadcaster sends `EnemyStates` only (`EnemySyncService.cs:263-270`), and
  the clone binder is fed by the snapshot's `RuntimeSpawns`
  (`EnemySyncService.cs:285`, `:337-341`).
- A swallowed snapshot leaves the guest with no enemy clones until the next
  reconnect; an empty host table is a no-op
  (`EnemySyncService.cs:277`), so a legitimately empty snapshot is not the issue —
  a dropped one is.
- `EnemyAttack` is dropped when the victim is not in world
  (`EnemySyncService.cs:145`) or the enemy id is not yet bound
  (`src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyCombatReplay.cs:46`),
  and nothing re-issues it; the host has already written the retreat/cooldown
  (`src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyCombatDirector.cs:328-342`).
- The kernel checkpoint does not rebuild the runtime clone buffer: on restore it
  only tracks removed ids and terminal revisions
  (`EnemySyncService.cs:432-455`); `EnemyKernelRestoreProjection.Apply` only
  overwrites health/stunned/prefab/runtime flags of an already-present entity
  (`src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemyKernelRestoreProjection.cs:28-40`).

## Goal

A guest that missed the `EnemySnapshot` re-binds without a reconnect, and a lost
`EnemyAttack` does not leave the host's simulation and the victim's body
permanently disagreeing. Lifecycle finality (a removed enemy never resurrects)
stays intact.

## Design direction (decide at implementation)

1. **Periodic snapshot resend** — piggyback the absolute `EnemySnapshot` on the
   existing 60 s host cycle (the block-state/checkpoint cycle), or a shorter
   dedicated cadence for the binding table.
2. **On-demand rebind** — the guest reports unknown enemy ids; the host answers
   with the snapshot (or just the missing entries).
3. **Attack recovery** — either re-issue a dropped attack on the next AI decision
   (preferred: no wire change, the enemy attacks again), or make the attack
   command idempotent with an ack/retry. If the loss is accepted, record the
   reason in the matrix row instead of leaving it implicit.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest misses the world-entry `EnemySnapshot` | Enemies re-bind without reconnect |
| 2 | Host has an empty enemy table at world entry | No spurious resend loop; the guest's own generated enemies stay |
| 3 | Late joiner | Bound clones + `RuntimeSpawns` materialized exactly once |
| 4 | Reconnect while in world | Same; no duplicates |
| 5 | Runtime-spawned animal | Materialized on every peer |
| 6 | Enemy removed before a resend | Not resurrected (lifecycle finality) |
| 7 | `EnemyAttack` dropped (victim not in world / id unbound) | The victim does not stay permanently unbitten; either re-issued or explicitly accepted with a recorded reason |
| 8 | Third-party view | All peers agree on the enemy set and terminal health |

## Non-goals

- Enemy AI / combat policy changes.
- The runtime BuildingEntity half (`todo/runtime-entity-spawn-backfill.md`).

## Progress (2026-09-09) — identity half landed, recovery still open

`review/runtime-entity-markerless-bind-absorption.md` landed the prerequisite identity
work: the host publishes the runtime-created animal's creation key on the snapshot
(`EnemySpawnEntryMsg.CreationKey`, ProtoMember 8) and `EnemySyncCoordinator.CreateRuntimeSpawn`
stamps it onto the materialized backfill copy, so a surviving live re-report binds that
copy by identity instead of duplicating it (the markerless 1 m positional fallback is
deleted). `ProtocolVersion.Current` 17 → 18.

Still open in this ticket: a swallowed `EnemySnapshot` has no periodic re-send, an empty
host table is still a no-op, and a dropped `EnemyAttack` is still never re-issued (matrix
rows 1, 2, 7 and the cadence decision).
