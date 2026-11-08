# The remote clone trails a nap transition

- Status: Done — fixed 2026-11-08 (`72a7ebb9`): the replica's position now writes a jump beyond two units outright and keeps the glide only for ordinary movement; the post-fix trace (`0.1.0+75133718`, batch `20261108-z`) reads the body and the clone at the same spot before and after a nap start (`(0, 485.615)` both), against the pre-fix reading's 2.4-unit divergence. Limit: the driver's call latency cannot sample inside the glide window, so the trace's before/after pair is the evidence, not a frame-level ramp.
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

## Why it happens (read, not assumed)

`src/CasualtiesUnknownOnline.GameAdapter/Character/SessionStatePump.cs:52-64` is the chain: every 20 Hz snapshot updates
`driver.LastStateMs` and the averaged interval (`AvgIntervalSec`, clamped 0.02-0.5 s, line 54); each frame the clone's
transform is set to `Vector2.Lerp(PrevPosition, Position, alpha)` with `alpha = clamp01(elapsed / window)` (lines 60-64).
A body that moves fast — a teleport, or the nap's own placement — therefore glides the clone across the window instead of
arriving with it, and the leg speed comes from the separate 1 Hz character snapshot (`RemoteBodyDriver.LegSpeedMult`), so
the pose field moves on yet another cadence.

That makes the reading a smoothing lag, not a fall-through: the clone has no collider and never leaves the ground plane;
it trails the last two streamed positions. The fix direction is a snap threshold on the glide (when the target is farther
than a body's own step can be, write the target instead of interpolating), with the window's normal case unchanged.

## What to read next

- Repeat the nap start with a position trace at 10 Hz on both clients (a recipe reading the body's and the clone's
  position each call), and name whether the y difference is a smoothing overshoot or the clone losing the ground plane.
- If it is smoothing, the fix belongs in `RemoteBodyDriver`'s position smoothing (clamp the transient to a fraction of
  the stream period, or snap the vertical axis); if it is a fall-through, the clone's collider/pin state is the surface.
