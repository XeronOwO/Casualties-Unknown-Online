# Unified remote display projection — machine reading (batch `20261108-y`)

Read on one live session (`launch-clients.ps1 -OutDir .acceptance/20261108-y`, lobby `109775243867663807`, all three in
world 16:41:39) with the host's `carry-read` (the clone `Body` entries) against Steam1's own `body-read`.

## Readings

| step | Steam1's own body | the host's clone of Steam1 | agreement |
|---|---|---|---|
| at rest | `(0, 493.616)`, leg speed `0.928`, standing, not sleeping | `(0, 493.615)`, leg speed `0.928`, `ragdollPoseActive false` | position 0.001 apart, leg speed equal, pose flag consistent |
| after a teleport to `-3, 493.6` (settled) | `(-2.085, 493.615)`, leg speed `0.932` | `(-2.085, 493.615)`, leg speed `0.932` | identical to three decimals |
| during a nap start (`body-sleep` → `canTakeNap true, called true`) | `(-1.959, 493.616)`, leg speed `0.747`, still `sleeping false` | `(-1.065, 491.41)`, leg speed `0.275` | x leads by 0.9, y differs by 2.2 while the transition runs |

The clone entries do not carry a SteamId; the pair was identified by the position/leg-speed match in the first two
readings (the second clone stayed at `(0, 493.615)` throughout, leg speed `0.975`–`0.976`).

## What it settles

- The projected clone stands exactly where the member stands and carries the same leg-speed pose: the static
  projection (position and leg-speed presentation) is read, not looked at.
- The nap transition is the one shape where the two clients disagreed while it ran: the clone led the body in x and sat
  lower in y, and its leg-speed field moved ahead of the body's. Filed as `docs/backlog/done/remote-clone-trails-the-nap-transition.md`
  rather than judged here — the umbrella's own row is the static projection.

## Limits

- The reading is one session, two players, one pose change; face/head and the medical display's derived fields are
  covered by the earlier phases' own fixtures, not re-read here.
- The clone entries carry no identity, so the pairing above relies on the position/leg-speed match.
