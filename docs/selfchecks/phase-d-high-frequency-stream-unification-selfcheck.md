# Phase D high-frequency stream unification self-check (2026-08-29)

This fact sheet records the first high-frequency stream unification slice: the
enemy 20 Hz stream is no longer a full-set overwrite that implicitly creates or
destroys aggregates. Stream packets now only update existing enemy buffers;
enemy removal is an explicit, reliable host→guest lifecycle message, and the
Game Adapter destroys the corresponding frozen guest copy.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Stream update semantics | `EnemySyncService.ApplyEnemyState` | `Merge` updates only the ids present in the batch. A missing id is no longer removed. |
| Aggregate lifecycle | `EnemyRemovedMsg` (NetMsg 123) | Host→guest reliable fact: one enemy id left the authoritative host set. |
| Handler | `EnemyRemovedHandler` | Guest-side packet handler routes into `IEnemySyncControl.ApplyEnemyRemoved`. |
| Host diff | `EnemySyncService.PublishEnemyStates` | Computes ids that left the previous buffer and sends reliable removals to in-world guests. |
| Guest destruction | `EnemySyncCoordinator.OnEnemyRemoved` | Removes mapping state and destroys the local frozen `BuildingEntity` copy. |
| Removal tombstone | `EnemySyncService._removedEnemies` | Guest-side session-scoped tombstone set; late/out-of-order state batches and full snapshots cannot resurrect an explicitly removed id. |
| Full snapshot | `EnemySyncService.ApplyEnemySnapshot` | Remains full-overwrite for world entry / reconnect, where a complete set is correct. |
| Kernel terminal revision guard | `EnemySyncService` + `EnemyStateBatchMsg.BaseGlobalRevision` | Guest tracks the kernel global revision of each enemy terminal health event; a stale 20 Hz stream older than that event preserves terminal health/stunned while still applying continuous position/velocity. |
| Player stream lifecycle audit | `PlayerStateHandler` / `PlayerLeaveHandler` / `ProcessPlayerJoin` | Player stream is update-only for existing buffers; explicit `PlayerJoin`/`PlayerLeave` owns aggregate lifecycle, so a state batch missing a player is not a removal. |

## Evidence table

| Claim | Evidence |
|---|---|
| A state batch missing an id does not remove it | `EnemySyncServiceTests.StateBatch_MissingId_DoesNotRemoveExistingBuffer`. |
| An explicit removal drops the buffer and raises the event | `EnemySyncServiceTests.RemovalMessage_RemovesEnemyAndRaisesEvent`. |
| Host shrink still converges on a guest through the removal message | `EnemySyncServiceTests.EnemySnapshot_Overwrites_PreviousBuffer`. |
| Dropped state batches still converge through a reliable removal | `EnemySyncServiceTests.RemovedEnemy_ConvergesEvenWhenStateBatchDrops`. |
| A late update-only state batch cannot resurrect a removed enemy | `EnemySyncServiceTests.RemovedEnemy_NotResurrectedByLateStateBatch`. |
| A full snapshot cannot resurrect a removed enemy | `EnemySyncServiceTests.RemovedEnemy_NotResurrectedByFullSnapshot`. |
| The new message is direction-classified | `DirectionTests.EveryNetMsg_IsExplicitlyClassified`. |
| A player state batch missing a player does not remove the buffer | `StateStreamTests.PlayerStateBatch_MissingPlayer_DoesNotRemoveExistingBuffer`. |
| PlayerLeave removes the guest's remote player buffer | `StateStreamTests.PlayerLeave_RemovesRemoteBuffer`. |
| A stale enemy stream cannot overwrite a newer kernel health event | `EnemySyncServiceTests.StaleStream_CannotOverwriteNewerKernelHealth`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1697 passed.
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

1. Align player/enemy continuous stream fields with `WireStateStream` /
   `StateStreamEnvelope` (the player stream lifecycle audit portion is now
   covered by the update-only/PlayerLeave tests above).
2. [x] Add property/simulation tests for dropped/out-of-order update-only packets
   with explicit removals, and guard the guest buffer against resurrection.
3. [x] Enemy health/death facts are committed as dedicated kernel events, and
   stale streams cannot roll them back (the revision guard covers this).
4. Continue with Phase D player supplements and world-entity snapshot cleanup.
