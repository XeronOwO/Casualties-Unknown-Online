# Phase D Enemies/Entities shadow self-check (2026-08-29)

This fact sheet records the Phase D Enemy/Entity domain cycles: a kernel
enemy/entity lifecycle-health table, checkpoint/wire/save integration, and
production wiring from the host enemy-sync publish path into the kernel.
High-frequency enemy position/velocity remains a stream; the kernel owns the
durable entity identity/health/runtime-spawn facts.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Entity identity | `Domains/Entities/EntityId.cs` | Epoch/counter/generation matching the Runtime NetworkEntityId shape. |
| Enemy fact | `Domains/Entities/EnemyState.cs` | Prefab, health, runtime-spawn, stunned. |
| Enemy table | `Domains/Entities/EnemyStateTable.cs` | Immutable snapshot with upsert/remove plus terminal `Removed` tombstones. |
| Commands | `UpsertEnemyCommand`, `RemoveEnemyCommand`, `ResetEnemiesCommand` | Host-only lifecycle/health commands; `UpsertEnemyCommand` rejects a removed id. |
| Events | `EnemyUpsertedEvent`, `EnemyRemovedEvent`, `EnemiesResetEvent` | Reduce into the table; an upsert event for a removed id is a replay-safe no-op. |
| Domain module | `EnemyDomainModule.cs` | Decide/reduce/invariant; invalid health rejected, unique entity ids enforced, post-removal upserts rejected. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `EnemyStateTable?` is a kernel domain table and checkpoint field. |
| Wire DTOs | `WireEnemyState`, `WireEntityId`, `WireCheckpoint.RemovedEnemies`, `KernelSaveFile.RemovedEnemies` | Protocol remains GameState-free; tombstones ride checkpoint and save containers. |
| Mapper/save | `KernelDomainWireMapper`, `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | Enemy facts and terminal tombstones round-trip through wire checkpoints and disk saves. |
| Entity-sync projection | `EnemyKernelProjection` + `EnemySyncService` | Host `PublishEnemyStates` change-gated projects the enemy set into kernel and removes stale entries. |
| Restore projection | `EnemyKernelRestoreProjection` + `EnemySyncService` | Host world-entry/reconnect snapshots and guest full-snapshot application overlay kernel `EnemyStateTable` terminal facts (health, stunned, prefab, runtime-spawn) onto `EnemyEntity`; continuous presentation fields (position/velocity/rotation/legs/telegraph) remain from the snapshot/stream. |
| Combat-result payloads | `EnemyCombatLimb`, `EnemyCombatEffectKind` | Kernel-shaped post-bite/lunge limb and proximity-effect discriminator; no Runtime DTOs leak into GameState. |
| Combat-result commands | `RecordEnemyBiteCommand`, `RecordEnemyLungeCommand`, `RecordEnemyEffectCommand` | Host-only journal commands; no table mutation, the events are presentation/result facts for projections. |
| Combat-result events | `EnemyBiteResultEvent`, `EnemyLungeResultEvent`, `EnemyEffectResultEvent` | Journal-only Entities domain events; `EnemyDomainModule.Reduce` keeps the current enemy table unchanged. |
| Combat result submitter | `EnemyCombatKernelSubmitter` + `EnemySyncService` | Host reports commit journal commands directly; guest reports ride `CommandEnvelope` to the host. |
| Combat result projection | `EnemyCombatKernelProjection` + `EnemySyncService` | Restores `EnemyBiteReceived` / `EnemyLungeReceived` / `EnemyEffectReceived` from `BatchCommitted` (host) and `BatchApplied` (guest); host also merges guest reports into the saved character snapshot; source victims are skipped. |
| Combat result wire | `WireEnemyCombat`, `WireEventKind.EnemyBiteResult/EnemyLungeResult/EnemyEffectResult`, `WireCommandKind.RecordEnemyBite/RecordEnemyLunge/RecordEnemyEffect` | Protocol remains GameState-free; `EnemyCombatWireMapper`/`EnemyCombatKernelCodec` keep the Runtime mapping boundary. |
| Combat policy constants | `EnemyCombatPolicy` | Pure Runtime thresholds extracted from `EnemyCombatDirector` (spider bite range, crystal close/ray length, lunge tolerance), lockable by tests. |
| Lunge trace detail | `CrystalLungeTrace` | Top-level adapter type split out of `EnemyCombatDirector` so the director stays focused on ordering/reporting. |
| Target resolver | `EnemyTargetResolver` + `EnemyTarget` | Extracted the candidate-set building/finding/limb-index responsibility from `EnemyCombatDirector`; the director now only orders/reports using the resolver’s results. |
| Order policy | `EnemyCombatOrderPolicy` | Pure `ApplyPath` decision surface for the remaining `EnemyCombatDirector` ordering choices: remote order vs local native for spider bite/crystal lunge, and host item fallback for item-vs-enemy hits. The director still owns the Unity-side execution/reporting. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel upserts/removes/resets enemies | `EnemyDomainKernelTests.UpsertRemoveAndReset_DriveEnemyTable`. |
| Invalid health rejected | `EnemyDomainKernelTests.NegativeHealth_IsRejectedByInvariant`. |
| Wire batch preserves an enemy event | `EnemyDomainKernelTests.WireBatchRoundTrip_PreservesEnemyUpsertedEvent`. |
| Checkpoint chunks preserve enemies | `EnemyDomainKernelTests.CheckpointSplitAssemble_RoundTripsEnemies`. |
| Save/load preserves enemies | `EnemyDomainKernelTests.SaveLoad_RoundTripsEnemies`. |
| Post-removal upsert is rejected and reset restarts lifecycle | `EnemyDomainKernelTests.UpsertRemoveAndReset_DriveEnemyTable`. |
| Replay upsert after removal does not resurrect | `EnemyDomainKernelTests.ReplayUpsertAfterRemoval_DoesNotResurrect`. |
| Duplicate removal keeps a single tombstone | `EnemyDomainKernelTests.RemoveTwice_KeepsSingleTombstone`. |
| Checkpoint chunks preserve tombstones | `EnemyDomainKernelTests.CheckpointSplitAssemble_RoundTripsRemovedTombstones`. |
| Save/load preserves tombstones | `EnemyDomainKernelTests.SaveLoad_RoundTripsRemovedTombstones`. |
| Guest checkpoint restore seeds the removed set | `EnemySyncServiceTests.CheckpointTombstone_SeedsGuestRemovedSet`. |
| Host enemy publish commits kernel facts | `EnemyProjectionTests.HostPublishEnemyStates_CommitsKernelEnemyTable`. |
| Host world-entry snapshot projects kernel terminal facts | `EnemySyncServiceTests.HostSendEnemySnapshot_ProjectsKernelTerminalFacts`. |
| Guest full-snapshot apply projects kernel terminal facts | `EnemySyncServiceTests.GuestApplyEnemySnapshot_ProjectsKernelTerminalFacts` (health, stunned, prefab, runtime-spawn from kernel; snapshot position remains). |
| Combat-result commands commit journal events | `EnemyDomainKernelTests.RecordEnemyBite_CommitsJournalEvent`, `RecordEnemyLunge_CommitsJournalEvent`, `RecordEnemyEffect_CommitsJournalEvent`. |
| Combat-result wire batch round-trips | `EnemyDomainKernelTests.WireBatchRoundTrip_PreservesEnemyBiteResultEvent`, `WireBatchRoundTrip_PreservesEnemyLungeResultEvent`, `WireBatchRoundTrip_PreservesEnemyEffectResultEvent`. |
| Guest combat-result command decodes on host | `EnemyDomainKernelTests.WireCommandRoundTrip_BuildsRecordEnemyBiteCommand`. |
| Host/guest combat-result projection restores presentation events | `EnemyBiteSyncTests`, `EnemyLungeSyncTests`, `EnemyEffectSyncTests` (host-own and guest-report star semantics; source victim skipped). |
| Enemy combat policy constants are locked | `EnemyCombatPolicyTests` (spider bite range, crystal close range, lunge ray/tolerance). |
| Enemy target resolver contract is locked | `EnemyTargetResolverContractTests` (resolver exposes `BuildCandidates`/`Find`/`Facts`/`SelectLimbIndex`/`LocalBody`, `EnemyTarget.ToFact`). |
| Enemy combat order policy is locked | `EnemyCombatOrderPolicyTests` (null/remote/local for spider bite and crystal lunge, native/fallback/none for item hits). |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1780 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation/delivery gates passed; architecture split added
  `ItemKernelCodec`, `KernelDomainWireMapper`, `EnemyCombatKernelCodec`,
  `EnemyCombatWireMapper`, and `EnemyCombatOrderPolicy` to keep large types under 600.

## Structure review

- `GameState` remains dependency-free.
- One top-level type per new file.
- The Enemy domain is isolated behind the same `IDomainModule` seam.

## Next sub-steps

1. [x] Add enemy combat/terminal-state events (bite/lunge/effect) as
   journal-only kernel batches; `EnemyAttackMsg` remains the separate
   host-ordered local-apply command.
2. [x] Project kernel enemy facts into `EnemyEntity` restore/snapshot paths:
   `EnemyKernelRestoreProjection` overlays kernel health/stunned/prefab/runtime-spawn
   onto host world-entry snapshots and guest full-snapshot application;
   continuous presentation fields remain snapshot/stream-owned.
3. [x] Continue high-frequency stream unification: the 20 Hz enemy batch is now
   update-only and aggregate removal rides the kernel `EnemyRemovedEvent`
   committed batch; see
   `docs/selfchecks/phase-d-high-frequency-stream-unification-selfcheck.md`.
4. [x] Make enemy removal terminal in the kernel: `EnemyStateTable` carries
   `Removed` tombstones, post-removal upserts are rejected, and tombstones
   persist through checkpoint/wire/save and seed the guest restore path.
5. [x] Start absorbing `EnemyCombatDirector` policy: extracted
   `EnemyCombatPolicy` constants into Runtime tests and split
   `CrystalLungeTrace` into a top-level adapter type. The remaining work is
   moving the director's ordering/arbitration decisions into a kernel process
   or a smaller pure policy layer.
6. [x] Extract `EnemyTargetResolver` + `EnemyTarget`: the candidate-building,
   find-back and limb-index responsibilities now live outside the director.
   Behavior-preserving; the next step is moving the remaining ordering decisions
   into a kernel policy/process or a smaller pure layer.
7. [x] Extract `EnemyCombatOrderPolicy`: the remaining apply-path branches
   (spider bite, crystal lunge, item hit fallback) now go through a pure
   Runtime decision surface with L0 tests. The director remains the Unity-side
   executor/reporter; the next step is deciding whether these apply paths
   become kernel processes/events or stay host-order commands.
