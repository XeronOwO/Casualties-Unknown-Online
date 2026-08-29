# Phase D Fluids shadow self-check (2026-08-29)

This fact sheet records the Phase D Fluids domain cycles: a kernel
persistent fluid-region table (coarse region totals/dominant type),
reset semantics, checkpoint/wire/save integration, and production wiring from
the host fluid grid into that kernel table. The high-frequency
simulation grid remains a stream/projection; the kernel owns the coarse
authoritative region checkpoint foundation.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Fluid region fact | `Domains/Fluids/FluidRegionState.cs` | Coarse chunk identity, total amount, dominant type, update timestamp. |
| Fluid table | `Domains/Fluids/FluidStateTable.cs` | Immutable snapshot with upsert. |
| Commands | `UpdateFluidRegionCommand`, `ResetFluidsCommand` | Host-only shadow commands. |
| Events | `FluidRegionUpdatedEvent`, `FluidsResetEvent` | Reduce into the table. |
| Domain module | `FluidDomainModule.cs` | Decide/reduce/invariant; unique region keys and non-negative totals. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `FluidStateTable?` is a kernel domain table and checkpoint field. |
| Wire DTO | `WireFluidRegionState` | Protocol remains GameState-free. |
| Mapper/save | `KernelDomainWireMapper`, `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | Fluid facts round-trip through wire checkpoints and disk saves. |
| Runtime authority surface | `ItemKernelAuthority.TryUpdateFluidRegion/TryResetFluids/QueryFluids` | Host commands and query entry points. |
| Host grid aggregator | `GameAdapter/World/FluidRegionKernelSync` | At a low cadence it aggregates `FluidManager.fluid` into `WorldGeneration.CHUNKSIZE` chunks (nonzero totals/dominant types). |
| Host projection | `FluidKernelProjection` + `IWorldControl.ReportFluidRegions` | `WorldService` forwards the summary; the projection change-gates upserts and clears stale positive chunks to zero. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel upserts/resets fluid regions | `FluidDomainKernelTests.UpdateReset_DriveFluidRegionTable`. |
| Negative total rejected | `FluidDomainKernelTests.NegativeTotal_IsRejectedByInvariant`. |
| Wire batch preserves a fluid event | `FluidDomainKernelTests.WireBatchRoundTrip_PreservesFluidRegionEvent`. |
| Checkpoint chunks preserve fluids | `FluidDomainKernelTests.CheckpointSplitAssemble_RoundTripsFluids`. |
| Save/load preserves fluids | `FluidDomainKernelTests.SaveLoad_RoundTripsFluids`. |
| Host region projection upserts and clears stale chunks | `FluidKernelProjectionTests.Sync_UpsertsAndClearsStalePositiveChunks`. |
| Host region projection is change-gated | `FluidKernelProjectionTests.Sync_DoesNotCommitUnchangedRegionFacts`. |
| `IWorldControl.ReportFluidRegions` reaches the kernel | `FluidKernelProjectionTests.WorldControlReportFluidRegions_CommitsThroughProjection`. |
| Random region updates preserve kernel invariants | `FluidDomainKernelTests.RandomUpdates_PreserveRegionInvariants`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1672 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed.

## Structure review

- `GameState` remains dependency-free.
- One top-level type per new file.
- The coarse authoritative region checkpoint is separated from the
  high-frequency simulation grid in the domain model.

## Next sub-steps

1. If a guest-side kernel read projection is needed beyond the existing RLE
   viewport stream, project checkpoint/fluid-region facts there and cover it
   with a simulation test.
2. Continue with high-frequency stream unification.
