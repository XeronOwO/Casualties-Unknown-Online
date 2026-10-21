# Acceptance record — Enemy generation sync: the host's roster is the identity (batch `20261021-f`)

- Ticket: `enemy-generation-pairing-late-join` — verdict: **back to `todo/` (row 1 fails on a dense
  world)**. Status field: `- Status: Todo — Rejected (batch 20261021-f: row 1 — the entry adoption
  built the whole roster after the host's world-entry layer reset cleared the members' adoption
  candidates; row 4's fix and the late-join release/host-resume fixes pass)`.
- Batch: `20261021-f` — this ticket (the rerun of row 4 plus rows 2 and 3, with rows 5 and 6 re-driven
  where the session allowed it).
- Commit: `ecec0d5b` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+ecec0d5b32fd8c9780336513fe35430e4b9fe3a2` (`prelude-a-verify-deploy.txt`,
  `prelude-b-verify-deploy.txt`; `tools/verify-deploy.ps1` exit 0).
- Run: 2026-10-21 20:21 → 20:32 local · Host: physical machine · Guests: sandbox `<sandbox-guest>` and
  `<sandbox-alt>`.
- Dependencies: the eleven the preflight reported present (`RESULT: OK - a full two-client run is
  possible; the alternate third client is configured`; `preflight.txt`).
- Artifacts: `20261021-f/` under the directory named by `acceptance-artifacts-dir` — the two preludes,
  the session ids, the log windows (`a-row1-evidence.txt`, the `log.ps1` reads), the batch-local probes
  (`x-row.cs`, `x-scene-summon.cs`, `x-enemy-kernel.cs`), the summon/blast/descent answers, the tables
  (`b-row3-*-table.json`, `b-row3-kill2-*-table.json`) and the member state reads.

## The run

Session A (`entry`): host + both members were in the lobby before the run started (lobby
`109775241715389195`; all three `inWorld` at 20:22:34). The host's world was a dense generation: 956
animals and 10 traders, and its first capture ran **during** generation (`host enemy baseline established
over 0 animals`, 20:22:33.154), so 83 rows were published before the world-entry reset. Both entry
snapshots were sent at 20:22:43.85 (`956 enemies`).

Session B (`drift`): the host started a run alone at 20:27:39, its solo world ran 30 s, it created the
lobby from inside that running world at 20:28:44 (lobby `109775241716070909`), and both members joined
mid-run (`inWorld` all three at 20:29:00). This is the shape that returned both members to the menu in
batch `20261021-e`.

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Both members present at entry read `roster adopted: N rows (… aligned=True)`, N = the host's row count, zero unresolved and zero surplus, on the entry snapshot; no pairing-failure line | machine | **fail** | Session A: both members read `[Enemy] roster adopted: 956 rows (0 kept, 0 reused, 946 built, 0 surplus destroyed, 0 stale bindings dropped, 10 unresolved) — aligned=False.` at 20:22:45.409 (guest) and 20:22:45.371 (alt), with ten `roster row … (prefab trader) could not be materialized` warnings. The host's roster is 956 (`host roster is ready over 956 enemies`; `snapshot sent … 956 enemies, 291 runtime spawns`). No pairing-failure line exists (whole-log grep: 0). The finding's cause is in `## The row-1 finding`; the entry repair at 20:23:47.942 read `956 kept, 946 surplus destroyed, 0 unresolved — aligned=True`, so the world converges ~2.5 s later, but the row's own condition is the entry snapshot. |
| 2 | The drift shape: the host's world ran before the session; the member's row is reused or materialized at the host's position, aligned, and no local-only set | machine | **pass** | Session B: both members joined a world that had already run 30 s solo, and both read `[Enemy] roster adopted: 88 rows (0 kept, 78 reused, 10 built, 10 surplus destroyed, 0 stale bindings dropped, 0 unresolved) — aligned=True.` (guest 20:28:59.903, alt 20:29:07.247). All three tables then held 89 rows (88 generated + the later summon). |
| 3 | A member that joins mid-run: the entry snapshot is aligned with the host's rows, sees the same enemies, and a killed/removed enemy disappears from all three views | machine | **pass** | The three tables held the same id set (`b-row3-*-table.json`: 89 rows, counters 0–88, `has88` true on all three). A generated shadecrawler (`…:56:0`) was killed by a structural blast at its own position (`b-row3-kill-blast2.json`: `healthBefore 99.572 → healthAfter -6768.835`); the host logged `[Enemy] host dropped 1 destroyed enemy binding(s).` (20:29:59.094) and both members `[Enemy] guest removed enemy 8DF2F6EACEDB0AA:56:0 from kernel batch.` + `guest destroyed removed enemy …:56:0` (20:29:59.107/.108). All three tables read 88 afterwards. |
| 4 | Runtime spawns: the host triggers a runtime animal after a member is in the world; the member logs `live-adopted` for it within one stream pass, both members see it at the host's position, and it follows the host's state stream | machine | **pass** | Session A: `/summon cavetick` created one at (70.3,448.9) at 20:25:25.451 and the host bound `…:956:0` at 20:25:25.506; the guest logged `live-adopted 1 new enemy row(s) from the stream.` at 20:25:26.085 (`live roster row …:956:0 bound the local cavetick at (70.3,448.9)`) and the alt at 20:25:26.080. The three scene reads then showed one cavetick copy at **(76.323,454.031) with health 15 on all three clients** (the same moving position), and the three tables all held 957 rows with the row. Session B repeated it after both members were in the world: created (61.5,379.2) at 20:29:26.121, host bound 20:29:26.143, guest live-adopted 20:29:26.299 (`bound the local cavetick at (61.5,379.2)`), alt 20:29:26.307; the 60 s repair at 20:29:44 sent both members `89 enemies, 1 runtime spawns`. Whole-log greps for `unknown enemy`: **0** on both members in both sessions. |
| 5 | Layer descent: all three descend; the new layer's rows adopt (aligned, zero unresolved), the old layer's bindings are dropped, neither member keeps an old-layer enemy | machine | **pass** | Session B: host `skiplayer` at 20:30:30; `[LayerReset] dropped the previous layer's 74 enemy row(s)` (20:30:30.934), `host roster is ready over 74 enemies`, `snapshot sent … 74 enemies, 0 runtime spawns, run 1 layer 1` (20:30:39.563/.569). Both members logged `guest dropped its enemy bindings: the host reset the roster for a new layer.` and adopted `74 rows (0 kept, 74 reused, 0 built, 0 surplus destroyed, 0 unresolved) — aligned=True.` (guest 20:30:40.272, alt 20:30:40.227), then `74 kept … aligned=True` on the repeat (20:30:44.677/.693). |
| 6 | Regression: damage, kills, removals, attack announcements and bites reach the bound copies; the host's screen carries no new warning; the member's 20 Hz drive still moves the bound copies | machine | **not re-run in full** | Row 3's kill is the damage + removal evidence on this build (the blast moved the bound copy's health across the death threshold and both members removed it). No spider bite or attack announcement was driven in this session (the host body was never in range of a spider), so the bite/announcement half rests on batch `20261021-e`'s readings, whose code paths this commit does not touch. No `[WRN] [Enemy]` line was produced on the host in either session (the host's only warnings were a Steam SDR availability line and two pre-existing item-termination rejections). |

