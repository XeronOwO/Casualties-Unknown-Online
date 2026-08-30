# Domain Model and Ownership

This is the domain-model layer of the current typed kernel. It answers the
questions a new contributor needs before adding a feature:

- What state is authoritative and who owns it?
- What is a projection and what may be dropped/rebuild?
- What may travel as a high-frequency stream, and what must become a domain event?

Reading path: [current-architecture.md](current-architecture.md) first, then
[domains.md](domains.md), then [protocol.md](protocol.md).

## Kernel core

The kernel itself owns only the transaction, revision, idempotency, checkpoint, and
projection-dispatch mechanics. Domain rules live in domain modules.

| Piece | Location | Purpose |
|---|---|---|
| `IGameStateKernel` / `GameStateKernel` | `src/CasualtiesUnknownOnline.GameState/GameStateKernel.cs`, `IGameStateKernel.cs` | `Execute`, `Apply`, `CreateCheckpoint`, `Restore`, and read queries. |
| `CommittedBatch` | `src/CasualtiesUnknownOnline.GameState/CommittedBatch.cs` | One atomic set of accepted facts; the only confirmation channel. |
| `GameCheckpoint` | `src/CasualtiesUnknownOnline.GameState/GameCheckpoint.cs` | Complete authoritative state snapshot at a revision. |
| `CommandContext` | `src/CasualtiesUnknownOnline.GameState/CommandContext.cs` | Explicit deterministic inputs (actor, authority, epoch, random). |
| `RejectionReason` | `src/CasualtiesUnknownOnline.GameState/RejectionReason.cs` | Typed rejection vocabulary. |
| `AuthorityKind` | `src/CasualtiesUnknownOnline.GameState/AuthorityKind.cs` | Declared authority policy of a command. |
| `IDomainModule` | `src/CasualtiesUnknownOnline.GameState/Kernel/IDomainModule.cs` | `Decide` / `Reduce` / `AssertInvariants` per domain. |
| `GameStateStore` | `src/CasualtiesUnknownOnline.GameState/Kernel/GameStateStore.cs` | The authoritative store and operation-idempotency window. |

The current domain modules registered in `GameStateKernel` are:

```csharp
new ItemDomainModule(),
new WorldDomainModule(),
new WorldEntityDomainModule(),
new PlayerDomainModule(),
new EnemyDomainModule(),
new FluidDomainModule()
```

## Domain table

| Domain | Kernel state | Representative commands | Representative events | Authority scope | Runtime projections |
|---|---|---|---|---|---|
| World / Run | `RunState` | `StartRunCommand`, `AdvanceLayerCommand` | `RunStartedEvent`, `RunAdvancedEvent` | Run identity, seed, layer, stage, global rules, epoch isolation | Kernel checkpoint handoff through `KernelProtocolService` |
| WorldEntities | `WorldEntityState` | `RecordTrapStateCommand`, `RecordTrapConsumedCommand`, `RecordOpenedEntityCommand`, `RecordBuildingEntityHealthCommand`, `ResetWorldEntitiesCommand` | `TrapStateChangedEvent`, `TrapConsumedEvent`, `OpenedEntityEvent`, `BuildingEntityHealthUpdatedEvent`, `WorldEntitiesResetEvent` | Trap phase/consumption, opened-entity facts, building health | `WorldEntityKernelProjection` |
| Players | `PlayerStateTable` / `PlayerState` | `UpdatePlayerStatusCommand`, `SetPlayerCarryCommand`, `ClearPlayerCarryCommand`, `RecordPlayerInventoryTransferCommand`, `RecordPlayerHealResultCommand`, `RecordPlayerItemUseResultCommand`, `ResetPlayersCommand` | `PlayerStatusUpdatedEvent`, `PlayerCarrySetEvent`, `PlayerCarryClearedEvent`, `PlayerInventoryTransferEvent`, `PlayerHealResultEvent`, `PlayerItemUseResultEvent`, `PlayersResetEvent` | Terminal health, body/limb latches, skills, carry relations, cross-player result facts | `PlayerKernelStatusProjection`, `PlayerKernelLimbProjection`, `PlayerKernelRestoreProjection`, `PlayerKernelCarryProjection`, `PlayerInteractionKernelProjection` |
| Items | `ItemState` (kernel item table) | `SpawnItemCommand`, `PickUpItemCommand`, `DropItemCommand`, `DestroyItemCommand`, `TransferItemCommand`, `UpdateItemStateCommand`, `SyncContainerItemsCommand`, `CookItemCommand` | `ItemSpawnedEvent`, `ItemRelocatedEvent`, `ItemDestroyedEvent`, `ItemDataUpdatedEvent` | Item identity, location (World/Carried/Contained/Terminal), payload, container graph | `ItemProjection`, `KernelBatchItemProjection` |
| Entities / Enemies | `EnemyStateTable` / `EnemyState` | `UpsertEnemyCommand`, `RemoveEnemyCommand`, `ResetEnemiesCommand`, `RecordEnemyBiteCommand`, `RecordEnemyLungeCommand`, `RecordEnemyEffectCommand` | `EnemyUpsertedEvent`, `EnemyRemovedEvent`, `EnemiesResetEvent`, `EnemyBiteResultEvent`, `EnemyLungeResultEvent`, `EnemyEffectResultEvent` | Enemy lifecycle/health and combat terminal facts | `EnemyKernelProjection`, `EnemyKernelRestoreProjection`, `EnemyCombatKernelProjection` |
| Fluids | `FluidStateTable` / `FluidRegionState` | `UpdateFluidRegionCommand`, `ResetFluidsCommand` | `FluidRegionUpdatedEvent`, `FluidsResetEvent` | Coarse authoritative region totals/checkpoints, not every fluid pixel | `FluidKernelProjection`, `FluidKernelReadProjection` |

