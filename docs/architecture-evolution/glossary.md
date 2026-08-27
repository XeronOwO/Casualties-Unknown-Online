# Glossary

Stable vocabulary used across the architecture evolution docs.

## Kernel

- **GameStateKernel**: the typed deterministic authoritative state engine.
- **Kernel State**: the set of authoritative tables and current global revision.
- **ReadModel**: immutable/read-only view of kernel state passed to `Decide`.
- **MutableState**: the state modified by `Reduce` within a transaction working copy.

## Commands, events, effects

- **Command**: a typed request that something should happen; may be rejected.
- **CommandContext**: explicit inputs that keep the kernel deterministic (actor, role,
  session phase, time input, random input, observed native state, permissions).
- **Event**: a fact accepted by the kernel, stored in a committed batch.
- **Effect**: an outer side effect derived from a Batch (Unity update, sound, network send).
- **Decision**: the kernel's response to a Command: accepted batch or typed rejection.

## Transactions

- **CommittedBatch**: atomically accepted fact set for one logical operation.
- **OperationId**: idempotency key for a logical operation.
- **Aggregate Revision**: per-object revision detecting stale operations.
- **Global Revision**: global monotonic order for batches, checkpoints, and network deltas.
- **Preconditions**: expected revisions a batch depends on.
- **Journal**: bounded recent Batch storage for gap recovery.
- **CommittedOperationWindow**: compact operation-id window covering retransmit needs.

## Authority and prediction

- **Authority Policy**: declared rule for who may simulate and who commits a Command.
- **IntentCommand**: a player/game desired action (usually from a guest).
- **NativeObservation**: an adapter observation that native code already produced a result.
- **ConfirmedState**: authoritative state as known by a client.
- **PendingPrediction**: local optimistic prediction not yet confirmed.
- **LocalProjectedState**: confirmed state plus pending predictions.
- **Prediction Runtime**: code that manages predictions, rollback, and replay of pending operations.

## Projection

- **Projection**: a derived, rebuildable consumer of kernel state/batches.
- **UnityWorldProjection**: real Unity world objects.
- **LocalPlayerProjection**: local body and backpack.
- **RemoteCloneProjection**: remote display proxies and their caches.
- **NetworkProjection**: wire encoding of batches/checkpoint/stream.
- **PersistenceProjection**: save/checkpoint encoding.
- **DiagnosticsProjection**: traces, invariant results, semantic diffs.
- **Dirty projection**: projection that failed to apply; rebuilt from checkpoint/query.

## Run and epoch

- **Run**: one multiplayer run/world session.
- **RunEpoch**: run identity/epoch; all old-epoch commands, batches, and stream packets are rejected.
- **Checkpoint**: complete authoritative state snapshot at a revision.
- **SaveHeader**: metadata about a save file, not gameplay state.

## Native operations

- **NativeOperationCoordinator**: GameAdapter component that groups multi-hook native
  operations into one `NativeObservation`.
- **NativeOperation token**: context for one observed native operation, with begin/observe/complete/abort.

## Domains

- **ItemState**: authoritative item identity, location, capabilities, revision.
- **Location**: item location variant: World, Carried, Contained, or Terminal.
- **Terminal**: consumed/destroyed/replacedBy; cannot be resurrected.
- **Capability Registry**: per-item-type capability composition (Battery, Liquid, Durability, Gun, etc.),
  requiring all of Capture/Restore/Equivalent/Validate/Presentation.
- **RunState**: run identity, seed, layer, stage, world generation results, global rules.
