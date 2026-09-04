# Suppress native idle-sit while a player is being carried/piggybacked

- Status: Todo
- Priority: Medium
- Category: Player interaction / carry-piggyback body pose
- Source: User report (2026-09-04) — a carried character can sit down after a long period without input. This is the game's native idle mechanic, but it is clearly unreasonable while being carried. Record only; no code action taken yet.

## Goal

Prevent a carried/piggybacked character from entering the native idle-sit pose. While carried, only the ride/back-carry presentation should be visible, on both the rider's own client and the carrier's view.

## Current implementation

The code already attempts to suppress the normal idle-timer sitting on render proxies and carried local bodies:

- `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs:60-67` — inside the proxy/carried branch of `Body.Update`, `idleTime` is reset to `0` when it exceeds `11f`, with the comment: "The 12s idle timer makes the original sit down (Body.cs:3162-3166); a render proxy must stay in its standing pose — reset the timer."
- `src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs:278-291` — when the local body becomes carried, `CarriedBodyDriver` is added.
- `src/CasualtiesUnknownOnline.GameAdapter/Character/CarriedBodyDriver.cs` — marks a local body as carried; `BodyPatches` routes it through the render-proxy path.
- `src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs:171-192` — the carrier-side rider clone is pinned to the local body and set to `standing = false`.

Despite this, the user still sees the carried character sit after idle.

## Likely causes to investigate

1. The native sit path may be triggered outside the reset point, or before `Body.Update` runs the proxy branch (e.g. another `HandleVisuals`/animator state machine path reads `idleTime`).
2. The carried local body's pose is `standing = false` (ride/driver pose), and the proxy/render path may not explicitly force the "carried" pose, allowing the animator to fall into an idle/sitting clip from a stale pose or from another state variable.
3. The reset only fires when `idleTime > 11f`. If the sit state is entered through a different timer/flag or if `idleTime` is not increasing but the sit animation is still selected from a previous state, the current guard is insufficient.
4. The carrier-side rider clone may not be covered by the same suppression on every frame, or the local carried body and the remote rider clone use different pose rules.
5. There may be no current test or runtime evidence locking "carried body never sits".

## Required design direction

- Introduce a single explicit "currently carried" pose rule shared by:
  - the carried (rider) local body,
  - the carrier-side remote rider clone,
  - any other peers' view of the rider.
- While carried, force/keep the ride presentation and suppress all non-ride idle poses (sit, and any other native idle-state transitions that should not apply).
- Keep the suppression scoped to carried bodies only; normal solo/local idle-sit behavior must remain unchanged after release.
- Add regression/static tests or runtime-log evidence proving the carried body stays in ride pose after long idle, and that normal sitting resumes after release.

## Acceptance criteria (for the later implementation cycle)

- A carried character does not sit after long idle on either participant's view.
- The ride/back-carry pose remains consistent while moving and while stationary.
- After release, the local body resumes normal idle/sit behavior.
- Existing carry/release/UI tests and repo gates remain green.
- No carry authority or wire semantics are changed unless required.

## Non-goals

- Not changing the game's solo idle-sit behavior.
- Not altering carry/release authority or host rules.
- Not implementing in this cycle — this ticket is a backlog record only.
