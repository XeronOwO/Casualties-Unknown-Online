# Phase D — Full Domain Migration

> Status: **In progress** (all Phase D domain areas have kernel foundations; remaining work is authority/projection cleanup and high-frequency stream alignment)
> Source: target architecture §8-§10; migration roadmap "Phase D".

## Objective

Extend the kernel from the Items domain to all persistent gameplay domains. Each domain
follows the same proven pattern: shadow -> authoritative switch -> old tables become
projections -> delete old state. By the end of Phase D, all authoritative gameplay facts
live inside typed kernel domains, and both protocol and save are driven by the same
transaction/reducer/replay machinery.

## Scope

In scope:

- All remaining domains:
  - World / Run / Epoch;
  - Traps and Building Entities;
  - Players (terminal state and cross-player interactions);
  - Enemies / Entities;
  - Fluids (persistent regions and high-frequency convergence);
  - high-frequency state stream unification.
- Per-domain shadow model and authority switch.
- Per-domain checkpoint integration.
- Cross-domain transaction processes/policies.
- Projection migrations for Unity world, local player, remote clones, network, save, diagnostics.
- Domain isolation architecture guards.
- Removal of stale dual state as each domain switches.

Out of scope:

- New gameplay feature development unless required to migrate an existing fact.
- Generic ECS replacement.
- Anti-cheat/strict validation.
- Final legacy deletion (Phase E finishes it, but each domain should delete its own old state immediately).

## Recommended migration order and rationale

1. **World / Run / Epoch**
   - Provides the run identity, seed, layer, stage, world generation results, and epoch
     boundary needed by every other domain.
   - Epoch filtering is the root protection against cross-run residue.
2. **Traps and Building Entities**
   - Bounded state machines (`Armed`, `Warning`, `Triggered`, `Cooldown`, `Disabled`).
   - Trap results (damage/drop) are a good cross-domain batch exercise.
3. **Player terminal state and cross-player interaction**
   - Death, unconsciousness, limb terminal state, carry/release, and item interaction
     relations are explicit domain events.
   - High-frequency movement stays a stream; terminal facts become kernel events.
4. **Enemy / Entity**
   - Shared entity identity, lifecycle, health, targeting/combat terminal states.
   - Existing enemy sync/replay logic is absorbed as projections and NativeObservations.
5. **Fluid persistent region**
   - Split authoritative regional totals/types from high-frequency simulation/visual grids.
   - Host periodically commits region checkpoints; guests rebuild local simulation.
6. **High-frequency stream unification**
   - Align continuous state streams with the kernel without creating/destroying
     aggregates or changing ownership/container relations.

## Per-domain transition template

For each domain, complete these steps in order:

1. **Inventory** — list authoritative facts, current owners, Unity/native objects, network messages, save fields, and replay surfaces.
2. **Shadow model** — build a kernel domain module beside the existing path without changing behavior.
3. **Differential validation** — compare old terminal facts with kernel facts over replay/simulation.
4. **Authority switch** — route all mutations through kernel Commands/NativeObservations; old path becomes read-only.
5. **Projection migration** — old tables/caches become projections; define rebuild from kernel query/checkpoint.
6. **Checkpoint integration** — include domain state in GameCheckpoint and save round-trip.
7. **Network integration** — wire domain events/streams into the Phase C envelopes.
8. **Delete old state** — remove the domain's old authoritative representation and shallow cooperation tests once the deep module covers behavior.
9. **Guard activation** — add domain isolation/invariant/checkpoint guards for this domain.
10. **Evidence** — write a self-check fact sheet and tech decisions.

## Domain-specific work breakdown

### 4.1 World / Run / Epoch

- [x] Define `RunState`: run identity, seed, layer, stage, world generation results,
      global rules, checkpoint epoch.
