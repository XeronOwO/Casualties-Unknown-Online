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
| Full snapshot | `EnemySyncService.ApplyEnemySnapshot` | Remains full-overwrite for world entry / reconnect, where a complete set is correct. |

## Evidence table

| Claim | Evidence |
|---|---|
| A state batch missing an id does not remove it | `EnemySyncServiceTests.StateBatch_MissingId_DoesNotRemoveExistingBuffer`. |
| An explicit removal drops the buffer and raises the event | `EnemySyncServiceTests.RemovalMessage_RemovesEnemyAndRaisesEvent`. |
| Host shrink still converges on a guest through the removal message | `EnemySyncServiceTests.EnemySnapshot_Overwrites_PreviousBuffer`. |
| Dropped state batches still converge through a reliable removal | `EnemySyncServiceTests.RemovedEnemy_ConvergesEvenWhenStateBatchDrops`. |
| The new message is direction-classified | `DirectionTests.EveryNetMsg_IsExplicitlyClassified`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1676 passed.
- `dotnet format`: applied.
- Architecture / event / entity / isolation / delivery gates passed.

## Structure review

- The game-facing removal handling stays in `EnemySyncCoordinator`; the
  protocol and buffer lifecycle stay in `EnemySyncService`.
- One top-level type per new file.
- The 20 Hz path remains a convergent stream; aggregate lifecycle is no longer
  inferred from stream absence.

## Remaining sub-steps

1. Align player/enemy continuous stream fields with `WireStateStream` /
   `StateStreamEnvelope`, or add the same explicit lifecycle policy to the
   player stream audit.
2. Add property/simulation tests for dropped/out-of-order update-only packets
   with explicit removals.
3. Consider promoting enemy health/death facts to dedicated domain events
   instead of carrying them only in the stream convergence path.
4. Continue with Phase D player supplements and world-entity snapshot cleanup.
