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

## Near the limit (watch)

- `src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs` (~583) — the pump order is the seam
  contract, so any further per-frame step should go into a domain, not into `Update`.
- `src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs` (~582) — the run-lifecycle phase
  machine plus the body-state publish path. It grew by one line in the S2 in-game gap fix (the
  WorldJoin follow cancels a queued local character restore); a split is due before anything else
  lands in the phase machine or the publish path.
- `src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataSync.cs` (~579) — the character
  domain's session-scoped coordinator (the 1 Hz report, the local clone fact table, the restore
  queue). The S2 in-game gap fix moved its local restore queue to role-neutral
  `QueueLocalRestore`/`CancelLocalRestore`; the next responsibility that lands here takes the
  restore/apply half out, not another method in.
- `src/CasualtiesUnknownOnline.Runtime/Session/Commands/CommandConsoleService.cs` (~510) — command
  groups register as their own owners (`HostAdminCommands`, `WorldSaveCommands`); a new command
  family must not grow this class.

## Split since the last revision

- `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldSaveService.cs` — the demanded split
  happened (2026-09-11): the restore half (`TryContinue` + the salvage/summary assembly + the local
  character's apply contract) moved into `WorldRestoreApplier`, and the service now only ADOPTS the
  identity a restore produced as its write target. 513 lines, no longer near the gate; what remains
  is the trigger lifecycle (armed request, deferral window, report) over `WorldCutWriter`.
