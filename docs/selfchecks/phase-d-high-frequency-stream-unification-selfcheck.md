# Phase D high-frequency stream unification self-check (2026-08-29)

This fact sheet records the first high-frequency stream unification slice: the
enemy 20 Hz stream is no longer a full-set overwrite that implicitly creates or
destroys aggregates. Stream packets now only update existing enemy buffers;
enemy removal is an explicit, reliable kernel `EnemyRemovedEvent` committed
batch, and the Game Adapter destroys the corresponding frozen guest copy.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Stream update semantics | `EnemySyncService.ApplyEnemyState` | `Merge` updates only the ids present in the batch. A missing id is no longer removed. |
| Aggregate lifecycle | `EnemyRemovedEvent` → `KernelEnvelope` committed batch | Reliable kernel fact: one enemy id left the authoritative host set. |
| Guest projection | `EnemySyncService.OnKernelBatchApplied` | Guest applies `EnemyRemovedEvent` from the committed batch, removes the buffer, and raises `EnemyRemovedReceived`. |
| Host diff | `EnemySyncService.PublishEnemyStates` + `EnemyKernelProjection.Sync` | Computes ids that left the previous set and commits `RemoveEnemyCommand`; the accepted batch is broadcast to guests. |
| Guest destruction | `EnemySyncCoordinator.OnEnemyRemoved` | Removes mapping state and destroys the local frozen `BuildingEntity` copy. |
| Removal tombstone | `EnemySyncService._removedEnemies` + `EnemyStateTable.Removed` | Guest-side session-scoped tombstone set seeded from checkpoint tombstones; late/out-of-order state batches and full snapshots cannot resurrect an explicitly removed id. |
| Full snapshot | `EnemySyncService.ApplyEnemySnapshot` | Remains full-overwrite for world entry / reconnect, where a complete set is correct. |
| Kernel terminal revision guard | `EnemySyncService` + `WireStateStream.BaseGlobalRevision` | Guest tracks the kernel global revision of each enemy terminal health event; a stale 20 Hz stream older than that event preserves terminal health/stunned while still applying continuous position/velocity. |
| Player stream lifecycle audit | `EntitySyncService` + `PlayerJoin`/`PlayerLeave` | Player stream is update-only for existing buffers; explicit `PlayerJoin`/`PlayerLeave` owns aggregate lifecycle, so a state batch missing a player is not a removal. |
| Unified state-stream wire | `WireStateStream.PlayerStates` / `WireStateStream.EnemyStates` + `WirePlayerStreamState` / `WireEnemyStreamState` | Both player and enemy 20 Hz continuous/presentation streams now ride `StateStreamEnvelope` over `NetMsg.KernelEnvelope`; the old direct `NetMsg.PlayerState`, `NetMsg.PlayerStateReport`, and `NetMsg.EnemyState` high-frequency paths and their handlers/DTOs are removed. |
| Player report direction | `KernelProtocolService.HandleHostFrame` + `PlayerStreamExchange` | Guest player reports travel as `PlayerStateStream` state-stream envelopes and are seq-gated per synced member on the host. |
| Player stream owner split | `PlayerStreamExchange` | The player stream send/receive/gate logic lives in a separate class so `EntitySyncService` stays under the architecture line gate; it reads only `IEntitySyncControl` and `IKernelProtocolControl`. |

## Evidence table

