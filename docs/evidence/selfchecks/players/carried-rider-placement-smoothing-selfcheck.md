# Carried rider placement smoothing and vertical consistency self-check

> **Status: Review (2026-09-05 autonomous full-rework + deploy).** The first
> movement-smoothing attempt was rejected because the carrier/participant view
> still saw the rider misaligned/teleporting; a second `LateUpdate` re-pin still
> failed on the user's re-test. The final cycle replaced pin-only behavior on
> the local-carrier participant view with a structural carry-mount reparent,
> so the rider follows the carrier's final rendered transform instead of being
> an independent scene root re-pinned before render. This selfcheck is the
> evidence for the ticket now in
> `docs/backlog/review/carry-piggyback-rider-position-smoothing.md`.

Owner cycle: backlog `carry-piggyback-rider-position-smoothing` and
`carry-piggyback-vertical-placement-asymmetry`. Decision: close both reports by
making the carried-ride presentation one shared path and by keeping every view
on the same body-root stream anchor. No carry authority, wire protocol, release
semantics, or host rules changed.

## 1. Problem evidence

Two user-visible symptoms from the same presentation family:

1. **Movement teleport/snap.** The rider's own client moved its frozen local
   body from the carrier's raw 20 Hz entity buffer
   (`PlayerInteractionApply.UpdateCarriedBody`), while the same carrier is
   rendered on that client through `SessionStatePump` interpolation. The rider
   therefore stepped at the wire cadence while the visible carrier glided,
   producing frame snaps and perceived misalignment.
2. **Vertical placement mismatch.** The two participant placement paths use the
   same `BackOffset` and both write the body root, but the rider's 20 Hz player
   stream used the non-standing torso anchor convention: `RunCoordinator`
   published `limbs[1].transform.position` whenever `body.standing == false`,
   and the carried local body is deliberately set to `standing == false` while
   riding. Third-party clones (and any viewer that routes through the stream
   rather than a local pin) were therefore placed at a different reference
   point from the participant-side body-root placement.

## 2. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Carried local body driver | `src/CasualtiesUnknownOnline.GameAdapter/Character/CarriedBodyDriver.cs` — active-carrier marker, proxy freeze |
| 2 | Rider own-client placement | `src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs` — `UpdateCarriedBody` wrote transform from `PlayerEntity.Position` |
| 3 | Carrier-side/third-party rider clone pin | `src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs` — `ApplyRemoteCarrierAttachAll` |
| 4 | Remote clone smoothing | `src/CasualtiesUnknownOnline.GameAdapter/Character/SessionStatePump.cs` — `Lerp(PrevPosition, Position, alpha)` with adaptive interval |
| 5 | Local player state publisher | `src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs` — `PublishBodyState` |
| 6 | Non-standing stream anchor rule | `RunCoordinator.PublishBodyState` — published `limbs[1]` (upper torso) for `!standing` |
| 7 | Shared placement helper | `src/CasualtiesUnknownOnline.GameAdapter/Character/CarriedBodyPlacement.cs` — `BackOffset` + release restore |
| 8 | Pure carry presentation rule | `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/CarriedBodyPose.cs` — existing sit suppression family |

## 3. Whole-family audit

