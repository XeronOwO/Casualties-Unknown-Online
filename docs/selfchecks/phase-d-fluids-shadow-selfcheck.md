# Phase D Fluids shadow self-check (2026-08-29)

This fact sheet records the Phase D Fluids domain first cycle: a kernel
persistent fluid-region table (coarse region totals/dominant type),
reset semantics, and checkpoint/wire/save integration. The high-frequency
simulation grid remains a stream/projection; this model is the authoritative
region checkpoint foundation.

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
| Runtime authority surface | `ItemKernelAuthority.TryUpdateFluidRegion/TryResetFluids/QueryFluids` | Shadow entry points for later production wiring. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel upserts/resets fluid regions | `FluidDomainKernelTests.UpdateReset_DriveFluidRegionTable`. |
| Negative total rejected | `FluidDomainKernelTests.NegativeTotal_IsRejectedByInvariant`. |
| Wire batch preserves a fluid event | `FluidDomainKernelTests.WireBatchRoundTrip_PreservesFluidRegionEvent`. |
| Checkpoint chunks preserve fluids | `FluidDomainKernelTests.CheckpointSplitAssemble_RoundTripsFluids`. |
| Save/load preserves fluids | `FluidDomainKernelTests.SaveLoad_RoundTripsFluids`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1667 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed.

## Structure review

- `GameState` remains dependency-free.
- One top-level type per new file.
- The coarse authoritative region checkpoint is separated from the
  high-frequency simulation grid in the domain model.

## Next sub-steps

1. Periodically derive host fluid-grid region totals/types and commit
   `UpdateFluidRegionCommand` at a low cadence.
2. Project kernel fluid-region facts into guest local simulation rebuilds and
   checkpoint restore.
3. Continue with high-frequency stream unification.
