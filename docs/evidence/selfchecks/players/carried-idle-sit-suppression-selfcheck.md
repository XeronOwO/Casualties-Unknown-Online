# Carried-ride idle-sit suppression self-check

Owner cycle: backlog `carried-player-idle-sit-suppression`. Decision: close the
"carried character sits after long idle" by introducing one pure carried-ride
pose rule and applying it at every presentation path that can publish, replay,
or linger in the native sit pose. No carry authority, wire protocol, or release
semantics were changed.

## 1. Problem evidence

The native idle-sit condition is `Body.HandleVisuals` (`reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:3162-3166`):
when `idleTime > 12`, the game plays `ExperimentSit` / `ArmsSit`. CUO already
attempted to suppress it for render proxies by resetting `idleTime` above 11 s
in `BodyPatches`. That guard is insufficient for the carried-ride family:

- The rider's own state publisher could still leak `Sitting=true` if a stale
  or inflated `idleTime` existed while the body was carried.
- The carrier-side rider clone could still replay `Sitting=true` from the
  entity stream and remain in the sit clip because a stationary proxy stays in
  the animator state.
- A body that was sitting before the carry began could keep the already-playing
  `ExperimentSit` clip: resetting the timer alone does not make `HandleVisuals`
  leave an active sit clip when the proxy presents as standing to the animator.

## 2. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Native idle-sit condition | `Body.cs:3145-3166` — `idleTime` accumulates while standing/stationary; `idleTime > 12 && movingAllowed && !exercising` plays `ExperimentSit`/`ArmsSit` |
| 2 | Existing proxy/idle guard | `src/.../Patches/BodyPatches.cs` — proxy branch reset `idleTime` only after it exceeded 11 s |
| 3 | Carried local body marker | `src/.../Character/CarriedBodyDriver.cs` — `IsCarrying(body)` detects an active local carried body |
| 4 | Carrier-side rider clone | `src/.../Character/RemotePlayerRenderer.cs` — `ApplyLocalCarrierFollow` pins the remote clone to the local carrier |
| 5 | Remote entity state stream | `src/.../Character/SessionStatePump.cs` — applies `PlayerEntity.Sitting` to remote clones and replays sit clips |
| 6 | Local state publisher | `src/.../Run/RunCoordinator.cs` — `PublishBodyState` computes the `Sitting` stream flag from `body.idleTime` |

## 3. Whole-family audit

| Family member | Change |
|---|---|
| `CarriedBodyPose` (new pure Runtime rule) | One testable decision set: never publish sitting while carried, never replay sit on a carried rider clone, actively exit an already-playing sit clip, keep the carried idle timer at zero, and restore Grounded when the stream ends a sit state |
| `RemoteBodyDriver` | New `IsCarriedRider` presentation marker; set/cleared by the renderer every frame |
| `RemotePlayerRenderer` | Marks the local player's carried remote clone as a rider BEFORE applying stream state, so the suppression is same-frame for the carrier-side view |
| `SessionStatePump` | Uses the pure rule for sit replay; also restores `Grounded` on the sit→not-sitting stream transition (the missing exit for every stationary remote clone) |
| `RunCoordinator.PublishBodyState` | Uses the pure rule to never send `Sitting=true` for a carried local body |
| `BodyPatches` | Holds the carried-ride `idleTime` at zero every frame and actively leaves `ExperimentSit`/`ArmsSit` for a carried-ride body |
| Normal non-carried idle-sit | Unchanged: `ShouldPublishSitting(false, true, false)` is true, non-carried clones may replay sit, and sit-end still restores Grounded |

## 4. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Carried local body never publishes sit | `RunCoordinator` passes `CarriedBodyDriver.IsCarrying(body)` into `CarriedBodyPose.ShouldPublishSitting` | `CarriedBodyPoseTests.CarriedBody_NeverPublishesSittingEvenWhenIdleTimerExceeded` |
| Carrier-side rider clone never replays sit | `RemotePlayerRenderer` sets `RemoteBodyDriver.IsCarriedRider` before `SessionStatePump`; `SessionStatePump` calls `ShouldReplaySit` | `CarriedBodyPoseTests.CarriedRiderClone_DoesNotReplaySitFromStream` |
| Already-playing sit is actively exited on a ride | `BodyPatches` checks current animator clips after `HandleVisuals` and plays `Grounded` when `ShouldExitSit` is true | `CarriedBodyPoseTests.CarriedRider_WithSitClip_ActivelyExitsSit` |
| Carried idle timer cannot begin | `BodyPatches` calls `ShouldZeroIdleTimer(isCarriedRide)` every frame | `CarriedBodyPoseTests.CarriedRide_HoldsIdleTimerAtZero` |
| Generic sit-end restores standing presentation | `SessionStatePump` uses `ShouldRestoreGroundedOnSitEnd` on the stream transition | `CarriedBodyPoseTests.SittingEnd_RestoresGrounded` |
| Non-carried idle-sit preserved | All pure rules return the old behavior when not carried | `CarriedBodyPoseTests.NonCarriedIdleBody_MayPublishSitting`, `NonCarriedRemote_MayReplaySitFromStream`, `NonCarriedBody_WithSitClip_IsNotForcedOutOfSit` |

## 5. Verification design and results

- **L0 pure tests**: `CarriedBodyPoseTests` (14 tests) lock publish, replay,
  active-exit, idle-timer, and sit-end decisions.
- **Full regression**: `dotnet test CasualtiesUnknownOnline.slnx` — **2209
  passed / 0 failed**.
- **Build/format/gates**: `dotnet build`, `dotnet format`,
  `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1`, `check-delivery.ps1` pass.
- **Runtime verification**: development-period rule — L0 simulation/static
  evidence; no manual dual-client acceptance during feature development.

## 6. Structure review

- `CarriedBodyPose.cs` is a small pure static rule (no Unity).
- `RemoteBodyDriver.IsCarriedRider` is a presentation marker on the render
  clone, owned/cleared by `RemotePlayerRenderer`; it is not kernel state and
  does not affect carry authority.
- `BodyPatches` remains under the line gate; the added helper is a small
  clip-inspection method.
- No dead mechanism: the previous `idleTime > 11` proxy reset remains for all
  proxies; the carried-ride path adds a stricter zero-hold and active sit exit.
- No wire/protocol/event matrix change.
