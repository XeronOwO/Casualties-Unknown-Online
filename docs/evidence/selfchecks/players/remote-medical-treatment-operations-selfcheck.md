# Remote medical treatment operations — self-check

Closes the backlog item `remote-medical-treatment-operations.md`.
The native WoundView remote focus was display-only; this cycle keeps the native
UI but routes the limb treatment drag through the existing host-authoritative
cross-player heal/use slice with a selected-limb fact.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native WoundView already accepts a dragged item on a body-limb image | `PlayerCamera.TryPerformSpecialUIAction` → `ApplyWoundItem` (`reversing/.../PlayerCamera.cs:1654-1659`) |
| 2 | Existing cross-player heal slice | `PlayerHealService` / `PlayerHealRequestMsg` |
| 3 | Existing cross-player use slice already covers syringe/medicine, topical and limb tools | `PlayerItemUseService` / `RemoteMedicineCatalog`, `RemoteTopicalCatalog`, `RemoteLimbToolCatalog` |
| 4 | Remote medical focus is display-only | `RemoteMedicalView` / `RemoteMedicalCoordinator` |
| 5 | New gesture routes to host-authoritative request without touching the display body | `RemoteMedicalOperationHandler` + `RemoteMedicalPatches` |

## 2. Change table

| Mechanism | Change | Evidence |
|---|---|---|
| Wire can express limb 0 | request messages store `limbIndex + 1` (`LimbSelection`); C# `LimbIndex` maps back | `PlayerHealRequestMsg`, `PlayerItemUseRequestMsg` |
| Host honors native selected limb | `RemoteHealApplication.ResolveLimbIndex` used by heal, medicine, topical and limb-tool application | `RemoteHealApplication.cs`, `PlayerHealService.cs`, `PlayerItemUseService.cs`, `RemoteMedicineApplication.cs`, `RemoteTopicalApplication.cs`, `RemoteLimbToolApplication.cs` |
| Native WoundView limb drag routes instead of mutating display body | `RemoteMedicalPatches.TryPerformSpecialUIAction` calls the bridge only for `WoundViewLimb`; all other special actions remain blocked | `RemoteMedicalPatches.cs` |
| Remote medical view reaches its own routing before world-overlap drag-use | `PlayerCameraDragUsePatch` returns to native when `RemoteMedicalView.IsOpen && !RemoteBackpackView.IsOpen` | `PlayerCameraDragUsePatch.cs` |
| Special removal and direct ApplyWoundItem cannot mutate display body | `PlayerCamera.WoundSpecialAction` and `PlayerCamera.ApplyWoundItem` both prefixed off in remote focus | `RemoteMedicalPatches.cs` |
| Medical view and backpack view cannot overlap | opening one closes the other; `RemoteBackpackView.Close` clears native radial-open state | `RemoteBackpackCoordinator.cs`, `RemoteMedicalCoordinator.cs`, `RemoteBackpackView.cs` |
| Medical limb eligibility excludes non-limb surfaces | `LocalUseItemEligibility.IsMedicalLimbUseItem` accepts only heal/medicine/topical/limb-tool and rejects zero-condition/wear/food/drink | `LocalUseItemEligibility.cs` |
| Protocol mixed-version rejection | `ProtocolVersion.Current` 8 → 9 | `ProtocolVersion.cs` |

## 3. Regression coverage

- `PlayerInteractionServiceTests.HealRequest_RoundTripsSelectedLimbIndex` — protobuf roundtrip of limb 0 (previously lost by default-zero omission).
- `PlayerInteractionServiceTests.HealRequest_RoundTripsAutoLimbSelection` — protobuf roundtrip of auto (-1).
- `PlayerInteractionServiceTests.UseRequest_RoundTripsSelectedLimbIndex` — protobuf roundtrip of positive limb selection.
- `PlayerInteractionServiceTests.Guest_HealsSelectedLimbOnHost_AppliesRequestedLimbNotAutoPick` — host applies to requested limb 0, not auto-picked limb 1.
- `PlayerInteractionServiceTests.Guest_UsesTopicalOnSelectedLimb_AppliesRequestedLimbNotAutoPick` — host applies topical to requested limb 0, not auto-picked limb 1.
- `RemoteLimbToolApplicationTests.ApplyMusharm_RequestedLimbWinsOverMostInjuredAutoPick` — pure limb-tool selection path.
- `RemoteMedicineApplicationTests.ApplyAntiserum_RequestedLimbWinsOverMostInjuredAutoPick` and invalid/dismembered fallback — medicine selection boundaries.
- `RemoteTopicalApplicationTests.ApplyReliefcream_RequestedLimbWinsOverMostInjuredAutoPick` — topical selection path.
- `RemoteHealApplicationTests.ResolveLimbIndex_UsesRequestedValidLimb`, fallback, and unordered-semantic-index tests — selection/fallback boundaries.
- `RemoteMedicalContractTests.MedicalBridge_ExposesLimbUseRouting` — adapter bridge surface contract.

## 4. Verification

- `dotnet build CasualtiesUnknownOnline.slnx --no-restore` — 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx --no-build --no-restore` — 2304 passed / 0 failed.
- `dotnet format CasualtiesUnknownOnline.slnx --no-restore` — run.
- `tools/check-architecture.ps1`, `check-event-replay.ps1`, `check-entity-event-dispatch.ps1` — pass.
- Protocol version bumped; mixed-version sessions are rejected by the existing handshake.

## 5. Structure review

- New types are single-purpose: `IRemoteMedicalPatchBridge` (one gesture seam) and
  `RemoteMedicalOperationHandler` (pure routing, no scene mutation).
- Existing runtime services remain single-responsibility; `ResolveLimbIndex` is a
  pure fallback rule in the existing heal application.
- No new mutable domain state was introduced; the remote focus remains the static
  presentation state already owned by `RemoteMedicalView`.
