# NPC / Enemy Synchronization — Current Design

> Architecture context: enemy terminal facts are kernel-owned. See
[> `architecture-evolution/domains.md`](../architecture/domains.md) and
[> `architecture-evolution/protocol.md`](../architecture/protocol.md).
[> Canonical per-entity sync status: `entity-features.md`](entities.md).

Status: **current** — host-authoritative enemy sync, host-ordered attacks, and
kernel-backed enemy combat terminal facts are implemented. The old direct
`EnemyBite`/`EnemyLunge`/`EnemyEffect` result frames were removed in Phase D.

## 1. Enemy model

An enemy is a `BuildingEntity` with `animal == true` plus one of several
heterogeneous AI scripts. There is no shared enemy base class; the common anchor
is the `animal` flag. CUO freezes enemy-local AI/physics on guests and drives
their observable state from the host.

Representative enemy script families:

- `SpiderHandler` / `SpiderHandlerTBE` — wander/aggro/use `Random` + physics.
- `CrystalEnemy` — track/wind-up/lunge state machine.
- `GrabberPlant`, `ElderThornbackBehaviour`, `XalorisScript`, `CaveTicks`, etc.

Enemy movement uses Unity physics and local random numbers, so two sides
simulating independently diverge. Consequently guests do not simulate enemy
physics; they render a frozen copy driven by host state.

## 2. Authority and data flow

### Continuous enemy state

High-frequency enemy fields ride `StateStreamEnvelope` over `KernelEnvelope`
(host → guest). This is the same unified stream path as player continuous state;
see `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/PlayerStreamExchange.cs`
and `docs/architecture/protocol.md`.

- Continuous fields: position, velocity, rotation, presentation flags
  (spider legs, wind-up, etc.).
- These streams are update-only: they may not create/destroy enemy aggregates or
  override a terminal kernel fact.
- World-entry/reconnect snapshots use kernel enemy terminal facts through
  `EnemyKernelRestoreProjection`.
- The same absolute snapshot also rides the host's 60 s in-session repair group
  (`WorldEntryFanout.SendInSessionRepair`), so a member that never leaves the
  world re-binds after an entry send the lazy session swallowed. A repeat
  snapshot is idempotent because the guest pairs its frozen generated copies on
  each entry's host bind-time `SpawnPosition` anchor, never on the live position
  (which has moved on by then).

### Enemy lifecycle and health facts

Kernel domain (Entities/Enemies):

- State: `EnemyStateTable` / `EnemyState`.
- Commands: `UpsertEnemyCommand`, `RemoveEnemyCommand`, `ResetEnemiesCommand`,
  `RecordEnemyBiteCommand`, `RecordEnemyLungeCommand`,
  `RecordEnemyEffectCommand`.
- Events: `EnemyUpsertedEvent`, `EnemyRemovedEvent`, `EnemiesResetEvent`,
  `EnemyBiteResultEvent`, `EnemyLungeResultEvent`, `EnemyEffectResultEvent`.

Source: `src/CasualtiesUnknownOnline.GameState/Domains/Entities/`.

Runtime projections:

- `EnemyKernelProjection` — host kernel facts into enemy runtime state.
- `EnemyKernelRestoreProjection` — kernel terminal facts into reconnect/entry snapshots.
- `EnemyCombatKernelProjection` — combat result events into host character save
  and peer presentation.

### Announced attacks judged by the victim

Remote player clones have colliders disabled, so host collision callbacks cannot
apply an attack to a remote body directly — and since the 2026-09-18 ruling the
host does not decide who was hit either. The active path is:

- `EnemyAttack` (`NetMsg 83`) — host → every in-world guest: the host announces
  the enemy's action (`EnemyId`, `Kind`, the per-enemy `AttackSeq`) and each guest
  judges on its own view whether the attack reached its body, then applies the
  game's own damage path locally.
- The judgment belongs to the client the effect lands on: a spider bite needs a
  real collider contact plus the game's facing gate, a crystal lunge runs the
  game's own ray (first body wins, the ground stops it), and the limb is chosen
  on that client.
- Terminal combat results are journal-only kernel events
  (`EnemyBiteResultEvent`, `EnemyLungeResultEvent`, `EnemyEffectResultEvent`).
- `EnemyAttackJudgment` / `EnemyAttackLedger` (Runtime) own the victim-side rule
  and the one-attack-once identity; `EnemyTargetResolver` /
  `EnemyCombatArbitration` keep the host's target selection and bite-action gates.
  The old `EnemyBite`/`EnemyLunge`/`EnemyEffect` direct result frames are gone.

### Runtime spawns and removal

- Deterministic worldgen enemies are distributed by
  `WorldGeneration.DistributeEntities`.
- Runtime spawns (for example `CaveTickSpawner`) travel through
  `EntitySpawned` and are bound by `EnemyRuntimeSpawnArbitration`
  (position-based pairing, all-or-nothing). That positional key is scoped to the
  moment of creation — a runtime spawn the host binds some frames later keeps
  that scope — while the generated baseline pairs on the recorded anchor
  (`review/runtime-entity-markerless-bind-absorption.md` covers the keyed cases).
- Enemy aggregate removal rides `EnemyRemovedEvent`; a session-scoped
  resurrection guard prevents stale stream rollback of a removed enemy.

## 3. Current codec/wire mappings

There is no `EnemyStateCodec` type. Current mappings are:

- `EnemyEntity.ToEnemyStateMsg()` for runtime snapshot creation.
- `EnemyStreamWireMapper` for stream wire mapping.
- `EnemySnapshotMsg` for the absolute enemy snapshot (world entry / reconnect /
  the 60 s in-session repair); each entry carries the host's bind-time
  `SpawnPosition` anchor beside the live position.
- `EnemyKernelWireMapper` / `EnemyCombatWireMapper` where kernel facts map to wire.

## 4. Risks and boundaries

1. **Heterogeneous script freezing** is the largest adapter surface; Harmony
   contract tests lock the freeze list for enemy AI scripts.
2. **Only presentation/continuous state is streamed**, not full AI internal state
   (targets, timers). Terminal side effects are kernel events, not stream facts.
3. **No guest enemy physics** — the guest is frozen and snapshot-driven, so no
   deterministic-simulation divergence is introduced.
4. **Runtime enemy prefabs** must reuse the `EntitySpawned` + runtime-binding
   path so late joiners can materialize the same facts.
5. **`Heater`/temperature and other local-body effects** remain excluded by
   design.

## 5. Evidence

- Kernel enemy domain:
  `src/CasualtiesUnknownOnline.GameState/Domains/Entities/`
- Runtime projections:
  `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemyKernelProjection.cs`,
  `EnemyKernelRestoreProjection.cs`, `EnemyCombatKernelProjection.cs`
- Stream path:
  `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/PlayerStreamExchange.cs`
- Phase D self-check:
  `docs/evidence/selfchecks/architecture/phase-d-enemies-shadow-selfcheck.md`
- Phase D full migration:
  `docs/evidence/selfchecks/architecture/phase-d-full-domain-migration-selfcheck.md`