- [x] Define epoch commands/events: start run, switch epoch, restore epoch.
- [x] Move run/seed/world generation facts into kernel; remove session-local cross-run caches.
- [x] Make `RunEpoch` the filter for all Commands, Batches, and stream packets.
- [x] Add epoch isolation property tests: no old-epoch entity survives after switch.
- [x] Add world determinism checkpoint fields (random streams/world-gen results).

### 4.2 Traps and Building Entities

- [x] Define trap state machines and events (`Armed`, `Warning`, `Triggered`, `Cooldown`,
      `Disabled`).
- [ ] Define building/entity lifecycle and health events.
- [ ] Move trap trigger + damage/drop into one cross-domain batch.
- [ ] Turn trap presentation into projection/replay, not authority.
- [ ] Migrate trap replay/snapshot logic to kernel events.
- [x] Add invariant tests: traps cannot skip legal states; destroyed entities cannot accept damage.

### 4.3 Player terminal state and cross-player interaction

- [ ] Define player domain: identity, terminal health/limb states, skills, backpack root,
      interaction relations, durable state.
- [ ] Move death/unconscious/carry/release/cross-player take/heal/use terminal facts into kernel.
- [ ] Keep movement and high-frequency local body fields as stream/projection.
- [ ] Define authority policies for cross-player interactions (owner predicted / host validated).
- [ ] Migrate prediction/rollback for cross-player operations from ad-hoc caches to Prediction Runtime.
- [ ] Add invariant tests: death + backpack/drop batch consistency; relation consistency.

### 4.4 Enemy / Entity

- [ ] Define entity domain: shared identity, lifecycle, health, state, targeting/combat terminal facts.
- [ ] Move enemy spawn/despawn/health/attack/lunge/bite/effect facts into kernel events.
- [ ] Keep high-frequency enemy positions/animations as stream/projection.
- [ ] Absorb existing `EnemyCombatDirector` style host decisions into kernel processes/policies.
- [ ] Migrate enemy replay/snapshot to kernel events.
- [x] Add invariant tests: no post-destroy damage; no duplicate operations on replay.

### 4.5 Fluids

- [x] Split authoritative fluid region totals/types from simulation/visual grids.
- [x] Define region checkpoint commands/events and periodic authoritative commits.
- [ ] Move guest local fluid simulation into rebuildable projection.
- [ ] Define convergence fields and forbidden stream operations (no aggregate creation/destruction).
- [x] Add property tests for region/total invariants under random updates.

### 4.6 High-frequency stream unification

- [x] Define stream projection as an update-only mechanism for convergent fields.
- [x] Ensure streams cannot create/destroy aggregates, change ownership/container relations, or advance key state machines.
- [x] Replace ad-hoc per-domain stream code with kernel-validated stream updates where possible.
- [x] Ensure terminal states are promoted to domain events, never left to stream convergence.
- [x] Add simulation tests: dropped/out-of-order stream packets converge without violating invariants.

## Exit criteria

- All persistent gameplay facts across the six domain areas have a single authoritative
  write entry in the kernel.
- Every domain has a typed module, checkpoint inclusion, and replay/reduce path.
- Cross-domain operations are atomic batches, not multi-table ad-hoc writes.
- RunEpoch isolation is enforced at every command/batch/packet boundary.
- Projections for world, local player, remote clones, network, save, and diagnostics can
  be rebuilt from checkpoint + batches.
- Old authoritative tables/caches are projections or removed for each migrated domain.
- Domain isolation guards pass: no cross-domain private namespace access.
- Full test suite plus new property/simulation tests pass.
- User-observable replay semantics remain equivalent for migrated domains.

## Verification design

- Per-domain shadow differential before authority switch.
- Kernel property tests per domain.
- Cross-domain transaction tests.
- Network simulation for domain-specific events.
- Checkpoint round-trip tests including random streams.
- Existing replay traces extended to cover each domain.
- Full build, format, architecture/event/entity gates.
- L0 + static evidence, no manual acceptance.

