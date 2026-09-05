# Carrier can sit while carrying a player

- Status: Todo
- Priority: Medium
- Category: Player interaction / carry-piggyback body pose
- Source: User report (2026-09-05) — while the guest carries the host on their back, the guest (carrier) is still able to sit on the ground, which is physically unreasonable.

## Observed symptom

- A player is carrying another player piggyback.
- The carrier can enter/sit on the ground while still carrying the other player.
- This is visible and unreasonable for the carry relation.

## Assessment

The existing carried-ride idle-sit suppression covers the carried/rider side.
This report is the family gap on the carrier side: while the carrier has a
passenger, the carrier should not be allowed to sit down (or enter a grounded
sit pose that conflicts with carrying). The fix must be whole-family, not only
suppress rider sit.

## Investigation direction

- Find where the native idle-sit condition/pose is decided on the carrier's
  body and whether carry state is checked there.
- Trace the carry relation fact on the carrier side (local/remote) and see if it
  can gate the sit state without changing carry authority.
- Extend the existing pure carry pose rule / idle-sit suppression to cover both
  roles:
  - carried rider cannot sit;
  - carrier cannot sit while carrying a passenger;
  - both host-carrier and guest-carrier directions.
- Decide behavior when a sit starts before carry begins: force the carrier back
  to a valid standing/ride pose while carrying, and prevent sit from re-entering
  until carry ends, consistent with the existing rider-side behavior.
- Add coverage for both roles and both host/guest directions.

## Non-goals

- Not accepting "only the rider is prevented from sitting" as complete.
- Not changing carry authority, release semantics, or host rules.
