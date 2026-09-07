# Unified Remote Display Projection Self-Check

Delivery-cycle fact sheet for
`docs/backlog/review/unified-remote-display-projection-rework.md`
(deep-analysis + phase split + unified projection seam).

## 1. Problem and root cause

Remote presentation had grown through repeated per-field projection helpers:
`CloneFacePresentation`, `CloneBodyPosePresentation`,
`RemoteMedicalDisplayProjection`, plus inline derived-field code in
`RemoteMedicalCoordinator`. Each new acceptance failure was fixed by another
narrow helper, and the family kept drifting because there was no single entry
point that every remote view used.

## 2. Unified projection seam

The change introduces `RemoteCharacterDisplayProjection`
(`src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteCharacterDisplayProjection.cs`)
as the single adapter-side projector for remote player presentation.

| Surface | Old path | Unified path |
|---|---|---|
| Owner capture into 1 Hz/limb-event wire | `CloneFacePresentation.Capture` + `CloneBodyPosePresentation.Capture` + `CharacterComponentSync.Capture` | `RemoteCharacterDisplayProjection.Capture` |
| Remote render clone face/body pose | `CloneFacePresentation.Apply` + `CloneBodyPosePresentation.Apply` | `RemoteCharacterDisplayProjection.ApplyRenderClone` |
| Remote WoundView display body | `RemoteMedicalCoordinator.ApplyDisplayDerived` + `CharacterComponentSync.Apply` + inline progress code | `RemoteCharacterDisplayProjection.ApplyMedicalDisplay` + `AdvanceMedicalDisplay` |
| Remote clone item source values | duplicated in renderer branches | `RemoteItemPresentation.ApplySourceValues` |

The three old helper files were deleted; no dual parallel projection path remains
for face, body pose, medical display, or item source values.

## 3. Behavioral preservation

- `Capture` still writes `EatTime`, `HeadMouth`, `DisfiguredIndex`,
  `DisfiguredTimeFullSkin`, `EyeTimeHealed`, `LegSpeedMult`, and the
  painkiller/medicine component wire fields.
- `ApplyRenderClone` still writes the face latches, face-driving vitals,
  `FacialExpression` child fields, `RemoteBodyDriver.HeadMouth`, and
  `RemoteBodyDriver.LegSpeedMult`.
- `ApplyMedicalDisplay` still writes component state, heart rate, blood-pressure
  readout, breathing, respiratory-rate readout, antidepressant happiness, and
  mindwipe presence/cleanup.
- `AdvanceMedicalDisplay` still advances the ECG progress and the opiate
  reception ramp at the same native rates.
- Limb-state events now use the same unified capture as the 1 Hz snapshot, so
  they also carry `LegSpeedMult`; this is an intentional convergence and avoids
  waiting for the next snapshot to refresh the full remote presentation.

## 4. Item source-value regression

`CloneInventoryRenderer` previously created or refreshed top-level item proxies
without applying `CharacterItemMsg.Condition`, `Favourited`, or `Liquids`; a
remote backpack could therefore show a template-default 100% item while the
owner actually held a 75% item. All renderer branches (new top-level, matched
top-level, container update, container create) now route through
`RemoteItemPresentation.ApplySourceValues`, and the pure `SourceValues` record
is L0-tested.

## 5. Tests / gates

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2506 + 17 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `RemoteCharacterDisplayProjection` reflective tests | capture/apply/medical surface locked |
| `RemoteItemPresentationTests.SourceValues_IncludeConditionFavouriteLiquids` | source values locked |
| Independent adversarial subagent review | no blocking code-level defect; noted and fixed duplicated container source-value paths and test surface |

## 6. Remaining boundary

This is development-cycle verification only. Real dual-client visual acceptance
is still the user's final unified acceptance pass, especially for remote
WoundView/backpack rendering and carry/pose movement.

The same cycle also landed the global projection framework core
(`IProjectionDomain`, `ProjectionDomain`,
`ProjectionHealthCoordinator.Register(IProjectionDomain)`) and the architecture
doc `docs/architecture/projection-framework.md`; full domain migration remains
tracked in `docs/backlog/todo/global-projection-framework.md`.
