# Phase D World/Run/Epoch self-check (2026-08-29)

This fact sheet records the Phase D World/Run/Epoch delivery: the kernel domain,
checkpoint persistence, wire mapping, the runtime save round-trip, the
production authority switch (world-start params through kernel commands), and
the removal of the legacy `WorldStartParams` wire.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Run state | `src/CasualtiesUnknownOnline.GameState/Domains/World/RunState.cs` | Typed run identity, Random.state baseline, biome fields, traveled depth, loaded flag, typed run settings, layer index. |
| Run commands | `StartRunCommand.cs`, `AdvanceLayerCommand.cs` | Host-only commands; start may happen once per kernel epoch, advance requires an existing run. |
| Run events | `RunStartedEvent.cs`, `RunAdvancedEvent.cs` | Carrying the full run baseline; reducers set the single `RunState` on the kernel. |
| Domain module | `WorldDomainModule.cs` | Decide/reduce/invariant implementation: duplicate start rejected, advance before start rejected, non-empty RNG state and unique setting keys enforced. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `GameStateKernel` now owns `RunState?` as a domain table; checkpoints carry it. |
| Wire DTOs | `WireRunState.cs`, `WireRunSetting.cs`, `WireEventKind.RunStarted/RunAdvanced`, `WireCommandKind.RunStart/AdvanceLayer`, `WireCheckpoint.Run`, `WireCommand.RunState` | Protocol remains GameState-free. |
| Mapper/save | `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | Run state round-trips through wire checkpoints and disk saves. |
| Runtime authority surface | `ItemKernelAuthority.TryStartRun/TryAdvanceLayer/QueryRun` | The kernel authority commits and queries run facts; `WorldService.PublishWorldParams` now drives it. |
| Host/guest projection | `WorldService` + `WorldRunStateMapper` | Host commits the run baseline and stores the adapter projection; guest projects `RunStarted`/`RunAdvanced` batches and checkpoints into `WorldStartParams`. |
| Handshake delivery | `HandshakeHandler` + `IKernelProtocolControl.SendCheckpoint` | A mid-generation joiner receives the kernel checkpoint before `WorldJoin`; the run baseline is restored on the guest. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel accepts a run start and exposes the run baseline | `WorldDomainKernelTests.StartRun_CommitsRunBaselineAndCheckpoint`. |
| Duplicate run start is rejected | `WorldDomainKernelTests.DuplicateStartRun_IsRejected`. |
| Layer advance requires a started run and updates the baseline | `WorldDomainKernelTests.AdvanceLayer_RequiresRunAndUpdatesBaseline`. |
| A committed RunStarted batch replays on a guest kernel | `WorldDomainKernelTests.Apply_RunStartedBatch_ReplaysRunStateOnGuestKernel`. |
| Checkpoint chunks preserve run state | `WorldDomainKernelTests.CheckpointSplitAssemble_RoundTripsRunState`. |
| Save/load preserves run state | `WorldDomainKernelTests.SaveLoad_RoundTripsRunState`. |
| Host publishing world params commits the kernel run | `WorldRunStateProjectionTests.HostPublishWorldParams_CommitsKernelRunState`. |
| Guest applying a run batch projects `WorldStartParams` | `WorldRunStateProjectionTests.GuestAppliesRunBatch_ProjectsWorldParams`. |
| Mid-generation handshake delivers the run baseline via checkpoint | `WorldRunStateProjectionTests.GuestHandshake_ReceivesRunBaselineViaKernelCheckpoint`. |
| A fresh epoch kernel has no residue from an old epoch | `EpochIsolationTests.NewEpochKernel_HasNoResidueFromPreviousEpoch`. |
| Old-epoch commands/batches are rejected by a new-epoch kernel | `EpochIsolationTests.OldEpochCommand_IsRejectedByNewEpochKernel`, `OldEpochBatch_IsRejectedByNewEpochKernel`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1722 passed.
- `dotnet format`: applied.
- `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1`, `tools/check-gamestate-isolation.ps1`,
  `tools/check-delivery.ps1`: passed.

## Structure review

- `GameState` remains dependency-free: World/Run types are plain records and
  enums, no Unity/Protocol/Runtime references.
- One top-level type per new file; all new classes are small.
- The world domain is isolated behind the same `IDomainModule` seam as Items;
  cross-domain integration will use CommittedBatch, not direct table access.
- Legacy `WorldStartParamsMsg`, `WorldParamsHandler`, `SettingEntryMsg`, and
  `NetMsg.WorldStartParams` are removed; `WorldStartParams` remains only as the
  adapter-facing projection type.

## Next sub-steps

1. Start the next Phase D domain (Traps/Building Entities) with the same
   shadow -> authority -> projection -> delete template.
2. Keep RunEpoch filtering and cross-domain batch semantics in mind when trap
   results create damage/drop facts.
3. [x] Epoch-isolation property tests landed: a fresh epoch kernel has no
   old-epoch residue across all kernel domain tables, and old-epoch
   commands/batches are rejected.
