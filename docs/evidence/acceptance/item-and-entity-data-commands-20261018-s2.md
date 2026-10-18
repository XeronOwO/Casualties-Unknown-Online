# Acceptance record — item and entity data commands (`/summon`)

- Ticket: `item-and-entity-data-commands` — verdict: stays in `review/` (rows 1-9 pass; row 10 `unproven`)
- Batch: `20261018-s2` (the re-run of `20261018-s` on the review-fix commit) — tickets
  `done/item-and-entity-data-commands.md`
- Commit: `5cd3cb9f` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+5cd3cb9f7c93e7d819f58b5d356736b567c342d0`, checked with `tools/verify-deploy.ps1` (exit 0;
  `prelude-verify-deploy.txt`)
- Run: prelude 09:41:48 → 09:42:34, rows 09:42:38 → 09:49:41, both clients quit at 09:50 · Host: physical
  machine · Member: the `Steam1` sandbox (one second Steam account; the ids are written host/member here)
- Dependencies: `dotnet`, `game`, `deploy`, `steam` (already running), `sandboxie`, `hotrepl` (host 18590 +
  member 18591), `input`, `logs`, `artifacts` — the preflight reported eleven present (`prelude-preflight.txt`),
  the active BepInEx tree was CUO's (`prelude-session-environment.txt`)
- Artifacts: the `20261018-s2` subdirectory of the directory named by `acceptance-artifacts-dir` (`prelude-*`,
  `s1-*` … `s10-*`, `z-*`, `marks-*`, `prelude-driver.log`)

## Why this batch

The review-fix commit `5cd3cb9f` replaced the artifact the interrupted batch `20261018-s` had rehearsed, so the
ten rows of *What the acceptance batch owes* were re-run from scratch against it. One deploy, one host+member
session, the rows driven one step at a time through the committed in-process driver.

## Rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host `/summon beartrap player` | machine | **pass** | `s1-host-summon.json`: `Summoned beartrap at (0.0, 503.6)`; `s1-host-census.json` / `s1-guest-census.json` both carry `seq=3495675330`, `id=beartrap`, creator the host, at `(0.000, 503.615)`. The answer's point is the census position to a tenth. |
| 2 | Host `/summon medcrate` with the pointer over the world | machine | **pass** | `s2-host-summon.json`: `Summoned medcrate at (37.6, 473.5)` with no position argument (the native pointer default); both censuses carry `seq=3495675331`, `id=medcrate`, creation cell `(37, 473)` (`s2-host-census.json`, `s2-guest-census.json`). The crate settles to `(38.7, 461.2)` after the creation — the answer names the creation point, the census the settled pose. |
| 3 | Host `/summon item bandage player {Condition:0.4f}` | machine | **pass** | `s3-host-summon.json`: `Summoned bandage (#1142782281667) at (-1.9, 502.6) with state {Condition:0.4f,…}`; both peers' `/data get id 1142782281667` read `bandage [world (-1.915, 502.615)]` with `Condition:0.4f` (`s3-host-read.json`, `s3-guest-read.json`); the host body reads `positionX -1.915, positionY 502.615` (`s3-host-body.json`) — the item lies where the host stood. |
| 4 | Host `/summon cavetick player` | machine | **pass** | `s4-host-summon.json`: `Summoned cavetick at (-1.9, 502.6)`; both censuses carry `seq=3495675332`, `id=cavetick`, `animal=True` (`s4-host-census.json`, `s4-guest-census.json`); the host's enemy table reads `runtimeSpawns: 1`, the first enemy `prefab=cavetick`, `spawnX=-1.915 spawnY=502.615`, `runtimeSpawned=True` (`s4-host-enemies.json`); the host log carries `[Enemy] host bound runtime spawn 8DF2CB8FBE68B38:74:0 (prefab cavetick).` at 09:43:36.515. |
| 5 | Member (switch open + grant) `/summon item bandage player` | machine | **pass** | the member's console history (`s5-guest-history.json`) reads `Summoned bandage (#1147077248963) at (-0.1, 503.2)`; both peers' `/data get id 1147077248963` read `world (-0.124, 503.18)` (`s5-host-read.json`, `s5-guest-read.json`) — created by the host at the point the member's own scene resolved; the member's body reads `positionX -0.6, positionY 501.84` after it settled away from the creation point (`s5-guest-body.json`). |
| 6 | Member, switch closed (announced), the same verb | machine | **pass** | `s6-guest-summon.json`: `Refused locally: the host's cheat switch (AllowMemberCheatVerbs) is off, … Nothing was done here.`; the host log window from `marks-s6.txt` has no `[Summon]` line at all (`log.ps1 read`) — no request was sent, nothing was created. |
| 7 | Member, switch open, no grant (announced) | machine | **pass** | `s7-guest-summon.json`: the roster refusal `the host's cheat roster (announced to this client) does not name you …`; the host log window from `marks-s7.txt` has no `[Summon]` line — no request, nothing created. |
| 8 | `/summon notanprefab` and `/summon beartrap player {Condition:0.5f}` | machine | **pass** | `s8-host-unknown.json`: `Refused: no prefab 'notanprefab' could be materialized…`; `s8-host-entity-state.json`: `Refused: 'beartrap' is not an item definition, so it takes no state argument — … Nothing was created.`; the runtime-entity census still holds exactly the three objects of rows 1, 2 and 4 (`s8-host-census.json`). |
| 9 | A later joiner: the member leaves and re-enters | machine | **pass** | the member left the world (`s9-guest-leave.json` `inWorld=false`), left the lobby (`s9-guest-state-nolobby.json` `lobby=0, role=None`), rejoined the same lobby and re-entered (`s9-guest-state-rejoined.json` `inWorld=true`); its census then carries the row-1 `beartrap` once, `seq=3495675330` at `(0.000, 503.615)` — the same creation key and point — with the medcrate and cavetick once each (`s9-guest-census.json`; host `s9-host-census.json` agrees on the ids). The host sent the entry group (`Sending world-entry snapshot group to <member>` at 09:45:49.511). |
| 10 | The layer-boundary request | machine | **unproven** | first attempt: the member's inbound blackout was armed at 09:47:18 (`s10-guest-blackout-on.json` `armed=true`) and the host descended (`s10-host-skiplayer.json`, 09:47:21); the host read `runEpoch=1, layerIndex=1` at 09:47:25 while the member still read `layerIndex=0` at 09:47:32 (`s10-host-gen-after.json`, `s10-guest-gen-after.json`, `s10-guest-gen-before.json`) — the stale window existed. But the request went out at 09:47:41, about 23 s after the arm and past the member's silence budget: its session had already ended and the console answered `No active session — a cheat verb needs one …` (`s10-guest-summon.json`). The blackout was disarmed (`s10-guest-blackout-off.json`), and a retry could not re-establish the member (`s10-guest-state-rejoin.json` `active=false, hostId=0`; the later leave/join click no longer applied). No stale request reached the host. The check itself stays pinned by `ARequestFromAnotherLayer_IsRefusedByTheHostAndCreatesNothing`. |

## Residuals for the user

None: no row needs a person's judgement. Row 10 is `unproven`, not a residual.

## Limits

- **Row 10's live window.** The blackout DID hold the member on layer 0 while the host moved to layer 1 — the
  host read layer 1 four seconds after `skiplayer`, and the member still read layer 0 seven seconds later. The
  miss was the run's own pacing: the stale request went out about 23 s after the blackout was armed, past the
  member's silence budget, so its session had ended and the command answered `No active session…` instead of
  sending. The next attempt sends the request the moment the host's generation moves, and reads the member's
  generation in the same step; a retry could not re-establish the member afterwards. The row is not weakened.
- **Row 2's pointer.** The invocation named no position, so the pointer default is what produced the answer; the
  pointer's screen geometry was not independently probed this run (the `remote-gesture` aim-ring mode needs an
  item argument). The answer's point and the census's creation cell agree.
- **One member, no third client.** No third-peer view is claimed anywhere in this batch.
- The runtime-entity census was read after each creation; the medcrate's settled pose differs from its creation
  point because the container falls, which the row records rather than treats as drift.
