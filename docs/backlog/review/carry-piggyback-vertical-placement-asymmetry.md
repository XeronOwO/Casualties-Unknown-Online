# Carry/piggyback vertical placement asymmetry (rider appears above on one side, below on the other)

- Status: Review
- Priority: Medium
- Category: Player interaction / carry-piggyback presentation
- Source: User report (2026-09-04) — when the host rides on a guest's back, the host's own view shows the host body higher than the guest, while the guest's view shows the host body lower than the guest. Suspected addition/subtraction/offset asymmetry. Record only; no code action taken yet.

## Landed

Root-caused the reference-point split in the carried-ride presentation family:

- The rider's 20 Hz stream was publishing the non-standing upper-torso anchor
  (`limbs[1]`) because a carried body is held with `standing = false`, while
  both participant-side placement paths use the Body root. This made
  stream-driven views and pin-driven views disagree vertically.
- `RunCoordinator.PublishBodyState` now uses a pure
  `CarriedBodyPose.ShouldPublishBodyRoot` rule to publish the body root for any
  carried rider, preserving the torso anchor for non-carried ragdolls.
- The shared `ApplyRidePose` write also sets the rider's crouch state to match
  the carrier on both sides, removing the remaining facing/crouch presentation
  divergence.

No carry authority, wire protocol, release semantics, or host rules changed.

Selfcheck: `docs/evidence/selfchecks/players/carried-rider-placement-smoothing-selfcheck.md`.

## Goal

Make both participants of a carry/piggyback relation see the rider at the same vertical relationship to the carrier (e.g. consistently on/above the carrier's back), regardless of which side is the local player.

## Current implementation

Both carry presentation paths use the same placement helper:

- `src/CasualtiesUnknownOnline.GameAdapter/Character/CarriedBodyPlacement.cs:20-25` — `BackOffset(carrierPosition, carrierIsRight, carrierCrouching)` returns `carrierPosition + new Vector3(0.35f * side, up, 0f)`.
- Rider's own client:
  - `src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs:69-101` — `UpdateCarriedBody` reads the remote carrier's entity `Position` and writes the local body to `BackOffset(...)`.
- Carrier's own client:
  - `src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs:171-192` — `ApplyLocalCarrierFollow` reads the local carrier's `transform.position` and writes the remote rider clone to `BackOffset(...)`.

Both paths add the same positive vertical offset, so a plain sign mismatch in `BackOffset` is not an obvious explanation. The asymmetry must be traced to one of:

- different transform/visual reference between the local carried body and the remote rider clone (body root vs clone root, sprite pivot, parent transform);
- one side reading a different carrier anchor (body root vs torso/limb position, or the stream's non-standing torso anchor from `RunCoordinator.PublishBodyState`);
- a facing/crouch state difference between the local carrier and the remote carrier entity, causing the same `BackOffset` to produce a different displayed height;
- `BodyFacing.Apply` / `RenderProxyPose` / pose state changing how the vertical offset is visually interpreted on one side.

## Investigation needed

- Capture the actual world positions of both bodies on the two participant clients while riding (logs/diagnostics).
- Compare the rider's local body transform, the remote carrier clone transform, and the remote rider clone transform to find where the vertical relationship diverges.
- Check whether both sides read the same carrier anchor (transform position vs synced entity position) and the same facing/crouch state.
- Determine whether the issue is in `BackOffset` itself, in the reference point, or in the pose/animation layer.

## Acceptance criteria (for the later implementation cycle)

- Host-on-guest and guest-on-host both show the same rider/carrier vertical relationship (rider on/above the carrier's back).
- The relationship remains correct when the carrier is standing and when crouching.
- Correctness is verified from both participants' perspectives.
- Existing carry/release/UI tests and repo gates remain green.

## Non-goals

- Not changing carry relation authority or release semantics.
- Not merging into the general carry movement-smoothing ticket; this is a distinct placement/reference asymmetry.
- The placement/reference fix landed in this cycle; see the selfcheck linked above.