## The row-1 finding

The batch's world was dense (956 animals, 10 traders), and the host's capture ran during world
generation: `[Enemy] host enemy baseline established over 0 animals.` at 20:22:33.154, then binds from
20:22:39.006. Because 83 enemy rows existed in the kernel when the host entered its world, the
world-entry layer reset fired:

```text
[2026-10-21 20:22:43.718] [INF] [WorldService] [LayerReset] dropped the previous layer's 83 enemy row(s) …
```

That reset is a committed kernel event, and both members applied it **after their own generation had
finished** (the guest froze 374 copies at 20:22:40.254; the reset arrived at 20:22:45.152):

```text
[2026-10-21 20:22:45.152] [INF] [EnemySyncService] [Enemy] host reset the enemy table for a new layer — dropped 83 live row(s) …
[2026-10-21 20:22:45.152] [INF] [EnemySyncCoordinator] [Enemy] guest dropped its enemy bindings: the host reset the roster for a new layer.
[2026-10-21 20:22:45.409] [INF] [EnemySyncCoordinator] [Enemy] roster adopted: 956 rows (0 kept, 0 reused, 946 built, … 10 unresolved) — aligned=False.
```

`ClearRosterState` clears the member's tracked animal set, and the entry adoption ran SYNCHRONOUSLY from
the
snapshot event — before the next frame's freeze pass could rebuild the set from a scene scan — so the
adoption had no local copy to reuse: it materialized 946 copies and could not materialize the ten
`trader` rows (the runtime-creation path has no trader prefab), which stayed unresolved. The member then
held 1902 scene copies (`guest froze 1902 enemy copies` at 20:22:48.447) until the next adoption pass
destroyed the 946 surplus and adopted the materialized set (`956 rows (956 kept, … 946 surplus destroyed,
0 unresolved) — aligned=True` at 20:23:47.942); the ten traders were live-adopted from the re-scanned
scene at 20:22:49.015.

