# Phase C protocol-core self-check (2026-08-28)

This fact sheet records the first Phase C delivery cycle: the new four-envelope
protocol project, kernel/wire mapping, checkpoint save format, host/guest
kernel protocol service, production switch for spawn/pickup/drop/destroy item
facts, and the associated tests. Phase C is **in progress**; the remaining
cutover items are listed at the bottom.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Protocol project | `src/CasualtiesUnknownOnline.Protocol/` | Wire DTOs for `CommandEnvelope`, `CommittedBatchEnvelope`, `CheckpointEnvelope`, `StateStreamEnvelope`, shared `EnvelopeHeader`, numeric `WirePayloadType`, version constants, protobuf-net codec. No GameState/Runtime dependency. |
| Golden wire contract | `tests/.../Protocol/ProtocolCodecTests.cs` | Round-trips for all four envelopes + a fixed golden byte frame for a CommandEnvelope. |
| Kernel ↔ wire mapping | `Runtime/Session/Items/KernelWireMapper.cs` | Pure mapping between `GameCheckpoint`, `CommittedBatch`, `GameEvent`, `GameCommand` and Protocol wire DTOs. |
| Checkpoint chunks | `Runtime/Session/Items/WireCheckpointAssembler.cs` | Splits `GameCheckpoint` into fixed-size wire chunks and validates/assembles them back. |
| Kernel protocol service | `Runtime/Session/Items/KernelProtocolService.cs` + `IKernelProtocolControl` | Host executes wire commands, broadcasts committed batches, sends checkpoint+journal tail; guest applies checkpoints/batches, filters wrong epoch/version/gaps. |
| Transport entry | `Runtime/Session/Handlers/KernelEnvelopeHandler.cs` + `NetMsg.KernelEnvelope` | One existing frame id carries all four envelope kinds; direction is Bidirectional. |
| Idempotent application | `ItemKernelAuthority.Apply` + `BatchApplied` | Guest-side duplicate batches are idempotent by `OperationId`; applied batches raise `BatchApplied` for projection. |
| Guest projection | `ItemService.OnBatchApplied` | Applies confirmed kernel batches to the legacy world-item table and raises adopter item events (spawn/pickup/drop/destroy/data-update projection). |
| Save format | `Runtime/Session/Items/KernelSaveFileStore.cs`, `KernelSaveFile.cs`, `SaveHeader.cs` | Atomic protobuf checkpoint save with header; rejects unknown schema/corrupt files; no old save migration. |
| Production item send switch | `ItemMessageFlowService.SendItemSpawned/PickedUp/Dropped/Destroyed` | Guest sends `CommandEnvelope`; host no longer sends the corresponding old `ItemSpawn/ItemPickup/ItemDrop/ItemDestroy` frames. |
| Join hook | `WorldEntryFanout.Send` calls `IKernelProtocolControl.SendCheckpoint` | Checkpoint chunks + journal tail are part of world-entry backfill. |

## Evidence table

| Claim | Evidence |
|---|---|
| Protocol project defines all four envelopes and a common header | `ProtocolFrame` + envelope DTO files; `ProtocolCodecTests` exercises each. |
| Wire bytes are stable/locked | `GoldenCommandFrameBytes_AreStable`. |
| Kernel events/checkpoints map losslessly to wire | `KernelWireMapperTests` (batch round-trip, checkpoint split/assemble, wire command mapping). |
| Host accepts guest commands through the real transport | `KernelProtocolServiceTests` (guest `ItemService.SendItemSpawned`, host authority sees item, guest batch/projection converges). |
| Guest applies batches idempotently and projects world items | duplicate-delivery test, `Guest_ItemSpawnReport...`, `Guest_ItemPickupReport...`. |
| Wrong epoch/version/gap frames are rejected | `Guest_DropsBatchFromWrongEpoch`, `Guest_DropsUnsupportedEnvelopeVersion`, `Guest_DropsBatchWithRevisionGap`. |
| Checkpoint restore on guest works through the wire channel | `Host_SendsCheckpoint_AndGuestRestoresItemState`. |
| Save round-trips and rejects corrupt/missing files | `KernelSaveFileStoreTests`. |
| Existing behavior remains stable | Full suite 1636 tests green after the Phase C core landed. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: clean, 0 warnings/errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1636 tests green.
- New protocol/save tests run independently (`ProtocolCodecTests`, `KernelWireMapperTests`, `KernelSaveFileStoreTests`, `KernelProtocolServiceTests`).
- Direction contract updated (`NetMsg.KernelEnvelope` classified Bidirectional).

## Structure review

- `GameState` remains dependency-free; all wire DTOs live in the new Protocol
  project and all kernel/wire mapping lives in the Runtime seam.
- `KernelProtocolService` is the only broadcaster of kernel batches and the only
  receiver-side dispatcher for envelope frames.
- `ItemKernelAuthority` now has explicit `BatchCommitted`/`BatchApplied` events;
  the authority remains the single writer and the service/projections are
  observers, keeping the state-owner rule intact.
- No new class exceeds the 600-line gate; all new files are one top-level type.
- Remaining dual-write risk is explicitly tracked: `ItemCook` is still a legacy
  projection because the Phase C kernel does not yet expose a single
  cross-domain cook batch.

## Phase C completion items

- ItemUse/ItemSlot/ItemContainerContent now ride `CommandEnvelope`; container
  reconciliation is atomic via `SyncContainerItemsCommand`.
- Carried-fact and world-correction events are re-surfaced from
  `CommittedBatchEnvelope` projections; the old carried-sync/correction
  production send paths are gone.
- Periodic/generation item snapshots ride `StateStreamEnvelope`
  (`ItemSnapshotStream`/`WorldItemsSnapshotStream`).
- Cook is an atomic kernel batch (`CookItemCommand`), not a legacy `ItemCook`.
- Command rejections ride `CommandRejected` `CommandEnvelope`; tests observe the
  adapter event instead of the legacy `ItemReject` frame.
- Remaining old packet handlers are test-only replay injection; production
  paths use only `NetMsg.KernelEnvelope`.

## Subsequent-cycle additions (2026-08-28)

- Range recovery: guest buffers out-of-order batches, sends `RangeRequestCommand`,
  host sends journal batch ranges or falls back to a fresh checkpoint.
- Named random streams: `RandomStreamState` in `GameCheckpoint`,
  `WireRandomStream` in protocol/wire checkpoint/save, round-trip tests.
- Projection rebuild: checkpoint restore raises `CheckpointRestored` and guest
  world projection rebuilds from the authoritative checkpoint.
- StateStream: item move stream now rides `StateStreamEnvelope` host→guest and
  re-surfaces as `ItemMoveReceived` on the guest, replacing old `ItemMove`.
- Host wire projection: `ItemKernelAuthority.ExternalBatchCommitted` lets the
  host project commands that arrived over `CommandEnvelope` into the legacy
  world table without double-projecting local native writes.
- Projection failure: an event-handler exception during guest projection does
  not roll back the already-applied authoritative kernel batch.
- Simulation: latency + duplicate convergence, disconnect/reconnect checkpoint
  restore; all verified by the full suite (1644 green) and repo gates.