Source roots:

- World/Run: `src/CasualtiesUnknownOnline.GameState/Domains/World/`
- WorldEntities: `src/CasualtiesUnknownOnline.GameState/Domains/WorldEntities/`
- Players: `src/CasualtiesUnknownOnline.GameState/Domains/Players/`
- Items: `src/CasualtiesUnknownOnline.GameState/Domains/Items/`
- Entities/Enemies: `src/CasualtiesUnknownOnline.GameState/Domains/Entities/`
- Fluids: `src/CasualtiesUnknownOnline.GameState/Domains/Fluids/`

## Ownership rules

1. **One authoritative write per persistent fact.** The kernel domain table is the
   writer; Unity objects, UI, network caches, remote clones, and save files are
   projections.
2. **Continuous high-frequency fields are not kernel facts.** Position, velocity,
   aim, and presentation-only fields may ride `StateStreamEnvelope`; they may not
   create/destroy aggregates, change ownership, or advance a key state machine.
3. **Terminal facts must be domain events.** Death, unconsciousness, limb loss,
   ownership transfer, trap consumption, enemy removal, and cooking results travel
   as kernel events or committed batches, not as the last UDP tick.
4. **Projections are rebuildable.** A projection may be cleared and rebuilt from
   checkpoint + committed batches. It never corrects authority.
5. **Cross-domain operations are typed processes, not kernel switch statements.**
   `CompositeGameCommand` carries inner domain commands; see
   `src/CasualtiesUnknownOnline.GameState/CompositeGameCommand.cs` and
   `GameStateKernel.ExecuteComposite`.
6. **Kernel isolation is hard.** `CasualtiesUnknownOnline.GameState` has no
   Unity/BepInEx/Steam/network references and no Protocol DTO dependencies. The
   guards in `tools/check-architecture.ps1` enforce this.

## Predictions and no-prediction boundaries

Cross-player interactions are `HostValidatedNoPrediction`: take, heal, use, and
carry set/clear are host-validated and are not client-predicted. Push is
`PresentationOnly`. See `docs/tech-decisions.md` #154 and #157. The generic
Prediction Runtime remains future work in `docs/backlog.md`.

## Evidence

- Kernel implementation and domain dispatch: `src/CasualtiesUnknownOnline.GameState/GameStateKernel.cs`
- Checkpoint shape: `src/CasualtiesUnknownOnline.GameState/GameCheckpoint.cs`
- Domain module interface: `src/CasualtiesUnknownOnline.GameState/Kernel/IDomainModule.cs`
- Projections live under: `src/CasualtiesUnknownOnline.Runtime/Session/` (Items,
  CharacterData, EntitySync, PlayerInteraction, World)
- Authority/prediction boundary: `docs/tech-decisions.md` #154, #157
- Phase D full migration evidence: `docs/selfchecks/phase-d-full-domain-migration-selfcheck.md`
