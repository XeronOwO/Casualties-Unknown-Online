# Members do not arrive together when the group descends a layer

- Status: Todo
- Priority: Medium
- Category: World generation / layer transition / spawn placement
- Source: the user's 2026-10-07 backlog request — when the group goes down a layer, the members' spawn points
  are not together.
- Related: `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs` (the generation's own last step for
  placement), `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs` (`PlaceBody`), the layer-transition tickets
  `done/layer-change-member-dropout.md` and `todo/layer-change-member-recovery.md` (the same boundary, other
  symptoms), `done/reenter-baseline-adoption.md` (a member entering after the host)

## What is observed

- A layer's generation ends by placing **the local body only**:
  `WorldGeneration` calls `PlayerCamera.main.body.PlaceBody()` after the world is built.
- `Body.PlaceBody` scans downward from the layer's top (`WorldGeneration.world.halfHeight` minus an offset per
  step) for the first position whose `origColSize` box does not overlap the `Ground` layer, and puts the body
  there — the scan's own position is on the vertical centre line, so the chosen point is an air pocket near
  the layer's ceiling. The tutorial override is the one exception and anchors to the `TUTORIALSPAWN` object.
- What the OTHER members' bodies do during a CUO layer change is not read yet: the native call places whoever
  ran the generation, and a remote body in that client's world is either corrected by CUO's position sync or
  left where it was.

## What is not known yet

- Which body runs `PlaceBody` on each side of a CUO layer change (the host's own, and on a guest: its own body
  or a remote copy), and what the members' bodies do while their own client is not the one generating.
- Whether the reported separation is vertical (different depth in the new layer), horizontal, or "one member
  is still in the previous layer's geometry", which are three different defects.
- Whether the layer entry should be a shared point at all, or whether the game intends per-body placement and
  the defect is only that the group cannot find each other. This is the first thing to settle: a shared spawn
  is a gameplay decision, and CUO's answer has to be one rule, not a per-client accident.

## Required work

1. Attribute first: drive one layer change on three clients and read each body's position and each client's
   `PlaceBody` execution (the accepting ticket's recipes can read a body's position; the layer machinery is
   already instrumented by the family's own diagnostics), then name which side placed which body.
2. Decide the rule with the user's intent in hand — the ask is that the group arrives together — and write it
   as one host-authoritative entry point rather than each client guessing; a member that joins mid-layer or
   re-enters after a dropout must land by the same rule.
3. Cover the failure paths: a layer whose entry point is blocked, a member who is dead or being carried when
   the group descends, and a member whose body is out of the world at that moment
   (`todo/layer-change-member-recovery.md`'s state).
4. Verify on three clients: after a descent every member reads the same entry point (within the tolerance the
   rule names), and no member is left in the previous layer's geometry.

## Non-goals

- Not a spawn-selection feature (choosing where to enter): the ask is that the group stays together.
- Not a fix for the member-dropout family: that is `todo/layer-change-member-recovery.md`; this ticket owns
  where an in-world member ends up.
