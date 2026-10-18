# Acceptance record — item and entity data commands (`/summon`)

- Ticket: `item-and-entity-data-commands` — verdict: **all ten rows pass**, the ticket moves to `done/`
- Batch: `20261018-s3` (the full ten rows on the second-review fix commit)
- Commit: `35a0b14d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+35a0b14d2fa62e51336ec003f72572da6b774fd4`, checked with `tools/verify-deploy.ps1` (exit 0;
  `prelude-verify-deploy.txt`)
- Run: prelude 10:34:00 → 10:34:47, rows 10:35:02 → 10:38:07, row 10 10:38:32 → 10:41:21, both clients quit
  at 10:43 · Host: physical machine · Member: the `Steam1` sandbox (the ids are written host/member here)
- Dependencies: `dotnet`, `game`, `deploy`, `steam` (already running), `sandboxie`, `hotrepl` (host 18590 +
  member 18591), `input`, `logs`, `artifacts` — the preflight reported eleven present
  (`prelude-preflight.txt`), the active BepInEx tree was CUO's (`prelude-session-environment.txt`)
- Artifacts: the `20261018-s3` subdirectory of the directory named by `acceptance-artifacts-dir`
  (`prelude-*`, `s1-*` … `s10-*`, `window.log`, `fast-window.log`, `s10-host-refusal.txt`, `z-*`,
  `prelude-driver.log`)

## Why this batch

The second independent review of `5cd3cb9f` (recorded in the ticket) closed one major and nine smaller findings
in `35a0b14d`. The artifact was rebuilt and redeployed from that commit — the version stamp follows the commit —
so all ten rows of *What the acceptance batch owes* were re-run from scratch rather than inheriting the
`20261018-s2` run, whose artifact was the previous build. The s2 run's only open row was 10, the live
layer-boundary window; this batch re-verified rows 1-9 on the new artifact and settled row 10.

## Rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host `/summon beartrap player` | machine | **pass** | `s1-host-summon.json`: `Summoned beartrap at (0.0, 470.6)`; `s1-host-census.json` / `s1-guest-census.json` both carry `seq=2124974028`, `id=beartrap`, creator the host, at `(0.000, 470.616)`. The answer's point is the census position to a tenth. |
| 2 | Host `/summon medcrate` with the pointer over the world | machine | **pass** | `s2-host-summon.json`: `Summoned medcrate at (-15.8, 444.8)` with no position argument (the native pointer default); both censuses carry `seq=2124974029`, `id=medcrate`, creation cell `(-16, 444)` (`s2-host-census.json`, `s2-guest-census.json`). The crate settles to `(-18.394, 431.766)` after the creation — the answer names the creation point, the census the settled pose. |
| 3 | Host `/summon item bandage player {Condition:0.4f}` | machine | **pass** | `s3-host-summon.json`: `Summoned bandage (#1151372216259) at (-2.3, 470.6) with state {Condition:0.4f,…}`; both peers' `/data get id` read `bandage [world (-2.288, 470.616)]` with `Condition:0.4f` (`s3-host-read.json`, `s3-guest-read.json`); the host body reads `positionX -2.277, positionY 470.616` (`s3-host-body.json`) — the item lies where the host stood. |
| 4 | Host `/summon cavetick player` | machine | **pass** | `s4-host-summon.json`: `Summoned cavetick at (-2.3, 470.6)`; both censuses carry `seq=2124974030`, `id=cavetick`, `animal=True` (`s4-host-census.json`, `s4-guest-census.json`); the host's enemy table reads `runtimeSpawns: 1` with the cavetick `runtimeSpawned=True`, `spawnX=-2.267 spawnY=470.616`, health 10.29 (`s4-host-enemies.json`), and the host log's next snapshot to the member reads `82 enemies, 1 runtime spawns` (10:36:06). The animal was killed at 10:36:22 and its accepted-creation record dropped (`[EntitySpawn] cavetick … died — dropped the accepted creation record`), which is why the later censuses no longer carry it. |
| 5 | Member (switch open + grant) `/summon item bandage player` | machine | **pass** | the member's console history (`s5-guest-history.json`) reads `Summoned bandage (#1155667183555) at (-1.9, 470.6)`; both peers' `/data get id 1155667183555` read `world (-1.92, 470.616)` (`s5-host-read.json`, `s5-guest-read.json`) — created by the host at the point the member's own scene resolved, which is the member's body position (`s5-guest-body.json`). |
| 6 | Member, switch closed (announced), the same verb | machine | **pass** | `s6-guest-summon.json` / `s6-guest-history.json`: `Refused locally: the host's cheat switch (AllowMemberCheatVerbs) is off, … Nothing was done here.`; the host log window from `marks-s6.txt` has no `[Summon]` line at all (`log.ps1 read`) — no request was sent, nothing was created. |
| 7 | Member, switch open, no grant (announced) | machine | **pass** | `s7-guest-summon.json` / `s7-guest-history.json`: the roster refusal `the host's cheat roster (announced to this client) does not name you …`; the host log window from `marks-s7.txt` has no `[Summon]` line — no request, nothing created. |
| 8 | `/summon notanprefab` and `/summon beartrap player {Condition:0.5f}` | machine | **pass** | `s8-host-unknown.json`: `Refused: no prefab 'notanprefab' could be materialized…`; `s8-host-entity-state.json`: `Refused: 'beartrap' is not an item definition, so it takes no state argument — … Nothing was created.`; `s8-host-census.json` still holds exactly the two surviving objects of rows 1 and 2 (`beartrap`, `medcrate`) and no new sequence — the cavetick is absent because it died in row 4, not because of these refusals. |
| 9 | A later joiner: the member leaves and re-enters | machine | **pass** | the member left the world (`s9-guest-leave.json`) and the lobby (`s9-guest-state-nolobby.json` `lobby=0, role=None`), rejoined the same lobby and re-entered (`s9-guest-state-rejoin-1.json` `active=true, inWorld=true`); its census then carries the row-1 `beartrap` once, `seq=2124974028` at `(0.000, 470.616)`, with the medcrate once (`s9-guest-census.json`); the host's census agrees on both ids (`s9-host-census.json`), and the host sent the entry group at 10:38:03 (`Sending world-entry snapshot group to <member>`). |
| 10 | The layer-boundary request | machine | **pass** | the member stayed at `run 1 layer 2` behind the blackout while the host reached `run 1 layer 3` (`fast-window.log`), and its `/summon item bandage player` was refused by name: `s10-host-refusal.txt` carries `[WorldReportGeneration] WorldSummonRequest from <member> belongs to run 1 layer 2 while this side is at run 1 layer 3 — … refused as stale` and `[Summon] request from <member> for bandage at (0.0,480.6) refused: the request belongs to run 1 layer 2 while this host is at run 1 layer 3 — positions are layer-relative, so nothing was created; run /summon again here.` The member's console printed the same sentence (`cur2-guest-history.json`), the blackout was disarmed after 8.2 s (inside the 15 s budget), the session stayed active, and the layer's runtime-entity census is empty (`s10-host-census.json`) with no creation line in the window — nothing was created. |

