# Split SessionService at its seams

- Status: Done (2026-11-18) — all three seams landed. `SessionHeartbeat` owns the schedule (`SessionService` 599 → 579 lines, no `docs/architecture-debt.json` entry), the teardown rules left earlier, and seam 2 moved the per-update order to `SessionMaintenance`. The live row read: `role-clearing` PASS 18.2 s on `0.1.0+40e439bc5`.
- Priority: Medium
- Opened: 2026-11-14
- Debt: `docs/architecture-debt.json` → `CasualtiesUnknownOnline.Runtime.Session.SessionService`

## Why

`SessionService` is the session's single owner of role, presence, the
peer maintenance and the heartbeat. The 2026-11-14 fix moved the heartbeat out
of the world's lifetime (a guest ends its session after 15 s of silence, and a
world transition used to suspend the pump), which took the class to 616
aggregate lines — past the 600 the shape gate allows, so the growth is recorded
as debt until the seams below are split.

## The seams

1. **Heartbeat** — the ping schedule (`_nextPingMs`, `PingInterval`,
   `RequestPing`) becomes `SessionHeartbeat`, an `ICuoService` that pings on the
   fixed cadence whenever a role exists, independent of `SessionActive`. It
   needs the control's `RequestPing` and nothing else.
2. **Peer maintenance** — `_peerMaintenance`'s warmup, handshake retry and
   presence check are already a separate type; the service only delegates, so
   the delegation can move to the same heartbeat owner once (1) lands.
3. **Teardown** — `TeardownSession` and the role/lobby clearing rules
   (`EndsWithTheSession`, the transport-backed end) read as one unit and can
   leave with their own type.

Each step keeps the public `ISessionControl` surface identical: behaviour is the
same, the class just stops being the place every session concern lands.

## Acceptance

- Row 1: `SessionService` is back under 600 aggregate lines and its
  `docs/architecture-debt.json` entry is deleted in the same commit.
- Row 2: `role-clearing` and the pre-run join row still read as they do today
  (the heartbeat keeps pinging outside the world and a guest with no host sends
  nothing).
- Row 3: the unit suite stays green — the heartbeat's facts move with it
  (a session with a role pings on the cadence; the first ping waits one
  interval).

## Seam 2 (2026-11-18) — the per-update order

`SessionService.Update` ran four calls in a fixed order — warm up lobby peers
that have not handshaken, tick the ping schedule, retry a pending handshake
while no run is active, check presence — and now calls
`SessionMaintenance.Tick()` once. The new type takes the peer maintenance, the
heartbeat and the active predicate, and runs the same order; the comments that
explain why the order is the behaviour moved with it.

What stayed on the control: `RequestPing` (the frame the schedule sends),
`KickHandshake`, `ResetHandshakeRetry` and `ResetWarmup`. They are commands,
not per-update work, so moving them would have hidden the control's surface
without an owner to put it on.

Reading: build 0 errors, unit 6747/6747, gates 836/836, format clean; deployed
`0.1.0+40e439bc5`, `role-clearing` PASS (18.2 s, host and guest both leave the
session cleanly), and the suite's own setup joined the member before the run as
it does on every row.

## Not in this ticket

- Any change to the ping cadence or the silence bound.
- The Steam-lobby path (a lobby outlives the session for the returning host).