## Deliverables

- Migrated domain modules for World/Run, Traps/Buildings, Players, Enemies/Entities, Fluids.
- Cross-domain process/policy orchestration.
- Updated checkpoints, protocol payloads, projections.
- Per-domain self-check fact sheets and tech decisions.
- Deleted old domain authority tables as each migration completes.

## Open questions / risks

| Risk | Mitigation |
|---|---|
| Scope is large and may span many sessions | Keep the domain order and per-domain template; each domain is independently deliverable. |
| Cross-domain transactions may reveal missing invariants | Add property tests and write explicit processes before broad migration. |
| Enemy/player interactions are complex | Prefer shadow mode and replay differential before switching authority. |
| Fluid simulation is high-frequency | Keep authoritative region totals small; avoid writing every grid cell as an event. |
| A domain may need new wire events | Phase C already established event payload IDs; extend them deliberately. |

## Session handoff

- Each domain should be its own series of sessions.
- Update `status.md` after each domain completes, not only after the whole phase.
- Record per-domain evidence in `docs/selfchecks/` and `docs/tech-decisions.md`.
- If a domain is blocked, record the exact blocker and the smallest next sub-step.

## Session log

| Date | Scope | Commits | Verification | Notes |
|---|---|---|---|---|
| 2026-08-29 | World/Run/Epoch kernel shadow + checkpoint/wire/save integration | `8fa118b` | 1638 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Kernel `RunState` + `StartRunCommand`/`AdvanceLayerCommand` + `RunStartedEvent`/`RunAdvancedEvent`; legacy `WorldStartParams` production path remains for the next authority-switch cycle. See `docs/selfchecks/phase-d-world-run-epoch-shadow-selfcheck.md`. |
| 2026-08-29 | World/Run/Epoch authority switch + legacy wire removal | `3c0eb93` | 1640 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Host `PublishWorldParams` commits `StartRunCommand`/`AdvanceLayerCommand`; guest projects run batches/checkpoints into `WorldStartParams`; handshake delivers a kernel checkpoint; `WorldStartParamsMsg`, `WorldParamsHandler`, `SettingEntryMsg`, and `NetMsg.WorldStartParams` removed. |
| 2026-08-29 | WorldEntities kernel shadow + checkpoint/wire/save integration | `e8fbb02` | 1647 tests green; build/format/architecture/event/entity/isolation gates pass | Kernel `WorldEntityState` + trap consumption/building health/opened entity commands and events; checkpoint/wire/save round-trip; runtime registries remain the live path for authority switch next. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |
| 2026-08-29 | WorldEntities authority switch + kernel-backed registry projections | `a04d040` | 1649 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Runtime `TrapConsumptionRegistry`/`OpenedEntityRegistry`/`BuildingEntityHealthRegistry` now commit through kernel and build snapshots from `QueryWorldEntities`; reset command clears the kernel table. |
| 2026-08-29 | WorldEntities guest checkpoint projection | `d49d7cd` | 1684 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `WorldEntityKernelProjection` raises guest checkpoint world-entity facts into `EntityEventSync`/`WorldEventSync`, giving the checkpoint-driven rebuild path needed before removing legacy snapshot wire. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |
| 2026-08-29 | Players terminal-status kernel shadow + checkpoint/wire/save | `41c22bb` | 1655 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Kernel `PlayerState`/`PlayerStateTable`, `UpdatePlayerStatusCommand`, `ResetPlayersCommand`, wire/save round-trip. Production player/cross-player path remains for the next authority-switch cycle. See `docs/selfchecks/phase-d-players-shadow-selfcheck.md`. |
| 2026-08-29 | Players terminal-status production wiring via entity-sync projection | `b016569` | 1656 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Host `EntitySyncService` publishes alive/conscious changes through `PlayerKernelStatusProjection` into kernel; architecture split keeps `EntitySyncService` under the line gate. |
| 2026-08-29 | Players carry relation kernel shadow + production projection | `fd65383` | 1683 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `PlayerState` carries reciprocal carry fields; set/clear commands/events, wire/checkpoint/save round-trip, and `PlayerKernelCarryProjection` commits host `PlayerCarryService` mutations into the kernel while the legacy wire mirror remains the live presentation path. See `docs/selfchecks/phase-d-players-shadow-selfcheck.md`. |
| 2026-08-29 | Enemy/Entity kernel shadow + entity-sync production projection | `11d2e88` | 1662 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Kernel `EnemyState`/`EnemyStateTable`, upsert/remove/reset commands/events, wire/save round-trip, host `EnemySyncService.PublishEnemyStates` change-gated kernel projection; `ItemKernelCodec`/`KernelDomainWireMapper` split to satisfy line gate. See `docs/selfchecks/phase-d-enemies-shadow-selfcheck.md`. |
| 2026-08-29 | Fluids persistent region kernel shadow + checkpoint/wire/save | `a94e40a` | 1667 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Kernel `FluidRegionState`/`FluidStateTable`, `UpdateFluidRegionCommand`, `ResetFluidsCommand`, wire/save round-trip. Production region commit/rebuild is the next sub-step. See `docs/selfchecks/phase-d-fluids-shadow-selfcheck.md`. |
| 2026-08-29 | Fluids host region kernel production wiring | `9fea849` | 1670 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Host `FluidRegionKernelSync` aggregates `FluidManager.fluid` into `WorldGeneration.CHUNKSIZE` chunks at a low cadence and `FluidKernelProjection` change-gates the authoritative upserts/clears; `IWorldControl.ReportFluidRegions` wires the adapter to the kernel. See `docs/selfchecks/phase-d-fluids-shadow-selfcheck.md`. |
| 2026-08-29 | High-frequency stream unification first slice: enemy explicit lifecycle | `1853bdf` | 1675 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Enemy 20 Hz state batch is now update-only; disappearing enemies travel as reliable `EnemyRemovedMsg`, `EnemyRemovedHandler` removes the guest buffer, and `EnemySyncCoordinator` destroys the local frozen copy. See `docs/selfchecks/phase-d-high-frequency-stream-unification-selfcheck.md`. |
| 2026-08-29 | WorldEntities legacy snapshot wire removal | `3a9628b` | 1680 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Removed `TrapStateSnapshot`, `OpenedEntitiesSnapshot`, and `BuildingEntityHealthSnapshot` message ids/classes/handlers and their world-entry/60 s send paths. World-entity backfill now rides `KernelEnvelope` checkpoint + `WorldEntityKernelProjection`; the 60 s host cycle resends the kernel checkpoint as the lazy-session fallback. Tests/replay moved from snapshot actions to checkpoint projection. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |
| 2026-08-29 | Players limb terminal facts | `b7c9e9e` | 1687 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `PlayerState` now carries `PlayerLimbState` discrete latch facts (broken/dismembered/dislocated/splinted/infected/blocked-bleeding/head/vital); wire/checkpoint/save round-trip; `PlayerKernelLimbProjection` commits from host character snapshots and limb-latch events; `PlayerKernelStatusProjection` now preserves carry/limbs on status updates. See `docs/selfchecks/phase-d-players-shadow-selfcheck.md`. |
| 2026-08-29 | Players body-level terminal latches | `current` | 1693 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `PlayerState` now carries `PlayerBodyTerminalState` discrete body terminal booleans (face latches, pulmonary embolism, last-stand/neural flags, fibrillation forced, mindwipe script); wire/checkpoint/save round-trip; `PlayerKernelLimbProjection` commits them from character snapshots, limb-latch events, and cross-player use. See `docs/selfchecks/phase-d-players-shadow-selfcheck.md`. |
| 2026-08-29 | Enemy stream resurrection guard | `current` | 1695 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `EnemySyncService` keeps a session-scoped removal tombstone set; late/out-of-order update-only state batches and full snapshots skip ids that already received an explicit removal. See `docs/selfchecks/phase-d-high-frequency-stream-unification-selfcheck.md`. |
| 2026-08-29 | Players carry kernel-wire cutover | `current` | 1694 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `PlayerCarryService` now commits carry through kernel commands only; `PlayerKernelCarryProjection` projects host `BatchCommitted` and guest `BatchApplied` into the carry mirror and `CarryStateChanged`, and rebuilds from checkpoint. `NetMsg.PlayerCarryState`, `PlayerCarryStateHandler`, and `FireCarryStateReceived` are removed. See `docs/selfchecks/phase-d-players-shadow-selfcheck.md`. |
| 2026-08-29 | Players cross-player item kernel host sync | `current` | 1694 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `PlayerInventoryTakeService`, `PlayerHealService`, and `PlayerItemUseService` now spawn/transfer/update/destroy host-recipient and wear-to-host carried items in the item kernel, closing the host-side item-ownership gap while guest recipients continue through the transfer-table adopt path. See `docs/selfchecks/phase-d-players-shadow-selfcheck.md`. |
| 2026-08-29 | Players guest kernel replay + destroyed guest-item cleanup | `current` | 1694 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Tests now assert the guest replay kernel receives the same take/use/wear item facts through `KernelEnvelope`; destroyed non-wear guest-owned items are removed from the kernel carried state instead of lingering after the transfer-table removal. See `docs/selfchecks/phase-d-players-shadow-selfcheck.md`. |
| 2026-08-29 | Player high-frequency stream lifecycle audit | `current` | 1696 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Player stream is now audited as update-only with explicit `PlayerJoin`/`PlayerLeave` lifecycle: a state batch missing a player does not remove the buffer, and `PlayerLeave` removes the remote buffer. See `docs/selfchecks/phase-d-high-frequency-stream-unification-selfcheck.md`. |
| 2026-08-29 | Enemy stream terminal revision guard | `current` | 1697 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `EnemyStateBatchMsg` carries the host kernel global revision; the guest tracks terminal health revisions from `EnemyUpsertedEvent`/checkpoint restore and refuses stale streams that would roll back health/stunned, while continuous position/velocity still converge. See `docs/selfchecks/phase-d-high-frequency-stream-unification-selfcheck.md`. |
| 2026-08-29 | Player/enemy high-frequency stream wire unification | `current` | 1694 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `WireStateStream` now carries `PlayerStates`/`EnemyStates` + seq; both 20 Hz streams ride `StateStreamEnvelope` over `KernelEnvelope`; old `NetMsg.PlayerState`/`PlayerStateReport`/`EnemyState`, their handlers and DTOs are removed; guest player reports are seq-gated per member on the host. See `docs/selfchecks/phase-d-high-frequency-stream-unification-selfcheck.md`. |
| 2026-08-29 | Players take/heal/use result kernel-event routing | `current` | 1699 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `RecordPlayerInventoryTransferCommand`/`RecordPlayerHealResultCommand`/`RecordPlayerItemUseResultCommand` journal take/heal/use results as Players domain events; `PlayerInteractionKernelProjection` restores `TransferReceived`/`HealReceived`/`UseReceived` from `BatchCommitted` (host) and `BatchApplied` (guest); `NetMsg.PlayerInventoryTransfer`/`PlayerHealResult`/`PlayerItemUseResult` and their handlers removed. See `docs/selfchecks/phase-d-players-shadow-selfcheck.md`. |
| 2026-08-29 | Players restore/reconnect kernel terminal projection | `current` | 1701 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `PlayerKernelRestoreProjection` overlays `PlayerStateTable` alive/conscious/limb latches/body-terminal latches onto the saved `CharacterDataMsg` in `CharacterDataStore.SendSavedCharacter`; continuous snapshot fields remain the authority for physiological values/items/position; carry continues through the checkpoint/committed-batch carry projection. See `docs/selfchecks/phase-d-players-shadow-selfcheck.md`. |
| 2026-08-29 | Enemies restore/reconnect kernel terminal projection | `current` | 1703 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `EnemyKernelRestoreProjection` overlays kernel `EnemyStateTable` health/stunned/prefab/runtime-spawn onto host world-entry/reconnect snapshot payloads and guest full-snapshot application; continuous enemy presentation fields remain snapshot/stream-owned. See `docs/selfchecks/phase-d-enemies-shadow-selfcheck.md`. |
| 2026-08-29 | Enemy combat terminal-state result kernel routing | `a52b94b`, `dcdbb10` | 1702 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `RecordEnemyBiteCommand`/`RecordEnemyLungeCommand`/`RecordEnemyEffectCommand` journal bite/lunge/proximity results as Entities domain events; `EnemyCombatKernelProjection` restores `EnemyBiteReceived`/`EnemyLungeReceived`/`EnemyEffectReceived` from `BatchCommitted` (host) and `BatchApplied` (guest); `NetMsg.EnemyBite`/`EnemyLunge`/`EnemyEffect` and their handlers removed; `EnemyAttackMsg` remains the separate host-ordered local-apply command. See `docs/selfchecks/phase-d-enemies-shadow-selfcheck.md`. |
| 2026-08-29 | Fluids guest kernel read projection | `5fc4083` | 1706 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `FluidKernelReadProjection` rebuilds a guest-side mirror of `FluidStateTable` from `CheckpointRestored` and `BatchApplied` (`FluidRegionUpdatedEvent`/`FluidsResetEvent`); `WorldService.FluidRegionFacts` exposes the read model while the high-frequency RLE grid stream remains the live view path. See `docs/selfchecks/phase-d-fluids-shadow-selfcheck.md`. |
| 2026-08-29 | WorldEntities destroyed-building invariant | `68eeffa` | 1708 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `WorldEntityDomainModule` now rejects a positive building-health report after a recorded zero-health (destroyed) fact, while preserving idempotent zero reports. Added invariant tests for both branches. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |
| 2026-08-29 | Enemy aggregate removal through kernel batches | `09468d5` | 1707 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Guest `EnemySyncService` now applies `EnemyRemovedEvent` from `BatchApplied` and raises `EnemyRemovedReceived`; host no longer sends `EnemyRemovedMsg`. `NetMsg.EnemyRemoved`, `EnemyRemovedMsg`, `EnemyRemovedHandler`, and `IEnemySyncControl.ApplyEnemyRemoved` are removed; `ProtocolVersion.Current` bumped to 53. See `docs/selfchecks/phase-d-high-frequency-stream-unification-selfcheck.md`. |
| 2026-08-29 | Enemy removal terminal tombstones + replay safety | `92b1c98`, `9134257` | 1712 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `EnemyStateTable` now carries terminal `Removed` tombstones; a post-removal `UpsertEnemyCommand` is rejected with `InvalidTransition`; an `EnemyUpsertedEvent` replay for a removed id is a no-op; tombstones ride checkpoint/wire/save (`WireCheckpoint.RemovedEnemies`/`KernelSaveFile.RemovedEnemies`) and guest `EnemySyncService` seeds `_removedEnemies` from checkpoint restore. `ProtocolVersion.Current` reset to 1 as the unreleased baseline. See `docs/selfchecks/phase-d-enemies-shadow-selfcheck.md`. |
| 2026-08-29 | Trap state machine kernel shadow | `d363168` | 1717 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Added `TrapPhase` (`Armed`/`Warning`/`Triggered`/`Cooldown`/`Disabled`), `TrapStateFact`, `RecordTrapStateCommand`, and `TrapStateChangedEvent` to the WorldEntities kernel. The domain rejects illegal transitions and treats `Disabled` as terminal; trap states ride checkpoint/wire/save and are covered by wire batch/command round-trip, checkpoint/save round-trip, and invariant tests. Production reporting is not hooked yet — this is the shadow-model foundation for 4.2. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |
| 2026-08-29 | Trap state production reporting | `609b825` | 1718 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `TrapStateProfiles` maps live `EntityEventKind` edges to `TrapPhase`; `TrapStateRegistry` commits `RecordTrapStateCommand` from host-local `SendEntityEvent` and from the host-apply path for guest-triggered events. Guests receive the same kernel state batches through `KernelEnvelope`. Guest view projection is the remaining 4.2 sub-step. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |
| 2026-08-29 | Trap state guest checkpoint projection | `52394ec` | 1719 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `WorldEntityKernelProjection` now projects non-one-shot trap state facts alongside one-shot consumptions on guest checkpoint restore, while intentionally skipping transient `Warning` edges. This lets a late joiner replay durable non-one-shot state (clamp, turret fire, heat state, ...) from the kernel checkpoint. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |
| 2026-08-29 | Epoch isolation property tests | `c3527f1` | 1722 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | New `EpochIsolationTests` cover all kernel domain tables: a fresh epoch kernel has no residue from the previous epoch, and old-epoch commands/batches are rejected. Completes the last 4.1 item. See `docs/selfchecks/phase-d-world-run-epoch-shadow-selfcheck.md`. |
| 2026-08-29 | Trap state profile classification tests | `c20d701` | 1737 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `TrapStateProfilesTests` lock the `EntityEventKind` → `TrapPhase` classification, including the explicit visual-only null set, so new trap kinds are deliberately classified. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |
| 2026-08-29 | Atomic composite kernel commands | `354dca4` | 1740 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Added `CompositeGameCommand`: the kernel executes several typed domain commands as one atomic batch, routing each emitted event to its owning reducer, and rejects the whole batch if any inner command is rejected. Tests cover cross-domain item+player atomic commit, all-or-nothing rejection, and guest replay. This is the infrastructure prerequisite for the 4.2 trap trigger + damage/drop cross-domain batch. |
| 2026-08-29 | Trap trigger kernel facts atomic composite | `70e6e0a` | 1741 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | Host-local `EntityEventChannel.SendEntityEvent` and the host-apply path for guest-triggered events now commit the trap one-shot consumption and trap state-machine transition as one `CompositeGameCommand` batch. `IWorldControl.ReportTrapEvent` is the production entry; the old two-batch `ReportTrapConsumed`+`ReportTrapState` host path is replaced. Damage/drop cross-domain collection remains the next 4.2 sub-step. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |
| 2026-08-29 | Destructive trap health folded into atomic composite | `a662664` | 1741 tests green; build/format/architecture/event/entity/isolation/delivery gates pass | `EntityEventSync` reads the post-trigger zero health for `MineExploded`/`TurretSelfDestructed`/`CrystalFragileBroken`/`CrystalUnstableExploded` and `ReportTrapEvent` adds `RecordBuildingEntityHealthCommand` to the same atomic batch. The remaining 4.2 cross-domain item-drop/multi-entity damage collection is still open. See `docs/selfchecks/phase-d-world-entities-shadow-selfcheck.md`. |

## Next actions

1. [x] Route the remaining cross-player interaction results (take/heal/use/push)
   through kernel commands/events where they carry durable facts. Take/heal/use
   now ride journal-only Players domain events and the projection restores the
   Game Adapter event surface; push is confirmed presentation-only.
2. [x] Project kernel player terminal facts into character restore/reconnect
   snapshots where the legacy snapshot stream is no longer authoritative.
3. [x] Continue high-frequency stream alignment for player/enemy continuous fields
   with `WireStateStream` / `StateStreamEnvelope`, keeping terminal facts on
   domain events.