Two independent roots are visible in the same run:

- **The host captures during generation.** The capture-every-frame pump binds animals while the world is
  still generating, so the kernel carries rows the world-entry layer reset must drop; the reset exists
  for a layer being *left*, and a run's first layer has nothing to leave. Gating the capture on a settled
  world (the same `CanPublishRoster` gate the publication uses) removes the early rows and therefore the
  reset at entry.
- **A roster reset clears the adoption candidates and does not rebuild them.** The member's tracked set is
  the only source of adoption candidates; after a reset only a one-shot generation-finish scan (or a new
  animal's `Start`) adds to it. Reading the candidates from the live scene on the adoption pass (the host
  already unions a scene scan) would make the reset lose nothing.

A third, independent classification finding: the host's snapshot reported `291 runtime spawns` at
20:22:43.85 and `873 runtime spawns` at 20:22:46.64, for a roster whose rows are generation output. The
`IsRuntimeSpawn(sessionActive, !generating)` rule classifies every animal whose `Start` runs after the
generation flag flips as a runtime spawn; in a dense world the generator's own animals finish their
`Start` after that flip (the batch-e worlds were small enough that none did). The flag is therefore not a
reliable "created during play" marker for generation output at this scale.

## The row-4 fix, verified

The live pass now binds a runtime row on the first 20 Hz batch in both sessions: after the host's bind,
the members' `live-adopted` lines follow within 160–575 ms (the host's bound table → the 20 Hz stream →
the member's kernel fact → `FindLiveCopy` → the creation-channel copy), instead of waiting for the next
snapshot. The three scene copies agree exactly, and the attacks were not dropped as unknown enemies
(zero `unknown enemy` lines). The 60 s repair carries the runtime row in its detail list
(`1 runtime spawns`), so a member that missed the live pass still gets it.

## The start-gate and host-resume fixes, verified

Session B is the exact shape that returned both members to the menu in batch `20261021-e`. This run:

- The host marked itself playing when the lobby rose around its running world, half a second after the
  create-lobby call: `[2026-10-21 20:28:44.645] [INF] [StartGateCoordinator] The session rose around an
  already-running world — the host resumes playing.` **No** `[Gate] keeping the loading screen up` line
  exists in the session-B host log (batch `20261021-e` had it at 19:28:45.502 and nothing released it).
- Both members were released into the already-running world: `Start gate pass — <member> enters directly
  (game already running).` (20:28:59.235) and `… (alt) …` (20:29:06.410), followed by `World ready — start
  playing.` on each.
- Neither member was returned to the menu: zero `WorldReady never arrived within 60s` lines and zero
  `Scene state: InMenu` lines in either member's log, and at 20:30:10 (past the 60 s valve) both read
  `inWorld=true, active=true`.
- The 60 s repair pump then found them in the world and sent the repair group to both (20:29:44.423/.464;
  `89 enemies, 1 runtime spawns`).

## Residuals for the user

None: every row was judged from the run's own log/table/probe evidence. Row 6's bite/announcement half is
named as not re-run, not as a residual.

## Limits

- No frames were captured; every row is machine-judged. Row 2's "no local-only set" is judged from the
  adoption line and the tables, not from a window frame.
- The row-1 world was the batch's only entry session; the defect is timing-dependent (a dense world whose
  generation outlasts the host's early capture), so a later batch on a smaller world may not reproduce it.
  The finding's mechanism is pinned by the log sequence above.
- Row 6's bite/attack half was not driven this batch (no spider reached the host body); batch
  `20261021-e`'s references stand for that half, and this commit touches neither the bite nor the attack
  path.
- The session-A tables/scenes were read with compact batch-local probes: the committed `enemy-table-read`
  and `enemy-scene` recipes return the whole set and their answers are truncated at 956 rows (the eval
  channel's limit). The probe sources are in the artifact directory.
