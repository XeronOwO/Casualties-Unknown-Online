# A dead or unconscious carried body's own simulation stops

- Status: Todo
- Priority: Medium
- Category: Carry/piggyback presentation / own-client body simulation
- Source: Scope boundary recorded while implementing the rider-simulation rule (2026-09-21): `review/carried-rider-own-body-stops-simulating.md` covers a conscious/alive rider, and a dead or unconscious carried body deliberately keeps the frozen pinned-ragdoll presentation.
- Related: `review/carried-rider-own-body-stops-simulating.md` (the rule this ticket extends), `todo/carry-piggyback-rider-position-smoothing.md`

## Reported behaviour

Not user-reported: found while implementing the rider-simulation rule. On the rider's own client a
dead or unconscious carried body still skips its per-frame simulation
(`CarriedBodySimulation.SkipsNativeSimulation` is true for it), so `Body.Update` never runs and
`HandleBody` never advances its vitals — an unconscious teammate who is bleeding does not bleed out
while carried, temperature, radiation, the periodic checks and `Limb.Update` (wound/infection) are
frozen, and every peer keeps seeing the vitals of the last simulated frame.

## Why the rider rule does not cover it

A conscious rider stands natively, so the body can keep its own pose while the carry placement owns
the transform. A dead or unconscious carried body is a ragdoll: its pose is physics-driven, its limbs
are simulated rigidbodies, and it is pinned rigidly to a moving carrier. Running that pose pass while
pinned means deciding who owns the limb physics (the carrier's transform or the ragdoll simulation)
and what the native ragdoll timer does — `HandlePhysics` calls `Stand(false)` after 60 s of not
standing (`Body.cs:3080-3083`), which a pinned body must not be allowed to do.

## Goal

A dead or unconscious carried body's own client keeps the vitals half of its per-frame simulation
(`HandleBody`, temperature, radiation, the periodic checks, `Limb.Update`) while its pose stays the
pinned ragdoll the carry presentation needs — or this ticket records, with runtime evidence, why the
pose and the vitals cannot be separated and the current presentation is right.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | A bleeding unconscious player is carried to safety | Their vitals keep advancing on their own client while carried |
| 2 | A carried corpse | No change: limp ragdoll presentation, no standing up, no limb state regrown |
| 3 | The carrier moves, turns and crouches with a limp body | The body stays attached and does not come apart |
| 4 | Release | Exactly the pre-carry state, immediately |

## Non-goals

- Not changing who owns the carry relation or its authority.
- Not a carry-specific ragdoll network.
