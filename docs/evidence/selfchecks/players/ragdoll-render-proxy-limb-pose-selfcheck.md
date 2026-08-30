# Ragdoll render-proxy limb pose self-check

Owner cycle: backlog "Remote ragdoll state not visible". The reliable
`CharacterRagdoll` one-shot and the `Standing=false` state stream were already
present, but the remote clone still rendered upright because a frozen render
proxy has no physics to move its visible limbs while `Body.standing` is false.

> **Status: Accepted.** The original delivery was rejected for missing the
> failing-regression-test step; the red→green replay was completed and the
> user confirmed the remote ragdoll collapse is now visible. A separate
> issue tracks the remaining limb-pose mismatch.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native ragdoll visual | `Body.Ragdoll()` (`reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:1712-1729`) enables limb rigidbodies and sets `standing=false`; the local body's collapsed pose is physics-driven. |
| 2 | Render-proxy freeze | `RemoteBodyFactory` disables limb `Rigidbody2D`, `HingeJoint2D`, and colliders (`RemoteBodyFactory.cs:57-90`), so a clone cannot reproduce the physics-driven collapse. |
| 3 | Animator-driven pose | `Body.HandleVisuals` only copies the invisible `animLimb` transforms onto the visible limb transforms inside the `if (this.standing)` block (`Body.cs:3224-3252`); when `standing=false` the visible limbs are not re-posed. |
| 4 | Existing lying replay | `SessionStatePump` / `CharacterRagdollSync` play `ExperimentLayDown`/`ArmsLayDown` on the clone and set `body.standing=false`; the animator clip alone could not move the visible limbs on the frozen proxy. |
| 5 | State fallback | The 20 Hz `PlayerStateStream` `Standing` flag drives `LyingPose`; `RagdollPoseGate` prevents a stale `Standing=true` from standing the clone up too early. |

## 2. Root cause

On a frozen proxy, `Body.standing=false` disables the only animator-to-visible
limb copy inside `Body.HandleVisuals`. The clone therefore stayed in its
last standing limb configuration even though the animation clip had been
switched to the lying clip.

## 3. Fix

- `BodyPatches.BodyUpdatePatch` temporarily presents the proxy as standing to
  `HandleVisuals` during the visual pass, then restores the synced standing
  value. This lets the LayDown/lying clip drive the visible limb transforms on
  the frozen proxy while `SessionStatePump`/`LyingPose` still use the semantic
  standing state.
- `CharacterRagdollSync.TryApply` only marks `PrevLying=true` when the state
  stream already confirmed the collapse. When the one-shot beats the stream, it
  leaves `PrevLying=false` so the next `Standing=false` snapshot replays the lay
  clip and holds the pose even if the one-shot clip alone was not persistent.

## 4. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Pure rule for visual standing | `RenderProxyPose.EffectiveVisualStanding` | `RenderProxyPoseTests` (3 cases) |
| Visible limb pose while lying | temporary `standing=true` around `HandleVisuals` | `BodyPatches.cs`:83-110; static evidence `Body.cs:3224-3252` |
| One-shot vs stream replay | conditional `PrevLying` seed | `CharacterRagdollSync.cs`:170-175 |
| Stale standing suppression | unchanged | `RagdollPoseGateTests` |
| Full suite | no regressions | 1841 tests green |

## 5. Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1841 passed (3 new
  `RenderProxyPoseTests`).
- `dotnet format`, `check-architecture`, `check-event-replay`,
  `check-entity-event-dispatch`: all pass.
- Runtime acceptance: not performed (game was closed before this fix was
  available). User acceptance remains as the final step.
