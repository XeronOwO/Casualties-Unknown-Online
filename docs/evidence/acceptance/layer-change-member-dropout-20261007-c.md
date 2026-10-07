# Acceptance record — Consecutive layer changes drop the members out of the world and storm the log

- Ticket: `layer-change-member-dropout` — verdict: **pass, moved to `done/`**: the row re-driven with BOTH
  members' inbound parked inside one command holds both members in the world with a local body, and every
  client's log growth read as a size stays two to three orders below the pre-fix storm.
- Batch: `20261007-c` — tickets `layer-change-member-dropout` and
  `remote-clone-warning-storm-on-member-dropout` (the family's third producer, whose runtime row this
  batch reads on the same fixture; its own record is
  `remote-clone-warning-storm-on-member-dropout-20261007-c.md`)
- Commit: `870caead` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+870caeadd3a3b0e91987a71f9ec4585eb9700bcd` (the run's tree and the artifact's commit are the same,
  verified by `tools/verify-deploy.ps1` before the clients started)
- Run: 2026-10-07 11:21 → 11:26 local · Host: physical machine (the operator) · Guest: sandbox `Steam1` ·
  Third client: sandbox `Steam2`
- Dependencies: the eleven the preflight reported present (`RESULT: OK - a full two-client run is possible;
  the alternate third client is configured`)
- Artifacts: `20261007-c/` in the directory named by `acceptance-artifacts-dir` — the bring-up states
  (`a0-*`, `a4-*`), the fixture reads (`s0-*`), the two attempts' marks (`m1-before.txt`, `m2-before.txt`),
  the park and change probes (`pA-*`, `pB-*`), the state and body reads (`tA1-*`, `tA2-*`, `tB1-*`,
  `tB2-*`), the window frames (`tA2-guest-window.png`, `tA2-alt-window.png`) and the log excerpts
  (`ev-guest-remote-body.txt`, `ev-alt-remote-body.txt`)

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A consecutive layer change with BOTH members' inbound parked leaves both members in the world with a local body | machine | **pass** | Attempt A: `tA1-guest-state.json` / `tA1-alt-state.json` `inWorld: true` at +16 s and `tA2-*` at +34 s, both with `container-read mode=local` answering `localCount: 1` (`emergencylight`) — the same body and item they carried into the change (`s0-guest-local.json`, `s0-alt-local.json`, taken at 11:23:36). Both parks armed (`pA-guest-blackout-on.json`, `pA-alt-blackout-on.json`, `pA-host-blackout-on.json` all `armed: true`, `subscribersAfter: 0`) and disarmed (`pA-*-blackout-off.json` `parked: false`) inside ONE command, the members held from 11:23:47 to 11:24:00 |
| 2 | Each client's log growth, read as a size, is bounded | machine | **pass** | Attempt A census since `m1-before.txt`: host 0.261 MB / 1,353 lines, guest 0.452 MB / 2,591, alt 0.396 MB / 2,229 (a later read of the same mark over a ~100 s window: 0.362 / 0.732 / 0.675 MB) — 0.35–0.6 MB/min against the ~8.2 MB/min pre-fix storm; every family line bounded: `no Body component` 0, `not found in scene` 0, `[Fluid] region` 0, `[LayerMod] baseline divergence` 0, `[ItemPhysics] settle` 0 / 69 / 4 (see Limits for the 69) |
| 3 | *(control, no park)* The same change without a park still drops both members out of the world | machine | **pass** | Attempt B: `tB1-guest-state.json` / `tB1-alt-state.json` `inWorld: false` at +6–9 s with `localCount: 0`, both back `inWorld: true` at +30 s (`tB2-*`) — the fixture still reproduces the dropout, so attempt A's pass is the park's doing and not a change in the game's own behaviour |

## The layer change that ran

Both attempts drove the game's own `skiplayer` through the CUO console action
(`game-console command=skiplayer,args=none`, `applied: true` in `pA-skiplayer.json` / `pB-skiplayer.json`)
on the host. Each one produced the consecutive change this ticket is about, read from the host's log:

- Attempt A: `[LayerReset] dropped the previous layer's world-rooted items; 2 item record(s) remain
  (carried items cross the boundary).` at 11:23:50.783, then the automatic second advance at 11:23:59.814
  (`[LayerMod] enter state=… chance=40 depth=1 override=None`) with its own `[LayerReset]` at 11:23:59.881.
  The members were parked across BOTH advances (11:23:47 → 11:24:00).
- Attempt B: `[LayerReset]` at 11:24:52.258 and the second advance at 11:24:59.051 (`depth=2`) with its
  `[LayerReset]` at 11:24:59.164.

The `2 item record(s) remain` figure is the carried kit the boundary preserves, which is why a member's
`localCount: 1` after the change is the same `emergencylight` it carried before it.

## What the run read about the row's premise

- **The park is the deciding variable and it is now measured on both members at once.** Attempt A parks
  host, guest and alt inside one `pwsh` invocation (arm → `skiplayer` → explicit `Start-Sleep 9` → disarm);
  attempt B, minutes later in the same session on the same world, drove the same command with no park. A
  held the members; B dropped both and they returned on their own inside 30 s. Batch `20261007-a` could
  only park one member, which is exactly the gap its Limits named.
- **The window is a blunt instrument and the ceiling is respected.** `net-receive-blackout` drops every
  inbound frame while armed, so the park lasted 13 s on the members (11:23:47 → 11:24:00) and 11 s on the
  host, inside the 15 s `GuestHostSilenceWatchdog` ceiling the local facts record.
- **The visual half agrees with the machine half.** `tA2-guest-window.png` and `tA2-alt-window.png` (+34 s,
  after both advances) each show the loaded world with the character on screen and the other two peers'
  name tags — not the unloaded dark world with `Members: 3` that batch `20261007-a`'s frames captured from
  a dropped member.
- **Nothing throws.** `[ERR]`, `[ERR][Unity:Exception]` and any `Exception` line: 0 in all three clients
  over both attempt windows.

## Residuals for the user

None: every row above is a machine row or a window frame the agent read.

## Limits

- **One direction and one host.** Both attempts drove `skiplayer` on the physical-machine host with both
  members as sandbox guests; the mirrored direction was not driven.
- **Two attempts, one fixture, one layer edge.** The run does not say how often a dropped member returns:
  this batch's unparked attempt returned inside 30 s, batch `20261007-a` had attempts return inside 20 s and
  another member still out at +60 s. That spread is the reading, not a rate.
- **`[ItemPhysics] settle` read 69 lines on the guest in attempt A** (batch `20261007-a`: 14 / 13; the host
  read 0 and the two other clients 4). The window is per (item, distance band), so a copy whose band moves
  reports again: the excerpt shows one subject stopping at `repeat 7` with a `1 subject(s) held back` note
  while the band changes, and the total is ~1.5 lines/s — the bound holds per subject, and a future
  band-thrashing item is the producer to watch, not a breach of the row.
- **The layer count.** Two changes per attempt (the driven one and the game's own follow-up ~9 s later); the
  run did not descend further and did not drive the elevator path, which this ticket still lists as open.
- **No wire and no save reading.** No messages and no saves were read, and no hand-feel was judged.
- **The park covers the whole inbound plane**, not one message class, so the reading names "the member
  processes no inbound during the change", not a single message that decides the exit.
