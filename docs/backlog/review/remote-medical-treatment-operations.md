# Remote medical treatment operations

- Status: Review
- Priority: Medium
- Category: Remote player UI / cross-player medical actions
- Source: User request (2026-09-05) — the remote medical panel is view-only and cannot perform medical treatment operations, e.g. using a syringe on a remote player.

## Problem

The current remote medical/health panel (`review/remote-player-medical-panel.md`)
reuses the native WoundView but is explicitly display-only. A player can see a
remote player's body/limb condition, but cannot perform treatment actions that
the native medical UI would allow on a local player — e.g. using a syringe
(administer medicine/injection) or other medical operations from the panel.

## Goal

Allow the native remote medical panel to perform real medical treatment actions
against a remote player, while preserving the safe cross-player action
semantics already established by the project:

- Reuse the game's native medical UI/operations where one exists.
- Route each treatment operation through the existing host-validated /
  cross-player interaction/arbitration path, not by mutating the remote clone
  or the display-only body copy directly.
- Define the exact supported operation set (at minimum: syringe/injection;
  identify other native WoundView medical actions and either support or
  explicitly record them).
- Cover all roles/directions: local acting on remote, remote acting on local,
  and third-party views.
- Keep the remote clone non-authoritative; the target player's own client
  validates/applies the committed operation.

## Supported operation set

This cycle adds the native WoundView **drag-a-local-medical-item-onto-a-remote-limb** treatment gesture:

- Bandages/dressings from `RemoteHealProfiles` → `PlayerHealRequest` (host-authoritative heal, selected limb).
- Injectable/IV medicines from `RemoteMedicineCatalog` → `PlayerItemUseRequest` (host-authoritative medicine apply, selected limb).
- Topical treatment from `RemoteTopicalCatalog` → `PlayerItemUseRequest` (host-authoritative topical apply, selected limb).
- Non-liquid limb tools from `RemoteLimbToolCatalog` (splint, tourniquet, icepack, clottingmush, medicalsuture, etc.) → `PlayerItemUseRequest` (host-authoritative limb-tool apply, selected limb).

### Explicitly not supported in this cycle

- Native WoundView **special-removal actions** (`WoundSpecialAction`: remove
  tourniquet, remove shrapnel, remove splint, fix dislocation) remain blocked
  while the remote medical focus is open. They mutate the local/display body in
  the native path and do not yet have a host-authoritative removal operation.
  They are listed here rather than silently deferred: a separate operation slice
  is required.
- Nap, radial use/wear, and other non-treatment special actions remain blocked.

## Implementation

- **Selected-limb wire support** — `PlayerHealRequestMsg` and
  `PlayerItemUseRequestMsg` gain a selected-limb representation. The protobuf
  field stores `limbIndex + 1` so limb 0 survives the protobuf default-zero
  omission; the C# `LimbIndex` property decodes 0/positive to auto/-1 or the
  actual limb. `ProtocolVersion.Current` bumped 8 → 9.
- **Runtime validates the selected target limb** — `RemoteHealApplication`
  gains `ResolveLimbIndex` (requested valid limb, otherwise most-injured auto
  pick). `PlayerHealService`, `PlayerItemUseService`, `RemoteMedicineApplication`,
  `RemoteTopicalApplication` and `RemoteLimbToolApplication` all honor it.
- **Native WoundView drag routing** — `RemoteMedicalPatches`
  `TryPerformSpecialUIAction` now routes only the WoundViewLimb drag release to
  the new bridge; all other special actions stay blocked.
  `PlayerCameraDragUsePatch` lets the remote-medical view reach the native UI
  before the world-overlap cross-player use path can preempt it.
- **Remote medical bridge handler** — new
  `IRemoteMedicalPatchBridge` / `RemoteMedicalOperationHandler`: checks the
  remote focus target, requires a local item with an authoritative instance id,
  rejects remote display proxies, and dispatches to `SendHealRequest` or
  `SendUseRequest` with the selected limb.
- **Read-only guarantee** — `PlayerCamera.WoundSpecialAction` is now also
  blocked in remote focus, closing the one remaining native special-action
  mutation path; the remote display body is never written by this treatment
  path.

## Acceptance criteria

- A player can open the remote medical panel and use the native medical
  treatment UI (e.g. syringe) to perform a treatment on the remote player.
- The treatment is applied through the existing cross-player interaction /
  host arbitration path, with a verified commit and replay on the target side.
- The display-only WoundView path is not bypassed by direct writes to remote
  clones.
- Unsupported native medical operations are recorded explicitly above (not
  silently left as an undocumented future).
- Full build, tests, architecture/event/entity gates pass; deployed artifact
  verified before `review/`.

## Evidence

- Selfcheck: `docs/evidence/selfchecks/players/remote-medical-treatment-operations-selfcheck.md`
- Handler: `src/CasualtiesUnknownOnline.GameAdapter/RemoteMedicalOperationHandler.cs`
- Bridge: `src/CasualtiesUnknownOnline.GameAdapter/IRemoteMedicalPatchBridge.cs`
- Patch: `src/CasualtiesUnknownOnline.GameAdapter/Patches/RemoteMedicalPatches.cs`
- Selected-limb runtime: `RemoteHealApplication.ResolveLimbIndex`, `PlayerHealService`,
  `PlayerItemUseService`, `RemoteMedicineApplication`, `RemoteTopicalApplication`,
  `RemoteLimbToolApplication`
- Protocol: `PlayerHealRequestMsg`, `PlayerItemUseRequestMsg`, `ProtocolVersion.Current = 9`

## Non-goals

- Not re-introducing a parallel CUO medical panel.
- Not mutating the remote render clone as the operation path.
- Not implementing remote special-removal actions in this cycle (recorded above).
- Not expanding beyond the native medical UI's own operations without user direction.