## Row 10 — how the live window was held

The s2 run could not hold the window because the request went out ~23 s after the blackout was armed, past the
member's 15 s silence budget. This batch's first attempt used the committed driver one step at a time and hit the
same wall from the other side: the host needed 13 s after `skiplayer` before its own `generation-read` said
`inWorld=true` at the new layer, so the member's command ran 15.2 s after the arm and answered `No active
session` (`window.log`). Two facts the batch then established:

- The member must be blacked out BEFORE `skiplayer`: a diagnostic skip without a blackout showed the member's
  own generation following the host to the next layer within 3 s, before the host itself re-entered — so arming
  after the skip cannot keep the old stamp.
- The committed driver's one-process-and-one-connection per step costs about 2.2 s per step, which is what made
  the 15 s budget impossible to hit. The window was therefore driven through the SAME in-process evaluator
  protocol over two sockets opened before the window (`fast-window.ps1`, a batch-local helper, never committed;
  the eval snippets are the committed `game-console`, `generation-read` and `net-receive-blackout` recipes and
  the console template): arm, skip, poll the host's generation, send the member's `/summon`, disarm — 8.2 s.

After the first attempt the member's session had ended; the batch revived it by leaving and rejoining the lobby
(`s10b-guest-leave-lobby.json`, `s10b-guest-rejoin.json`), then used the next layer change for the successful
window. The s2 and s3 failures are recorded here as the run's own pacing/recovery, not as product defects; the
refusal itself is pinned by `ARequestFromAnotherLayer_IsRefusedByTheHostAndCreatesNothing`.

## Residuals for the user

None: no row needs a person's judgement.

## Limits

- **Row 2's pointer.** The invocation named no position, so the pointer default is what produced the answer; the
  pointer's screen geometry was not independently probed this run. The answer's point and the census's creation
  cell agree.
- **One member, no third client.** No third-peer view is claimed anywhere in this batch.
- **Row 8's census baseline.** The cavetick of row 4 died at 10:36:22 (killed by the host's own item hits), so
  the row-8 census holds two objects instead of three; the row's check is that neither refusal added a sequence,
  and no new one appears.
- **Row 10's helper.** The window was driven through the evaluator protocol directly because the committed
  driver's per-step process/connection cost cannot fit the 15 s silence budget once the host needs ~8-13 s to
  re-enter the next layer. The helper is a local batch artifact, named here so the run is reproducible from the
  record; the lesson is folded into `docs/acceptance/lessons.md`.
- **The medcrate's settled pose** differs from its creation point because the container falls, which the row
  records rather than treats as drift.
