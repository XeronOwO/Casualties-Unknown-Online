# Suppress native idle-sit while a player is being carried/piggybacked

- Status: Review
- Priority: Medium
- Category: Player interaction / carry-piggyback body pose
- Source: User report (2026-09-04) — a carried character can sit down after a long period without input.

## Landed

Introduced a single pure carried-ride pose rule (`CarriedBodyPose`) and applied it across the
whole presentation family:

- The rider's own client never publishes `Sitting=true` while carried.
- The carrier-side rider clone is marked as a carried rider and never replays the native sit
  clips from the entity stream.
- A body that was already in `ExperimentSit`/`ArmsSit` when carry began is actively returned
  to the `Grounded`/ride presentation instead of lingering.
- The carried-ride `idleTime` is held at zero every frame, so the native sit condition cannot
  begin accumulating.
- The general remote-clone sit-end transition now restores `Grounded` too, fixing the same
  linger class for every stationary remote proxy, not only carried riders.

No carry authority, wire protocol, or release semantics changed.

Selfcheck: `docs/evidence/selfchecks/players/carried-idle-sit-suppression-selfcheck.md`.

## Remaining family gap

This landed slice covers the carried rider. It does not yet cover the carrier:
a player can still sit down while carrying someone. That gap is tracked as
`todo/carrier-sit-while-carrying.md`.