| Family member | Change |
|---|---|
| `CarriedBodyPose` (Runtime) | New pure rule `ShouldPublishBodyRoot(isCarried)`: carried bodies report the body root, non-carried ragdolls keep the torso anchor |
| `CarriedBodyPlacement` | New `ApplyRidePose`: one shared write that sets position, velocity, facing, crouching, standing/move-dir gates and look target |
| `PlayerInteractionApply.UpdateCarriedBody` | Prefers the remote carrier's render clone (already smoothed this frame) as the anchor; entity buffer stays as the pre-clone fallback |
| `RemotePlayerRenderer` first pass | Marks every remote clone that is a carried rider (not only the local carrier's rider), so sit/idle suppression is identical on third-party views too |
| `RemotePlayerRenderer.ApplyRemoteCarrierAttachAll` | After all clones are interpolated, pins every carried rider clone to its carrier's visual position (local body for local carrier, carrier clone for third-party views), so the pair is rigid on every screen |
| `RemotePlayerRenderer.LogClonePosition` | 1 Hz clone diagnostics now tag carried-rider/carrier clones for runtime placement tracing |
| `RunCoordinator.PublishBodyState` | While carried, publishes `body.transform.position` instead of the ragdoll torso anchor |
| `GameAdapter.Update` | Moved `UpdateCarriedBody` after `Renderer.Update` so the carrier clone has this frame's interpolation before the rider follows it |
| `RenderProxyPose` | New carry-proxy arm of `EffectiveVisualStanding`: a conscious/alive local carried rider presents as standing to `HandleVisuals`, matching the remote clone path |
| `BodyUpdatePatch` | Calls `OnLocalCarrierBodyUpdated` after the local carrier's native `Body.Update`; re-pins remote rider clones to the just-updated local body |
| `GameAdapterBridge` | Implements `OnLocalCarrierBodyUpdated` → `RemotePlayerRenderer.RefreshLocalCarrierAttach` |
| `Plugin.LateUpdate` → `GameAdapter.LateUpdateCarryPresentation` | Runs after every `Update`/`Body.Update` has completed and re-pins every remote rider clone to the carrier's final transform before render |
| `RunCoordinator.RefreshLocalBodyState` | Re-publishes the local entity buffer after the carried-follow placement, so the stream carries the rider's final visual position/limb poses |
| `BodyUpdatePatch` | Local carried proxy uses `Body.legSpeedMult` for the CrouchAmount slouch input, matching the value the 1 Hz snapshot sends to the remote clone |
| `RemotePlayerRenderer` local-carrier mount | Creates a neutral-scale `CUO_CarryMount` under the local carrier Body and re-parents the rider clone ROOT under it; the rider follows the carrier's final rendered transform (Rigidbody render interpolation included) rather than only a LateUpdate world-space pin |
| `RemotePlayerRenderer` third-party pin | Third-party remote carriers keep world-space pin only — both are frozen CUO clones without Unity Rigidbody render interpolation, and mounting would make the rider collateral for the carrier clone's destruction |

Not changed: carry relation authority, `PlayerCarryStateMsg`, release semantics,
host rules, or the ordinary 20 Hz player stream shape.

## 4. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Carried body stream anchor | carried bodies use body root; non-carried ragdolls keep torso anchor | `CarriedBodyPoseTests.CarriedRide_PublishesBodyRootAsStreamAnchor`, `NonCarriedRagdollBody_KeepsTorsoAnchorConvention` (red observed before the implementation) |
| One shared ride-pose path | both participant paths call the same method, cannot drift field-by-field | `CarriedBodyReleaseTests.RidePoseEntryPoint_IsTheSingleSharedReplacementPath` |
| Rider follows smoothed carrier | `UpdateCarriedBody` uses the remote carrier render clone after `Renderer.Update` | static code evidence: `GameAdapter` ordering + `RemotePlayerRenderer.TryGetRemoteBody` |
| Third-party viewer alignment | every remote rider clone is pinned to its carrier clone after interpolation; stream no longer publishes torso anchor for a carried rider | `RemotePlayerRenderer.ApplyRemoteCarrierAttachAll` + `RunCoordinator.PublishBodyState` + pure rule |
| Local carried rider visual consistency | conscious/alive local carried body presents as a visual proxy so `HandleVisuals` keeps driving its limbs | `RenderProxyPoseTests.CarriedFrozenRiderNotStanding_PresentsStandingForVisuals` + `BodyUpdatePatch` carry-proxy call |
| Carrier-side no one-frame lag | rider clones are re-pinned after the native local carrier `Body.Update` finishes | `BodyUpdatePatch.Postfix` → `OnLocalCarrierBodyUpdated` → `RefreshLocalCarrierAttach` |
| Carrier-side final before-render pin | every remote rider clone is re-pinned once more after all Update methods, so the rendered frame matches the carrier's final transform | `Plugin.LateUpdate` → `GameAdapter.LateUpdateCarryPresentation` → `RefreshLocalCarrierAttach` |
| Local-carrier structural attach | the rider clone root is a real child of the local carrier's neutral mount, so it cannot be separated by render interpolation/order after the pin | `RemotePlayerRenderer.GetOrCreateCarryMount` + `AttachCarriedRiderRoot` + `CarriedRiderMountTests` |
| Mount scale neutralization | the mount local scale inverts the carrier world scale, preserving the rider's own facing/scale under both left/right carrier facing and non-unit scales | `CarriedBodyPlacement.CarryMountScale` + `CarriedRiderMountTests.CarryMountScale_NeutralizesCarrierWorldScale` (5 cases) |
| Mount detach/restore | a released rider root returns to the scene root so the ordinary state stream can drive it again | `RemotePlayerRenderer.DetachCarriedRiderRoot` |
| Stream fallback freshness | local entity buffer is refreshed after the carried-follow placement | `RunCoordinator.RefreshLocalBodyState` called from `PlayerInteractionApply.UpdateCarriedBody` |
| Observability | 1 Hz clone logs identify carried-rider/carrier clones and mounted state | `RemotePlayerRenderer.LogClonePosition` carry tag + `mounted-to-local-carrier` tag |

## 5. Verification design and results

- **Before-red**: the two `ShouldPublishBodyRoot` tests and the
  `ApplyRidePose` entry-point test were run against the pre-fix source; all
  three failed at runtime (`CarriedBodyPose.ShouldPublishBodyRoot not found`,
  `CarriedBodyPlacement.ApplyRidePose not found`). The new
  `CarriedRiderMountTests` are reflection-hosted and fail against the pre-mount
  assembly because `GetOrCreateCarryMount` / `AttachCarriedRiderRoot` /
  `DetachCarriedRiderRoot` / `CarryMountScale` did not exist before the rework.
- **Focused regression**: `RenderProxyPoseTests` + `CarriedBodyPoseTests` +
  `CarriedBodyReleaseTests` + `CarriedRiderMountTests` (contract surface + 5
  `CarryMountScale` behavior cases: ±1, non-unit, zero fallback) — all pass after the mount rework.
- **L0**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **2323 passed / 0 failed** after the mount rework.
- **Build**: `dotnet build CasualtiesUnknownOnline.slnx --no-restore` — 0 warnings / 0 errors.
- **Gates**: `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1` all pass.
- **Format**: `dotnet format` run; `--verify-no-changes` only reports the
  known gitignored generated `obj/.../MyPluginInfo.cs`, as documented.
- **Deploy**: `tools/deploy.ps1 -GameDir "<game-dir>"` succeeded on the real game directory; SHA256 of deployed `CasualtiesUnknownOnline.dll`, `CasualtiesUnknownOnline.GameAdapter.dll`, and `CasualtiesUnknownOnline.Runtime.dll` matches the build output.

## 6. Structure review

- New code is a single pure rule in the existing pure `CarriedBodyPose` and a
  single shared helper in `CarriedBodyPlacement`; the mount surface stays inside
  `RemotePlayerRenderer` as narrow private helpers, no new stateful service, no
  new wire field, no added authority.
- The two duplicated participant placement bodies were replaced by one call
  path, eliminating the family of "forgot a field on one side" bugs.
- The local-carrier mount avoids global mutable state: the mount is an empty
  child owned by the carrier body, and the detach restore keeps the clone
  recoverable by the normal stream.
- No touched file approached the line/state gates; no top-level type count
  changed.
