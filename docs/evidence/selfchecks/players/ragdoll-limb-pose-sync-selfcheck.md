# Ragdoll limb pose sync self-check

Owner cycle: backlog "Ragdoll limb pose not synced remotely". The previous fix
made the remote ragdoll collapse visible by faking `standing` to
`Body.HandleVisuals`, but the visible limbs still used the animator's generic
`ExperimentLayDown` clip instead of the owner's actual physics-driven limb
pose.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native ragdoll visual | `Body.Ragdoll()` enables limb rigidbodies and sets `standing=false` (`Body.cs:1713-1730`); the local body's collapsed pose is physics-driven. |
| 2 | Render-proxy freeze | `RemoteBodyFactory` disables limb `Rigidbody2D`, `HingeJoint2D`, and colliders; the clone cannot run the owner's ragdoll physics. |
| 3 | Existing limb replay | `SessionStatePump`/`CharacterRagdollSync` played `ExperimentLayDown`/`ArmsLayDown` and faked standing to `HandleVisuals`; the clip is a fixed approximation, not the owner's pose. |
| 4 | 20 Hz player stream | `WirePlayerStreamState`/`PlayerEntity` already carry convergent presentation state; this is the right channel for continuous limb poses (position + z rotation). |
| 5 | HandleVisuals overwrite | `Body.HandleVisuals` copies `animLimb` → visible limbs only inside `if (this.standing)` (`Body.cs:3225-3254`); without a gating flag it would overwrite exact poses every frame. |

## 2. Root cause

The frozen proxy cannot simulate the owner's ragdoll physics, and the previous
replay used a generic animation clip. There was no wire path carrying the
owner's actual visible limb transforms, so remote limb positions/poses could
not match.

## 3. Fix

- New `PlayerLimbPose`/`WirePlayerLimbPose` wire fact: each limb's local
  position and z rotation.
- `WirePlayerStreamState.LimbPoses` (ProtoMember 16) and
  `PlayerEntity.LimbPoses` carry the pose through the existing 20 Hz stream.
- `RunCoordinator`/`LimbPoseCapture` capture the local body's visible limb
  transforms while `!standing && !sleeping`.
- `PlayerStreamWireMapper` maps the runtime fact to/from the wire.
- `SessionStatePump`/`RagdollPoseApplication` write the exact poses onto the
  remote clone and activate `RemoteBodyDriver.RagdollPoseActive`.
- `BodyPatches`/`RenderProxyPose` now suppress the animator-driven visual
  standing when exact limb poses are active, so `HandleVisuals` cannot
  overwrite the owner's pose. Standing/sleeping clears the override and the
  normal animator/nap clips resume.

## 4. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Wire roundtrip | `WirePlayerLimbPose` list survives protobuf | `ProtocolCodecTests.StateStreamEnvelope_RoundTripsPlayerAndEnemyEntityStates` |
| Runtime fact roundtrip | `PlayerStreamWireMapper` maps pose list both ways | `EntityStateRoundtripTests.LimbPoses_RoundtripIntoEntityAndBackToWire` + null-clear case |
| Exact-pose visual gate | `RenderProxyPose.EffectiveVisualStanding(..., hasExactLimbPose)` | `RenderProxyPoseTests.RemoteCloneWithExactLimbPose_DoesNotPresentStandingForVisuals` |
| Driver latch | `RemoteBodyDriver.RagdollPoseActive` exists | `RagdollPresentationStateTests` |
| Full suite | no regressions | 1855 tests green |

## 5. Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1855 passed (contract,
  wire/pose-rule tests included).
- `dotnet format`; `check-architecture`; `check-event-replay`;
  `check-entity-event-dispatch`; `check-delivery` all pass.
- Development-period rule: L0 + static evidence, no manual dual-client
  acceptance. User acceptance remains the final step.
