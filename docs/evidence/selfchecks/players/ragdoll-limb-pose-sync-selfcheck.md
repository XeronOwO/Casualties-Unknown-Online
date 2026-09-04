# Ragdoll limb pose sync self-check

Owner cycle: backlog "Ragdoll limb pose not synced remotely". The previous
fix made the remote ragdoll collapse visible and started carrying per-limb
transform facts, but the user rejected the result: on the guest the body
remained upright with the lower half underground while the arms moved. The
rejection means the first limb-pose attempt was still using **local-space**
limb offsets; this revision switches the fact to **world-space** limb
transforms, matching the inspected KrokMP `ReadRagdollPacket` path that
writes `limb.rb.position` directly.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native ragdoll visual | `Body.Ragdoll()` enables limb rigidbodies and sets `standing=false` (`Body.cs:1713-1730`); the local body's collapsed pose is physics-driven. |
| 2 | Render-proxy freeze | `RemoteBodyFactory` disables limb `Rigidbody2D`, `HingeJoint2D`, and colliders; the clone cannot run the owner's ragdoll physics. |
| 3 | Existing limb replay | `SessionStatePump`/`CharacterRagdollSync` played `ExperimentLayDown`/`ArmsLayDown` and faked standing to `HandleVisuals`; the clip is a fixed approximation, not the owner's pose. |
| 4 | 20 Hz player stream | `WirePlayerStreamState`/`PlayerEntity` already carry convergent presentation state; this is the right channel for continuous limb poses (world position + z rotation). |
| 5 | HandleVisuals overwrite | `Body.HandleVisuals` copies `animLimb` → visible limbs only inside `if (this.standing)` (`Body.cs:3225-3254`); without a gating flag it would overwrite exact poses every frame. |
| 6 | World-space ragdoll evidence | KrokMP `WriteRagdollPacket`/`ReadRagdollPacket` send and apply `limb.rb.position` / `limb.rb.rotation` in world space (`ClientMain.cs:224-267`); its sync packet uses `limbs[1]` (upper torso) as the ragdoll position anchor (`ClientToServer_NetBodySyncPacket.cs:73-80`, `NetBody.cs:1121-1131`). |

## 2. Root cause

The frozen proxy cannot simulate the owner's ragdoll physics. The first
per-limb sync carried `limb.transform.localPosition` / `localEulerAngles.z`.
That is parent-relative, but the visible limb transforms are not reliably
centered on the Body transform; applying local offsets to a clone whose parent
layout/root placement differs leaves the skeleton upright and offset into the
ground even though every wire value was "correct". The working reference
implementation never used local offsets for ragdoll — it wrote the owner's
world-space rigidbody transforms directly.

## 3. Fix

- `PlayerLimbPose`/`WirePlayerLimbPose` now carry **world-space** position and
  world-space z rotation (property `WorldPosition`).
- `WirePlayerStreamState.LimbPoses` (ProtoMember 16) and
  `PlayerEntity.LimbPoses` carry the pose through the existing 20 Hz stream.
- `RunCoordinator`/`LimbPoseCapture` capture each visible limb's world-space
  transform while `!standing && !sleeping`.
- `RunCoordinator.PublishBodyState` uses the upper-torso world position as the
  stream anchor while non-standing, matching KrokMP.
- `SessionStatePump`/`RagdollPoseApplication` write the exact world transforms
  onto the remote clone (parents first so nested limbs are not shifted) and
  activate `RemoteBodyDriver.RagdollPoseActive`.
- `BodyPatches`/`RenderProxyPose` suppress the animator-driven visual standing
  when exact limb poses are active, so `HandleVisuals` cannot overwrite the
  owner's pose. Standing/sleeping clears the override and the normal
  animator/nap clips resume.

## 4. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Wire roundtrip | `WirePlayerLimbPose` list survives protobuf | `ProtocolCodecTests.StateStreamEnvelope_RoundTripsPlayerAndEnemyEntityStates` |
| Runtime fact roundtrip | `PlayerStreamWireMapper` maps pose list both ways | `EntityStateRoundtripTests.LimbPoses_RoundtripIntoEntityAndBackToWire` + null-clear case |
| World-space contract | `WorldPosition` present, `LocalPosition` absent on runtime + wire facts | `RagdollLimbPoseContractTests.PlayerLimbPose_UsesWorldSpacePosition`, `WirePlayerLimbPose_UsesWorldSpacePosition` |
| Exact-pose visual gate | `RenderProxyPose.EffectiveVisualStanding(..., hasExactLimbPose)` | `RenderProxyPoseTests.RemoteCloneWithExactLimbPose_DoesNotPresentStandingForVisuals` |
| Driver latch | `RemoteBodyDriver.RagdollPoseActive` exists | `RagdollPresentationStateTests` |
| Full suite | no regressions | 2177 tests green |

## 5. Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 2177 passed (contract,
  wire/pose-rule tests included).
- `dotnet format`; `check-architecture`; `check-event-replay`;
  `check-entity-event-dispatch`; `check-delivery` all pass.
- Runtime acceptance remains the user's final step; this selfcheck documents
  the static/runtime-evidence chain for the world-space correction.
