# Guest remote pose / head-orientation desync on host view

- Status: Todo
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

## Non-goals

- Not adding a cosmetic host-side head/pose correction that hides the
  underlying orientation desync.
- Not treating the guest-local orientation as automatically wrong; the remote
  clone must reproduce the owner's actual facing/head state from the correct
  synced facts.
