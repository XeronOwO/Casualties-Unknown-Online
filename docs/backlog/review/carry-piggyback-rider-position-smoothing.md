# Carry/piggyback riding movement teleport and rider/carrier position mismatch

- Status: Review
- Priority: High
- Category: Player interaction / movement sync / carry-piggyback presentation
- Source: User report (2026-09-04); rejected in review (2026-09-05) — the first fix only covered half of the carry presentation family; rejected again (2026-09-05) on host movement with a riding guest; reworked again with a final LateUpdate carrier-side re-pin.

## Rejected again (2026-09-05 user re-test)

User re-tested the carry/piggyback movement while the guest rides on the host:

- The guest is riding on the host.
- The host moves.
- In the host's own view, the guest's body is not rigidly fixed to the carry
  position; the guest visibly teleports / jumps rather than staying attached.
- The carry presentation is rejected; the rider must stay firmly attached to
  the carrier during movement on the participant's view.

## Follow-up fix before re-review

The previous cycle already pinned remote rider clones to the local carrier
during the CUO Update pump and again in `Body.Update`'s postfix. The remaining
window was after all `Update` methods: if any game script/physics ordering moved
the local carrier after those pins, the frame that actually rendered could still
show the rider on the previous transform. The follow-up adds a final
`LateUpdate` pass that re-pins every remote rider clone to the local carrier's
final body transform immediately before rendering. It also keeps the existing
rider own-client placement unchanged, so the 20 Hz stream timing is not
disrupted.

## Goal

Make carried/piggyback rider movement visually stable on both participant sides. The rider should stay continuously attached at the expected back offset while the carrier moves; neither participant should see frame-level teleporting or a noticeable mismatch between the carrier and rider positions. Third-party views must use the same reference point.

## Landed

The carry/piggyback presentation is now a single shared path with the same reference point on every screen:

- **One shared ride-pose rule** — `CarriedBodyPlacement.ApplyRidePose` writes position, velocity, facing, crouch, standing/move-dir gates and look target to a carried body. The rider's own client and every remote rider clone use this exact method, so per-field drift between participant/third-party views is structurally impossible.
- **Rider follows the smoothed carrier render clone** — `PlayerInteractionApply.UpdateCarriedBody` prefers the remote carrier's `RemotePlayerRenderer` clone (already interpolated by `SessionStatePump`) over the raw 20 Hz entity buffer, with the entity buffer only as pre-clone fallback.
- **Every carried rider clone is pinned after interpolation** — `RemotePlayerRenderer.ApplyRemoteCarrierAttachAll` attaches each remote rider clone to its carrier's visual position (local body for a local carrier, the carrier clone for third-party views) after all `SessionStatePump` passes, so interpolation cannot visibly detach the pair.
- **Body-root stream anchor for carried riders** — `RunCoordinator.PublishBodyState` uses `CarriedBodyPose.ShouldPublishBodyRoot` so a carried body reports its body root instead of the non-standing torso anchor; non-carried ragdolls keep the existing torso convention.
- **Local carried rider presents as a visual proxy** — a conscious/alive local rider now goes through the same `RenderProxyPose.EffectiveVisualStanding(..., isCarryRenderProxy: true)` path as a remote clone. `HandleVisuals` continues driving the visible limbs instead of freezing in the pre-carry pose; dead/unconscious carries keep the non-standing presentation. The local rider also uses its own live `legSpeedMult` for the slouch/crouch animation input, the same value the 1 Hz snapshot sends to remote clones.
- **Post-native-`Body.Update` carrier re-pin** — `BodyUpdatePatch.Postfix` calls `IPatchBridge.OnLocalCarrierBodyUpdated()` after the local carrier's native body simulation. The CUO render pump may run before the game moves the local carrier in the same frame; this second pass ensures the carrier's own view never shows the rider one frame behind.
- **Post-placement stream refresh** — `PlayerInteractionApply.UpdateCarriedBody` calls `RunCoordinator.RefreshLocalBodyState()` after placing the rider, so the next 20 Hz stream snapshot carries the rider's final visual position/limb world-poses instead of the pre-follow state.
- **Final LateUpdate carrier re-pin** — `Plugin.LateUpdate` calls `GameAdapter.LateUpdateCarryPresentation`, which re-runs `RemotePlayerRenderer.RefreshLocalCarrierAttach` after every `Update`/`Body.Update` has finished. Even if a game script or Unity physics ordering moves the local carrier after the CUO pump, the frame that renders cannot show the rider on the previous transform.
- **Observability** — 1 Hz clone diagnostics tag carried-rider/carrier clones.

No carry authority, wire protocol, release semantics, or host rules changed.

## Current implementation

- Carry/piggyback remains host-authoritative; each client simulates only its own body.
- The carried local body is marked with `CarriedBodyDriver`, skips its normal simulation, and is placed each frame by `PlayerInteractionApply.UpdateCarriedBody`.
- Remote render clones are interpolated by `SessionStatePump` and then pinned by `RemotePlayerRenderer.ApplyRemoteCarrierAttachAll`.
- The shared placement rule lives in `CarriedBodyPlacement.ApplyRidePose`.
- The render-proxy visual-standing rule lives in `RenderProxyPose.EffectiveVisualStanding`.
- The post-update re-pin entry is `IPatchBridge.OnLocalCarrierBodyUpdated()` → `RemotePlayerRenderer.RefreshLocalCarrierAttach`.
- The final before-render re-pin entry is `Plugin.LateUpdate` → `GameAdapter.LateUpdateCarryPresentation` → `RemotePlayerRenderer.RefreshLocalCarrierAttach`.
- The stream refresh entry is `RunCoordinator.RefreshLocalBodyState()`.

## Acceptance criteria

- Host-on-guest and guest-on-host movement no longer shows frame teleport on the two participant views.
- The rider remains at the expected back offset relative to the carrier during movement, facing changes, and crouch changes.
- Any third-party view of the same carry relation keeps the rider visually attached within the same tolerance as normal remote-player smoothing.
- The conscious/alive local rider's limbs continue to render consistently with the remote rider clone.
- No change to carry authority or the carry relation invariants.
- Existing carry/release/UI tests, build, format, and repo gates pass.
- Regression coverage includes the carry visual-standing rule and the shared placement path.

## Evidence

- Selfcheck: `docs/evidence/selfchecks/players/carried-rider-placement-smoothing-selfcheck.md`
- Shared placement: `src/CasualtiesUnknownOnline.GameAdapter/Character/CarriedBodyPlacement.cs`
- Rider own-client placement: `src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs`
- Remote rider-clone attach: `src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs`
- Visual proxy rule: `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/RenderProxyPose.cs`
- Post-update re-pin: `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyUpdatePatch.cs` + `src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs`
- Final LateUpdate re-pin: `src/CasualtiesUnknownOnline.Plugin/Plugin.cs` + `src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs`
- Stream refresh: `src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs`

## Non-goals

- Not adding client prediction or remote-side simulation of the carrier.
- Not changing carry/release authority, host rules, or the carry relation lifecycle.
- Not creating a custom ride-pose UI or replacing the game's body presentation.