| Claim | Evidence |
|---|---|
| A state batch missing an id does not remove it | `EnemySyncServiceTests.StateBatch_MissingId_DoesNotRemoveExistingBuffer`. |
| A kernel removal batch drops the buffer and raises the event | `EnemySyncServiceTests.KernelRemovalBatch_RemovesEnemyAndRaisesEvent`. |
| Host shrink still converges on a guest through a kernel removal batch | `EnemySyncServiceTests.EnemySnapshot_Overwrites_PreviousBuffer`. |
| Dropped state batches still converge through a reliable kernel removal | `EnemySyncServiceTests.RemovedEnemy_ConvergesEvenWhenStateBatchDrops`. |
| A late update-only state batch cannot resurrect a removed enemy | `EnemySyncServiceTests.RemovedEnemy_NotResurrectedByLateStateBatch`. |
| A full snapshot cannot resurrect a removed enemy | `EnemySyncServiceTests.RemovedEnemy_NotResurrectedByFullSnapshot`. |
| A checkpoint tombstone seeds the guest removed set | `EnemySyncServiceTests.CheckpointTombstone_SeedsGuestRemovedSet`. |
| A replay upsert event after removal is a no-op | `EnemyDomainKernelTests.ReplayUpsertAfterRemoval_DoesNotResurrect`. |
| The legacy removal wire row is removed from direction classification | `DirectionTests` no longer lists `NetMsg.EnemyRemoved`. |
| A player state batch missing a player does not remove the buffer | `StateStreamTests.PlayerStateBatch_MissingPlayer_DoesNotRemoveExistingBuffer`. |
| PlayerLeave removes the guest's remote player buffer | `StateStreamTests.PlayerLeave_RemovesRemoteBuffer`. |
| A stale enemy stream cannot overwrite a newer kernel health event | `EnemySyncServiceTests.StaleStream_CannotOverwriteNewerKernelHealth`. |
| The unified wire preserves player/enemy stream entities | `ProtocolCodecTests.StateStreamEnvelope_RoundTripsPlayerAndEnemyEntityStates`. |
| Player seq gate works over `KernelEnvelope` | `StateStreamTests.StaleAndDuplicateSequences_Dropped_NewerPass`. |
| Player stream remains update-only over `KernelEnvelope` | `StateStreamTests.PlayerStateBatch_MissingPlayer_DoesNotRemoveExistingBuffer`. |
| Guest player report converges to the host synced member | `StateStreamTests.GuestReport_ReachesHostAndUpdatesSyncedMember`. |
| Enemy seq gate works over `KernelEnvelope` | `EnemySyncServiceTests.StaleAndDuplicateSequences_Dropped_NewerPass`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1712 passed.
- `dotnet format`: applied.
- Architecture / event / entity / isolation / delivery gates passed.

## Structure review

- The game-facing removal handling stays in `EnemySyncCoordinator`; the
  protocol and buffer lifecycle stay in `EnemySyncService`.
- One top-level type per new file.
- The 20 Hz path remains a convergent stream; aggregate lifecycle is no longer
  inferred from stream absence; a removed aggregate is also never resurrected
  by a late out-of-order stream packet or snapshot.

## Remaining sub-steps

1. [x] Align player/enemy continuous stream fields with `WireStateStream` /
   `StateStreamEnvelope`: both 20 Hz paths now ride the unified state-stream
   envelope over `KernelEnvelope`, and the old direct high-frequency
   `NetMsg.PlayerState` / `PlayerStateReport` / `EnemyState` handlers and DTOs
   are removed. Terminal revision protection is preserved on the enemy stream.
2. [x] Add property/simulation tests for dropped/out-of-order update-only packets
   with explicit removals, and guard the guest buffer against resurrection.
3. [x] Enemy health/death facts are committed as dedicated kernel events, and
   stale streams cannot roll them back (the revision guard covers this).
4. [x] Enemy aggregate removal now rides `EnemyRemovedEvent` through
   `KernelEnvelope`; `NetMsg.EnemyRemoved`, `EnemyRemovedMsg`, and
   `EnemyRemovedHandler` are removed.
5. Continue with Phase D player supplements and world-entity snapshot cleanup.
6. [x] Kernel removal is now terminal and replay-safe: `EnemyStateTable.Removed`
   tombstones reject post-destruction upserts and make stale replay upserts
   no-ops; tombstones persist through checkpoint/wire/save and seed the guest
   runtime removed set.
