# Guest remote pose / head-orientation desync on host view

- Status: Review (code-complete)
- Priority: Medium
- Category: Player presentation / sync root cause
- Source: User report (2026-09-05), refined reproduction — dual-instance test; the original water-current scenario is included as one observed variant.

## Observed symptom

### Refined reproduction (dual-instance)

- The guest faces right and the mouse is on the guest's right side; the host's
  view of the guest has no posture problem.
- Move the mouse to the guest's left side while the guest still faces right;
  the host's view of the guest becomes abnormal.
- Observed host-view problems include, but are not limited to:
  - the host sees the guest facing left instead of the guest's actual facing;
  - the guest's head rotation angle is wrong on the host view;
  - the head angle has an abrupt jump when the mouse crosses the 180° ray of the
    Cartesian coordinate system.

### Earlier water-current variant

- A guest lying in water and being pushed by water also showed a pose difference
  between the host view and the guest's own view, with the host-side guest
  temporarily clipping underground.

## Assessment

This is likely a surface symptom of an orientation/pose synchronization
problem, not a water-specific or one-off visual issue. The refined mouse-side
reproduction points to the body/head orientation mapping itself: the remote
clone may be receiving or reconstructing a different facing/head angle than the
guest's local simulation, especially around the 180° angle boundary.

## Investigation direction

- Compare the guest-local body/head orientation with the host-side remote clone
  at each mouse side and as the mouse crosses the 180° ray.
- Identify which synced fields drive remote facing/head rotation:
  player stream yaw/pitch/head angle, body facing, mouse/aim direction, or a
  separate remote pose projection.
- Check angle representation and wrapping:
  - where yaw/head angle is converted across the 180° ray;
  - whether a Cartesian atan2/quadrant mapping is computed on one side and
    reused on the other, and where the singularity is handled;
  - whether remote pose uses world-space vs local-space angles.
- Determine whether the host seeing the guest facing left is caused by an
  actual reversed field, an unwrapped angle crossing, or a stale/mismatched
  pose fact.
- Check whether the water-current clipping/height offset is a separate issue or
  a downstream effect of the same orientation/pose divergence.
- Check the existing player state stream / remote clone pose family and whether
  this scenario should be covered by one root-cause fix.

## Root cause (this cycle)

The refined mouse-crossing reproduction is caused by a stale auto-flip input
leaking from the template into the render clone:

- `Body.HandleVisuals` flips a character when the look target crosses the 180°
  ray and `moveDir != 0 || attackCooldown > 0` (`reversing/.../Body.cs:3131`).
- A render clone does not run the original `Body.Update`, so a positive
  `attackCooldown` inherited at clone creation never decays
  (`reversing/.../Body.cs:3375`).
- `RemoteBodyFactory` reset `crouchAmount` / `inWater` / `currentClimbable`
  but not `attackCooldown` / `moveDir`.
- `BodyUpdatePatch.NeutralizePoseInputs` zeroed the other pose modifiers but
  not the facing auto-flip inputs.

## Landed

- `BodyUpdatePatch.NeutralizePoseInputs` now zeroes `body.attackCooldown`,
  `body.moveDir` and `body.eatTime` before every proxy/carry visual pass, so
  `HandleVisuals` can no longer auto-flip a remote clone away from the owner's
  synced `isRight` / `LookPos`, and `FacialExpression` cannot show a stale
  inherited eating/mouth sprite.
- `RemoteBodyFactory.CreateRemoteBody` also clears `attackCooldown`, `moveDir`
  and `eatTime` at clone creation, removing the one-frame window before the
  first per-frame neutralizer runs.
- No wire/protocol, authority, or local-player behavior change.
- Regression: `RemoteCloneFacingNeutralizationTests` red→green; see
  `docs/evidence/selfchecks/presentation/remote-clone-facing-auto-flip-selfcheck.md`.
- Deployed to the real game directory and verified the deployed
  `CasualtiesUnknownOnline.GameAdapter.dll` hash matches the build output.
- The earlier water-current/clipping variant is part of the broader systemic
  body-pose family (see `todo/host-severe-sleepiness-posture-desync.md`) and
  is not independently closed by this presentation-only facing fix.

## Non-goals

- Not adding a cosmetic host-side head/pose correction that hides the
  underlying orientation desync.
- Not treating the guest-local orientation as automatically wrong; the remote
  clone must reproduce the owner's actual facing/head state from the correct
  synced facts.
