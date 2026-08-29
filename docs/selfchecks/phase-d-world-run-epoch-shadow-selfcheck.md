# Phase D World/Run/Epoch shadow self-check (2026-08-29)

This fact sheet records the first Phase D delivery cycle: the World/Run/Epoch
kernel domain, checkpoint persistence, wire mapping, and the runtime save
round-trip. The production authority switch (world-start params through kernel
commands and removal of the legacy `WorldStartParams` wire) is deliberately not
part of this cycle; the kernel model is in place beside the existing path.

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
| Runtime authority surface | `ItemKernelAuthority.TryStartRun/TryAdvanceLayer/QueryRun` | The existing kernel authority can now commit and query run facts; adapter/host switch is the next cycle. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel accepts a run start and exposes the run baseline | `WorldDomainKernelTests.StartRun_CommitsRunBaselineAndCheckpoint`. |
| Duplicate run start is rejected | `WorldDomainKernelTests.DuplicateStartRun_IsRejected`. |
| Layer advance requires a started run and updates the baseline | `WorldDomainKernelTests.AdvanceLayer_RequiresRunAndUpdatesBaseline`. |
| A committed RunStarted batch replays on a guest kernel | `WorldDomainKernelTests.Apply_RunStartedBatch_ReplaysRunStateOnGuestKernel`. |
| Checkpoint chunks preserve run state | `WorldDomainKernelTests.CheckpointSplitAssemble_RoundTripsRunState`. |
| Save/load preserves run state | `WorldDomainKernelTests.SaveLoad_RoundTripsRunState`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1637 passed.
- `dotnet format`: applied.
- `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1`, `tools/check-gamestate-isolation.ps1`:
  passed.

## Structure review

- `GameState` remains dependency-free: World/Run types are plain records and
  enums, no Unity/Protocol/Runtime references.
- One top-level type per new file; all new classes are small.
- The world domain is isolated behind the same `IDomainModule` seam as Items;
  cross-domain integration will use CommittedBatch, not direct table access.

## Next sub-steps

1. Switch host `WorldParamsService` capture points to `StartRunCommand` /
   `AdvanceLayerCommand` and project `RunStarted`/`RunAdvanced` batches on the
   guest into `WorldStartParams`.
2. Replace the legacy `WorldStartParams` handshake send/read path with an
   authoritative batch/checkpoint delivery for mid-generation joiners.
3. Remove `WorldStartParamsMsg`, `WorldParamsHandler`, and `NetMsg.WorldStartParams`
   after the new path is covered by protocol/session tests.
