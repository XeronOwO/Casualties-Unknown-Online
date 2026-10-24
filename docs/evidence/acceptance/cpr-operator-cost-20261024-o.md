# Batch 20261024-o — whose body pays: the CPR press's cost had to move to the operator's own client

- Ticket: `docs/backlog/todo/remote-medical-cpr.md` (stage 4's per-press costs)
- First run: `0.1.0+733aedd7` (the host charged the operator) — **the charge never landed**
- Second run: `0.1.0+3c4ab912` (the operator's own client charges) — **it lands**
- Session each time: host + one member; host forced into arrest; the member opened the chart through `medical-open` and
  pressed the compression control through `cpr-press`; the member's OWN stamina read with `body-force` (which now reports
  `stamina` and `temperature`).

## What the two runs said

| run | presses | operator stamina before → after |
|---|---|---|
| host charges (`733aedd7`) | 1 start + 5 compressions | **100 → 100** |
| operator's client charges (`3c4ab912`) | 1 start + 5 compressions | **100 → 89.65** |

The nominal charge is 5 × 2.5 = 12.5; the reading shows 10.35 because the game's own stamina regeneration runs between the
presses. The point is the direction: before the fix nothing happened at all, after it the operator's own body pays.

## Why

A member's own vitals are authoritative on the member's client — the same rule that makes the injured side judge its own
injury. The host's snapshot of a member is a REPORT the host holds; writing into it changes what the host believes and
nothing on the member's screen. So the per-press cost belongs where the press happens: the control now reads
`PlayerCamera.main.body`, charges the policy's stamina and heat, and **refuses the press** (the game's own deny sound,
nothing sent) when the operator's stamina is at the floor — the patient is never given a press nobody made.

`CprApplier` keeps no operator charge at all, and the session case pins the boundary in that direction so a future change
cannot quietly move the charge back to the host.
