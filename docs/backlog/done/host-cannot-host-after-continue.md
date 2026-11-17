# A host cannot start a session after entering a world

- Status: Done (2026-11-17) — the natural order works: Continue first, then host. The host opened a session over the
  world it had just continued, and a member joined the same run (focused case `host-after-continue`, 29.2 s on
  `0.1.0+2a7b6e7e1`, and a 39-row batch read 39 of 39 green at 301.6 s with the case named as by-name).
- Priority: High
- Category: Session / save
- Source: found while reading the layer-end continue on the sandboxed 1.0 session (2026-11-10).

## What happens

The natural player order is: open the game, press Continue, then invite a friend. On the release build that order
produced a session that cannot be started (IP direct refused while a world runs) or a world whose characters were
claimed in the wrong key space (Steam instead of IpDirect), so every stored character was dropped as unclaimed.

The order that worked until this change was: create the session at the menu, then continue inside it — the archive's
characters are claimed and a member joins the restored layer (read 2026-11-10). The natural order is what this ticket
fixes; a host may now continue and then open the room.

## Options

1. **Auto-session on Continue**: when the host continues from the Worlds page (or the main menu's Continue is routed
   through CUO), start the configured transport first and continue inside it. One player action, the archive's key
   space always matches.
2. **Allow hosting a running world**: lift the IP-direct guard and make the session adopt the loaded world. The user
   chose this option on 2026-11-11: the experience comes first, so a host may turn a running world into a multiplayer
   room as long as the save is CUO's own (never the vanilla save).

## The reading that filed this

- Layer-end cut: `Cut LayerAdvance committed for world w-20261110-5938 … revision 845, layer 1 …`.
- Working order: IP session at the menu, then the continue → `Restored characters: 1 bound to present peers, 1 left
  unclaimed, 0 refused (key space IpDirect)`; the member rejoined the restored layer.
- Failing order: continue first, then `ip.create_host` → `IP-direct start refused: a world is running or generating`;
  the restore had already run in the Steam key space, so `none … was claimed`.

## Red probe (2026-11-17, before the fix)

- Instrument: the deployed build the install matches (`tools/verify-deploy.ps1`), the in-process driver, and the host's
  `BepInEx/logs/latest.log`.
- Order: the host at the menu → `continue-run` (no session; `state` reads `inWorld=true, role=None, active=false`) →
  click `ip.create_host`.
- Result: `[2026-11-17 22:20:30.748] [WRN] [CasualtiesUnknownOnline.IpDirectActions] IP-direct start refused: a world is
  running or generating.` The state after the click still read `role=None, active=false, lobby=0`: no session opened,
  so the natural order died at its first step and the invite never happened.

## Fix

`IpDirectActions` split one `CanStart` into the two operations it was serving. `CanCreateHost` keeps the transport and
Steam-session checks and then, for a live world, asks `LobbySwitchGuard.CanCreateLobby(session.Role, session.SessionActive,
worldFlowActive: true)` — the rule the Steam lobby create already applied to a solo player in a world — and logs
`IP-direct hosting opened over the solo world that is already running.` `CanJoinHost` keeps refusing while a world runs,
because a join changes the player's identity and re-keys the world. A sessioned world still refuses a second create.

The save stays CUO's own: `PreRunScriptLoadRunPatch` blocks the native `LoadRun`, and `RunSaveCoordinator.OnContinueRequested`
refuses unless CUO's own continue published a run baseline, so every live world a player can stand in carries the
baseline a joiner regenerates from.

## Acceptance reading (2026-11-17)

- Artifact: `0.1.0+2a7b6e7e1` (deployed with the acceptance channel; `tools/verify-deploy.ps1` reports the install
  matches this tree's build output).
- Case: `host-after-continue` (`RunsInBatch => false`, so the recorded 39-row wall-time baseline is untouched), focused
  run `.acceptance/hostafter5`, **PASS**, case 29.2 s, suite wall 57.4 s, host 12/0 and member 9/0 outside-case log
  errors.
- Readings, in order:
  - `22:47:02.607 host standing in the restored world`: `role=None, active=false, inWorld=true, runId=2` — a solo
    continued world with no session.
  - `22:47:03.734 host session over the running world`: `role=Host, active=true, inWorld=true, runId=2`, and the lobby
    controls (`home.leave`, `home.open_players`) appeared.
  - Host log: `IP-direct hosting opened over the solo world that is already running.`
  - Host log: `World join sent to 1 member(s) (tutorial: False, run 2, baseline follows: True).`
  - `22:47:07.565 member joined the running world`: `role=Guest, active=true, inWorld=true, runId=2, hostId=1`.
  - Host roster after the load: `5055416881145696052` — the member the host accepted.
- Harness change: `AcceptanceCase.RunsInBatch` (default true) plus the batch selection honouring it, `--list` printing
  `batch`/`by-name`, and the report header naming the by-name cases. This keeps rule 14's baseline honest while a case
  that gives up the session and restores a world runs on its own.
- Full batch, same artifact: `.acceptance/full-after-host2` — **39 of 39 green, 0 failed**, report wall 301.6 s (suite
  wall 302.4 s), shared pair setup 25.8 s, host 69/0 and member 133/0 log errors; the row table keeps
  `save-continue 37.9, last-layer-gate 23.0, character-choice 18.7, role-clearing 19.4`. An earlier batch on the same
  pair (`.acceptance/full-after-host`) read 318.8 s with `last-layer-gate` at 35.7 s against its usual 23-25 s; the
  second run read that row at 23.0 s, so the first reading was variance, not this change.

## Run sheet

- [x] Read the entrance family and the guard that refused the create.
- [x] Capture the red probe on the deployed build.
- [x] Split the create and join policies; reuse the solo-in-world create rule.
- [x] Add the `host-after-continue` case and the by-name batch selection.
- [x] Run the case on the real game and record the reading.
- [x] Run the full 39-row batch against the same artifact (it must stay green and inside the baseline).

## Residual

- A member that joins a host which hosted over a running world starts a new character; there is no saved row for it in
  the host's archive, which is the same shape as any first join.
