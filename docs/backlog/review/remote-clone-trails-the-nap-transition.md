# The remote clone trails a nap transition

- Status: Review — read on batch `20261108-y`: while Steam1's nap started, its clone led the body by 0.9 units in x, sat 2.2 units lower in y, and carried leg speed `0.275` when the body read `0.747`; the same pair agreed exactly at rest and after a teleport (0.001 / 0.000). Reading: `docs/evidence/acceptance/unified-remote-display-projection-20261108-y.md`.
- Priority: Medium
- Category: Remote presentation / projection / transient pose
- Source: The unified remote display projection umbrella's machine reading (2026-11-08).

## Design

The surface this ticket judges is the remote body's presentation, whose design page is
`docs/design/surfaces/remote-body-presentation.md`: the clone renders from the streamed facts, and the page's rule is
that the presented body follows the member's own simulation within the stream's smoothing. The page is read before the
next fixture and written back with what the position trace shows.

## What was read

At rest and after a teleport, the host's clone of Steam1 matched Steam1's own body exactly (position, leg speed, pose
flag). During the nap start (`body-sleep` → `canTakeNap true, called true`) the two disagreed while the transition ran:
the clone at `(-1.065, 491.41)` leg speed `0.275` against the body at `(-1.959, 493.616)` leg speed `0.747`.

## Why it may happen (to verify, not to assume)

- The nap's start animation moves the body natively; the clone follows the streamed `WirePlayerStreamState.Position` and
  the `RemoteBodyDriver`'s smoothing, so a transient lead/lag is expected — the 2.2-unit vertical difference is larger
  than a stream period's smoothing should produce, so it is either a driver smoothing artefact or a fall-through.
- The clone's leg-speed field moving ahead of the body's suggests the driver's pose input is derived from the stream's
  velocity, not from the body's own transition.

## What to read next

- Repeat the nap start with a position trace at 10 Hz on both clients (a recipe reading the body's and the clone's
  position each call), and name whether the y difference is a smoothing overshoot or the clone losing the ground plane.
- If it is smoothing, the fix belongs in `RemoteBodyDriver`'s position smoothing (clamp the transient to a fraction of
  the stream period, or snap the vertical axis); if it is a fall-through, the clone's collider/pin state is the surface.
