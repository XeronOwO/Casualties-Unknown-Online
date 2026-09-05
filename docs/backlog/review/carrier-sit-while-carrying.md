# Carrier can sit while carrying a player

- Status: Review
- Priority: Medium
- Category: Player interaction / carry-piggyback body pose
- Source: User report (2026-09-05) — while the guest carries the host on their back, the guest (carrier) is still able to sit on the ground, which is physically unreasonable.

## Landed

Closed the carrier-side half of the carry idle-sit family. The existing
carried-ride idle-sit suppression already covered the rider; this cycle extends
the same pure rule to the carrier:

- The local carrier body never starts the native idle-sit: `BodyUpdatePatch`
  holds `idleTime` at zero before the original `Body.Update`.
- A local carrier that was already sitting when the carry began is actively
  returned to `Grounded`.
- The local carrier state is read from the runtime carry mirror through a new
  narrow `IPatchBridge.IsLocalCarrier(body)` query — no duplicate local
  marker/authoritative state was added.
- Remote carrier clones are marked with `RemoteBodyDriver.IsCarrier` by
  `RemotePlayerRenderer`, so every peer (including third-party views) suppresses
  sit replay and cannot build an idle-sit timer.
- `RunCoordinator.PublishBodyState` never publishes `Sitting=true` for either
  half of a carry relation.
- Both host-carrier and guest-carrier directions use the same mirror-backed
  path; carry authority, wire protocol, and release semantics are unchanged.

Selfcheck: `docs/evidence/selfchecks/players/carrier-sit-suppression-selfcheck.md`.

## Original symptom

- A player is carrying another player piggyback.
- The carrier can enter/sit on the ground while still carrying the other player.
- This is visible and unreasonable for the carry relation.

## Assessment

The existing carried-ride idle-sit suppression covered the carried/rider side.
This report is the family gap on the carrier side: while the carrier has a
passenger, the carrier should not be allowed to sit down (or enter a grounded
sit pose that conflicts with carrying). The fix is whole-family, not only
suppressing rider sit.

## Acceptance criteria (covered by this cycle)

- Carrier cannot sit while carrying, in both host-carrier and guest-carrier
  directions.
- A carrier already sitting when the carry begins is returned to a valid
  standing/carry presentation.
- The suppression is visible on all participating and third-party views.
- Normal non-carried idle-sit behavior is unchanged.
- No carry authority, release semantics, or wire protocol change.

## Non-goals

- Not accepting "only the rider is prevented from sitting" as complete.
- Not changing carry authority, release semantics, or host rules.
