# A member joining a world that is already running sticks at the loading screen

- Status: Todo — a member that joins while the host's world is already running waits behind the start gate: its client
  keeps the loading screen up and re-reports its scene state ("still missing the start-gate release (WorldReady)") until
  the host leaves the world and starts the run again. The gate release is not delivered to a late joiner.
- Priority: High

## What happened

Measured 2026-11-07, batch `20261107-a`, artifact `0.1.0+48744847` (three clients: host, Steam1, Steam2).

- `00:23:57` the host started the run; the two sandbox clients joined that already-running world at about `00:24:00`.
- Steam1 log `00:24:05.819`: `Re-reported the scene state to the host (1/12) — still missing the start-gate release
  (WorldReady).`
- Host log `00:24:06.234`: `No confirmed members waiting — releasing the start gate immediately.` The host believed
  nobody was waiting, so it sent no release to the late joiner.
- Both sandbox screens stayed on the loading screen, while the driver's `state` read `inWorld=true,
  gateWaiting=false` — the world was live, the loading gate was not.
- Workaround used in the batch: host `leave-world` then `start-run`. The gates released at `00:25:29.845` /
  `00:25:30.012` (`the loading text is handed back to the game (generating plants and hidden traps...)` — translated).

## Why it is a defect

The release path exists and works for members that are waiting when the run starts (`No confirmed members waiting —
releasing the start gate immediately` is the host's own release). A member that enters later never receives it, so its
`StartGateCoordinator` holds the loading screen indefinitely even though the world is running. The session state reader
cannot see this: `state` reports the session's `inWorld`/`gateWaiting`, not the loading gate's.

## Design

Read from `docs/design/model/session-lifecycle.md` (the start gate holds the local player, its one forced release, the
three freezes while it holds, and the rejoin repair that re-sends the entry-group marker and the start-gate release every
5 s until both are in). The page's rule is that the release is part of the entry repair, not a one-shot broadcast at run
start — which is exactly where the late joiner falls through.

## Fix direction

- When a member's world join is confirmed after the run started, send that member the gate release (WorldReady)
  explicitly, or have the guest's `StartGateCoordinator` treat its own `inWorld` as the release.
- The host should not need a second `start-run` for a late joiner.
- Acceptance row: a late joiner's own client reads the loading gate released, without a host `leave-world`/`start-run`,
  and the log carries the release line.

## Chain, read to execution (2026-11-07)

- Entry event: a member that joined after the run started reports `SceneStateType.InWorld`
  (`SessionService.ReportSceneState`, `src/CasualtiesUnknownOnline.Runtime/Session/SessionService.cs:245-258`).
- Host edge: `SceneStateHandler.Handle` sees `wasInWorld != member.InWorld`
  (`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/SceneStateHandler.cs:47-52`) and calls
  `ctx.World.NotifyMemberInWorld(reporter)` (`:80`), which forwards to `WorldStartGate.NotifyMemberInWorld`
  (`src/CasualtiesUnknownOnline.Runtime/Session/World/WorldStartGate.cs:107-135`).
- The gate: `_armed is null` (the run started with nobody handshaken, so `Arm` took its "No confirmed members waiting"
  branch at `:88-97`) → the release is sent only when `LateJoinerPasses` (`:178`):
  `_descentPending is null && (_released || (_armed is null && _session.LocalInWorld))`. The empty-waiting branch sets
  `_armed = null` and broadcasts, but it never sets `_released = true`; and the host's own `LocalInWorld` flips only when
  its own scene report runs (`SessionService.cs:247`), which can be after the member's InWorld edge.
- Consequence: both the first arrival (`NotifyMemberInWorld`) and every re-asserted report (`AnswerRepeatInWorld`,
  `:146-157`) return without sending `NetMsg.WorldReady`, so the guest's `StartGateCoordinator` holds the loading screen
  until its 60 s valve returns it to the menu.
- Fix: set `_released = true` in `Arm`'s empty-waiting branch (`WorldStartGate.cs:95`) — a gate that never armed over a
  run is released by definition, so any member arriving later passes through the existing `SendWorldReadyTo` path.

