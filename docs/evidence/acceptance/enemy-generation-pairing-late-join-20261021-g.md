# Acceptance record — Enemy generation sync: the host's roster is the identity (batch `20261021-g`)

- Ticket: `enemy-generation-pairing-late-join` — verdict: **moved to `done/`** (all six rows pass; status
  field: `- Status: Done (batch 20261021-g, 2026-10-21: …)`).
- Batch: `20261021-g` — this ticket (the rerun of all six rows on the fix cycle's build, both the dense
  entry shape and the drift shape).
- Commit: `03cc1099` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+03cc1099340f77b5d5142a8c3c18a42fbd7175e4` (`prelude-a-verify-deploy.txt`,
  `prelude-b-verify-deploy.txt`; `tools/verify-deploy.ps1` exit 0).
- Run: 2026-10-21 21:12 → 21:20 local · Host: physical machine · Guests: sandbox `<sandbox-guest>` and
  `<sandbox-alt>` (three clients).
- Dependencies: the preflight reports `11 present` / `RESULT: OK - a full two-client run is possible; the
  alternate third client is configured` (`preflight.txt`; taken as the record's read-only dependency state
  while the run's second session was up).
- Artifacts: `20261021-g/` under the directory named by `acceptance-artifacts-dir` — the two preludes,
  the deploy/verify reads, the session ids, the per-window log extracts (`a-*-enemy.txt`,
  `a-row6-*.txt`, `b-*-enemy-entry.txt`), the batch-local probes (`row.cs`, `scene.cs`) with their
  answers (`a-row*.json`, `b-row3-*.json`), and the blast/summon/descent answers.

## The run

Session A (`entry`): host + both members in the lobby before the run started (lobby
`109775241721188851`; all three `inWorld` at 21:13:23). The world generated **375 animals** (15 traders
among them) with a ~11 s generation window (start 21:13:19, settled edge 21:13:30.116): the shape that
failed row 1 in batch `20261021-f`, smaller in count but with the same long generation.

Session B (`drift`): the host started a run alone at 21:16:37, its solo world ran 30 s, it created the
lobby from inside that running world at 21:17:44 (lobby `109775241721789169`), and both members joined
mid-run (all three `inWorld` at 21:18:05).

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Both members present at entry read `roster adopted: N rows (… aligned=True)`, N = the host's row count, zero unresolved and zero surplus, on the entry snapshot; no pairing-failure line | machine | **pass** | The host captured ONLY at the settled edge: `generation finished: 375 animal(s) exist at the settled edge` (21:13:30.116) then `host enemy baseline established over 375 animals` (21:13:30.124) — no `host bound` line exists during the generation window (0 before the baseline), and the entry reset names an EMPTY enemy table: `[LayerReset] dropped the previous layer's 0 enemy row(s) and 248 fluid chunk(s)` (21:13:30.095). `snapshot sent … 375 enemies, 0 runtime spawns, run 1 layer 0` (21:13:30.193/.196). Both members: `generation finished: 375 animal(s)` then `roster adopted: 375 rows (0 kept, 375 reused, 0 built, 0 surplus destroyed, 0 stale bindings dropped, 0 unresolved) — aligned=True` (guest 21:13:30.565, alt 21:13:30.563), and **zero** `[Enemy] host reset the enemy table for a new layer` lines on either member (whole-window grep), zero `pairing failed`, zero `unknown enemy`. The next repair repeats `375 kept … aligned=True`. |
| 2 | The drift shape: the host's world ran before the session; the member's row is reused or materialized at the host's position, aligned, and no local-only set | machine | **pass** | Session B: the host's world ran 30 s solo before the lobby existed, and both members joined the running world. Both read `roster adopted: 81 rows (0 kept, 75 reused, 6 built, 6 surplus destroyed, 0 stale bindings dropped, 0 unresolved) — aligned=True.` (guest 21:18:05.435, alt 21:18:12.466): 75 copies reused inside the reuse radius, 6 materialized at the host's current position, 6 surplus copies destroyed (a member that kept a divergent local set could not read 6/6 like that). The 60 s repair repeats `81 rows (81 kept, 0 reused, 0 built, 0 surplus destroyed, …) — aligned=True` (21:18:44.886/.890). |
| 3 | A member that joins mid-run: the entry snapshot is aligned with the host's rows, sees the same enemies at the host's positions, and a killed/removed enemy disappears from all three views | machine | **pass** | Same entry as row 2 (both members `aligned=True`, 81 rows; the three tables read `bufferCount 81`). A generated shadecrawler was killed by a structural blast at its own position (`b-row3-kill-blast.json`: `healthBefore 100 → healthAfter -431.025`); the host logged `[Enemy] host dropped 1 destroyed enemy binding(s).` (21:19:44.609) and both members `[Enemy] guest removed enemy 8DF2F7584CA8F78:36:0 from kernel batch.` (21:19:44.655/.661). All three tables read `bufferCount 80`, and the three scenes read `total 80` with **no** copy left within 6 units of the blast point (`b-row3-scene-kill-*.json`, `matched 0`). |
| 4 | Runtime spawns: the host triggers a runtime animal after a member is in the world; the member logs `live-adopted` for it within one stream pass, both members see it at the host's position, and it follows the host's state stream | machine | **pass** | Session A: `/summon shadecrawler player` created one at (0.0,499.6) at 21:14:18.457; the host bound `runtime spawn 8DF2F74F6AE7431:375:0` at 21:14:18.527, both members logged `live-adopted 1 new enemy row(s) from the stream` at 21:14:18.866/.865 (guest's line names the local shadecrawler at (1.7,499.6)). The three scene reads then showed one shadecrawler at **(4.389,502.467), health 100 on all three clients**, and the three tables carried `runtime:true` with kernel fact `kRuntime:true` for the row; the 60 s repair sent both members `376 enemies, 1 runtime spawns` (21:14:32.630/.640). |
| 5 | Layer descent: all three descend; the new layer's rows adopt (`aligned=True`, zero unresolved), the old layer's bindings are dropped, and neither member keeps an old-layer enemy | machine | **pass** | Session A: the host ran the game's own `skiplayer` (21:15:43.3); it logged `[LayerReset] dropped the previous layer's 375 enemy row(s) and 248 fluid chunk(s)` (21:15:43.935), `host roster is ready over 77 enemies` (21:15:52.258) and `snapshot sent … 77 enemies, 0 runtime spawns, run 1 layer 1` (21:16:00.476). Both members logged `guest dropped its enemy bindings: the host reset the roster for a new layer` (21:15:44.17) and then `roster adopted: 77 rows (0 kept, 77 reused, 0 built, 0 surplus destroyed, 0 stale bindings dropped, 0 unresolved) — aligned=True` (guest 21:16:01.595, alt 21:16:00.880) — every new-layer copy reused through the scene scan after the reset cleared the tracked set. All three tables read `bufferCount 77` and all three scenes read `total 77` (67 shadecrawlers + 10 traders), with no old-layer id in either. |
| 6 | Regression: damage, kills, removals, attack announcements and bites still reach the bound copies; the host's screen carries no new warning; the member's 20 Hz drive still moves the bound copies | machine | **pass** | Damage: a non-lethal blast took the bound runtime shadecrawler from 100 to **99.444**, and the three tables AND the three scene copies then read 99.444 at the same position (1.36,510.389) (`a-row6-table-dmg-*`, `a-row6-scene-dmg-*`). Kills/removals: a second blast (`100 → -211.857`) removed it: host `dropped 1 destroyed enemy binding(s)` (21:15:22.708), both members `guest removed enemy …:375:0 from kernel batch` (21:15:22.967/.23.006); the three tables fell 376 → 375 and the copy is gone from the scene. Attack announcements and bites: the bound spider announced `SpiderBite #1/#2/#3` to 2 in-world guests (`[EnemyAttack] announced enemy …:375:0 SpiderBite #N`), both members received `[EnemyAttack] announced SpiderBite #N of enemy …:375:0 from <host>` and judged each on their own view (`spider bite … judged a miss — the local view shows no contact with this body`); the host's own bite reached both members' clone facts (`[EnemyBite] applied to <host>'s limb 0` at 21:14:18.534/.538). Drive: the bound copy moved with the stream between reads ((4.389,502.467) → (1.36,510.389) → (3.408,504.665)). Zero `[WRN] [Enemy]` lines on the host in either session, zero `unknown enemy` on the members. |

## Residuals for the user

None: every row is machine-judged from this run's own log/probe evidence.

## Limits

- No frames were captured; every row is machine-judged. Row 6's "host's screen carries no new warning" is
  the whole-window log grep, not a visual read.
- Session A's entry world was 375 rows, not batch `20261021-f`'s 956. The defect's shape is unchanged
  (the bad mechanism was the capture running during generation, which the batch's own `host enemy
  baseline established over 0 animals` proved), and this run's baseline line (`over 375`, at the settled
  edge) is the direct evidence that the gate holds; a dense world is still the harsher case, and the
  world count is recorded here rather than compared with a different session's settings.
- The batch's host and member logs for session A were overwritten when the clients restarted for session
  B (the second prelude starts them fresh); the evidence is the per-window extracts saved before the
  restart (`a-*-enemy.txt`, `a-row6-*.txt`) plus the probe answers, all cited above.
- The runtime spawn's creation key was not read directly on the members; the kernel fact's
  `kRuntime:true` in all three tables, the `live-adopted` lines and the repair's `1 runtime spawns` are
  the evidence.
- Row 6's bite: the host's own body was one of the spider's announced candidates and the members' clone
  facts applied the host's bite; no member body was within contact of the spider in this run, so the
  members' own local judgments read `miss`. The row asks that announcements and bites still reach the
  bound copies, not that a member be bitten.
- The descent row's member-side screen behaviour is where the user reported a separate problem — the
  member is pulled to the menu through the host's layer change and rejoins after it. That is the current
  design (`ReentryCoordinator`) and is filed as its own ticket (`todo/layer-change-menu-round-trip.md`),
  not part of this ticket's rows.
