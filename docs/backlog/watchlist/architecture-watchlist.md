# Architecture watchlist

- Status: Watchlist
- Category: Architecture

Files at/near the 600-line hard gate (`SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits`)
must be split — as a real responsibility split, never by shrinking formatting — before the next
change lands in them.

## At the limit (no headroom)

- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/MedicalOperationSessionService.cs`
  and `ShrapnelOperationSessionService.cs` — both at exactly 600 lines after S3.3 added a
  one-line cut-policy probe to each (see `WorldTransientPolicy` / `MedicalSessionCutCounts`). The
  next change to either file needs the real split: the member/limb RESERVATION bookkeeping
  (`_reservedItems`, `_reservedTargetLimbs`) is shared by reference between the medical, shrapnel
  and other-medical services and belongs in its own object.
- `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldSaveService.cs` — 579 lines after
  S3.3 split the cut writer out. It is the trigger lifecycle (armed request, deferral window,
  report) over the writer + restore halves; the next responsibility that lands there should take
  the restore half (`TryContinue` + the salvage/summary assembly) into its own `WorldRestoreApplier`.

## Near the limit (watch)

- `src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs` (~583) — the pump order is the seam
  contract, so any further per-frame step should go into a domain, not into `Update`.
- `src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs` (~577) — the run-lifecycle phase
  machine plus the body-state publish path; a split is due if either grows.
- `src/CasualtiesUnknownOnline.Runtime/Session/Commands/CommandConsoleService.cs` (~510) — command
  groups register as their own owners (`HostAdminCommands`, `WorldSaveCommands`); a new command
  family must not grow this class.
