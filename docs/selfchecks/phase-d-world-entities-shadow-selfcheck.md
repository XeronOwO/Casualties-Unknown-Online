# Phase D WorldEntities self-check (2026-08-29)

This fact sheet records the Phase D Traps/Building Entities domain cycle: the
WorldEntities kernel domain (one-shot trap consumptions, building-entity
health, opened lockable entities), its checkpoint/wire/save round-trip, the
production authority switch, and the conversion of the three runtime registries
into kernel-backed projection/snapshot adapters.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Entity position | `Domains/WorldEntities/EntityPosition.cs` | Integer world-cell identity; matches the runtime position-keyed registries. |
| Facts | `TrapConsumptionFact`, `BuildingEntityHealthFact`, `OpenedEntityFact`, `TrapStateFact` | One authoritative fact for one-shot traps, building health, opened entities, and trap state-machine phases. |
| Aggregate | `WorldEntityState.cs` | Immutable snapshot with idempotent/upsert semantics. |
| Commands | `RecordTrapConsumedCommand`, `RecordBuildingEntityHealthCommand`, `RecordOpenedEntityCommand`, `RecordTrapStateCommand`, `ResetWorldEntitiesCommand` | Host-only commands for the authority model; reset clears all world-entity facts for a new layer. |
| Events | `TrapConsumedEvent`, `BuildingEntityHealthUpdatedEvent`, `OpenedEntityEvent`, `TrapStateChangedEvent`, `WorldEntitiesResetEvent` | Reduce into the aggregate. |
| Domain module | `WorldEntityDomainModule.cs` | Decide/reduce/invariant; rejects invalid trap kind/health, rejects health reports that would revive a destroyed building entity, rejects illegal trap state transitions (Disabled is terminal), and preserves the runtime table caps. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `WorldEntityState?` is now a kernel domain table and checkpoint field. |
| Wire DTOs | `WireWorldEntityState`, `WireTrapConsumption`, `WireBuildingEntityHealth`, `WireOpenedEntity`, `WireTrapState`, `WireEntityPosition` | Protocol remains GameState-free. |
| Mapper/save | `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | World-entity facts round-trip through wire checkpoints and disk saves. |
| Runtime authority surface | `ItemKernelAuthority.TryRecordTrapConsumed/TryRecordBuildingEntityHealth/TryRecordOpenedEntity/TryResetWorldEntities/QueryWorldEntities` | Host commands and query entry points. |
| Registry projections | `TrapConsumptionRegistry`, `OpenedEntityRegistry`, `BuildingEntityHealthRegistry` | The three old registries now commit through the kernel; they are no longer independent fact stores. The legacy snapshot payload builders were removed with the wire. |
| Trap state production | `TrapStateProfiles` + `TrapStateRegistry` + `EntityEventSync` | Host-local `EntityEventChannel.SendEntityEvent` and the host-apply path for guest reports commit `RecordTrapStateCommand` for every stateful `EntityEventKind` edge. |
| Guest checkpoint projection | `WorldEntityKernelProjection` + `EntityEventSync`/`WorldEventSync` | Guest `CheckpointRestored` raises one-shot consumption facts and non-one-shot trap state facts (transient `Warning` edges excluded) as the flat replay list the Game Adapter applies. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel records and queries all three fact kinds | `WorldEntityDomainKernelTests.RecordFacts_UpdatesKernelWorldEntityState`. |
| Same-position trap consumption upserts | `WorldEntityDomainKernelTests.SamePositionTrap_UpsertsInsteadOfDuplicating`. |
| Same-position opened entity is idempotent | `WorldEntityDomainKernelTests.SamePositionOpened_IsIdempotent`. |
| Destroyed building rejects a positive health report | `WorldEntityDomainKernelTests.DestroyedBuilding_RejectsPositiveHealthReport`. |
| Destroyed building still allows an idempotent zero report | `WorldEntityDomainKernelTests.DestroyedBuilding_AllowsIdempotentZeroHealthReport`. |
| A committed batch replays on a guest kernel | `WorldEntityDomainKernelTests.Apply_RunEntityBatch_ReplaysFactsOnGuestKernel`. |
| Wire batch preserves a trap event | `WorldEntityDomainKernelTests.WireBatchRoundTrip_PreservesTrapConsumedEvent`. |
| Checkpoint chunks preserve the aggregate | `WorldEntityDomainKernelTests.CheckpointSplitAssemble_RoundTripsWorldEntityState`. |
| Save/load preserves the aggregate | `WorldEntityDomainKernelTests.SaveLoad_RoundTripsWorldEntityState`. |
| Reset command clears all world-entity facts | `WorldEntityDomainKernelTests.ResetWorldEntities_ClearsAllFacts`. |
| Trap state facts update and upsert in the kernel | `WorldEntityDomainKernelTests.RecordTrapState_UpdatesKernelWorldEntityState`. |
| Illegal trap state transitions are rejected | `WorldEntityDomainKernelTests.IllegalTrapStateTransition_IsRejected`. |
| Disabled trap state is terminal | `WorldEntityDomainKernelTests.DisabledTrapState_IsTerminal`. |
| Wire batch preserves a trap state event | `WorldEntityDomainKernelTests.WireBatchRoundTrip_PreservesTrapStateChangedEvent`. |
| Wire command round-trips `RecordTrapStateCommand` | `WorldEntityDomainKernelTests.WireCommandRoundTrip_BuildsRecordTrapStateCommand`. |
| Host world control reports commit kernel facts | `WorldEntityProjectionTests.HostReports_CommitKernelWorldEntities`. |
| Host world control reports commit kernel trap state facts | `WorldEntityProjectionTests.HostReports_CommitKernelTrapStateFacts`. |
| Live `EntityEventKind` → `TrapPhase` classification is locked | `TrapStateProfilesTests.StatefulKinds_MapToExpectedPhase`, `VisualOnlyKinds_RemainUnclassified`. |
| Guest checkpoint restore projects non-one-shot trap state facts | `WorldEntityProjectionTests.GuestCheckpointRestore_ProjectsNonOneShotTrapStateFacts`. |
| Guest checkpoint restore projects world-entity facts | `WorldEntityProjectionTests.GuestCheckpointRestore_ProjectsKernelWorldEntities`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1737 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed.

## Structure review

- `GameState` remains dependency-free: WorldEntities uses only System collections
  and kernel primitives.
- One top-level type per new file.
- The domain sits behind the same `IDomainModule` seam as Items and World/Run.

## Next sub-steps

1. [x] The guest checkpoint projection has landed; the legacy snapshot frames
   (`TrapStateSnapshot`, `OpenedEntitiesSnapshot`,
   `BuildingEntityHealthSnapshot`), their handlers/messages and their periodic
   resend path were removed. World-entity backfill now rides the kernel
   checkpoint (`KernelEnvelope`) and `WorldEntityKernelProjection`.
2. Continue with the remaining player domain supplements and cross-player
   interaction migration.
3. [x] Trap state-machine shadow landed: `TrapPhase`, `TrapStateFact`,
   `RecordTrapStateCommand`, and `TrapStateChangedEvent` define the 4.2 state
   vocabulary in the kernel, with illegal-transition/disabled-terminal
   invariants and checkpoint/wire/save round-trip.
4. [x] Live production reporting landed: `TrapStateProfiles` maps
   `EntityEventKind` edges to `TrapPhase`, and `TrapStateRegistry` commits
   `RecordTrapStateCommand` from host-local and guest-triggered host-apply
   paths.
5. [x] Guest checkpoint projection landed: non-one-shot trap state facts are
   included in the late-joiner replay list, while transient `Warning` edges are
   intentionally not snapshotted.
6. [x] Atomic trap trigger kernel facts landed: the host-local channel and the
   host-apply path commit the one-shot consumption plus the trap state
   transition as one `CompositeGameCommand` batch through
   `IWorldControl.ReportTrapEvent`. Next is the cross-domain damage/drop
   batch.
