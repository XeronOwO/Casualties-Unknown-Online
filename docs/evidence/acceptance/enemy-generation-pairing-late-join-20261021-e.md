# Acceptance record — Enemy generation sync: the host's roster is the identity

- Ticket: `enemy-generation-pairing-late-join` — verdict: **back to `todo/` (row 4 fails)**. Status field:
  `- Status: Todo — Rejected (batch 20261021-e: row 4 — no member live-adopts a runtime spawn; the row is
  adopted only by a later snapshot, and the second member never received it at all)`.
- Batch: `20261021-e` — this ticket. Two sessions in one batch: A (`entry`, all three present at world
  entry) and B (`drift`, the host's world runs before the session; both members join mid-run).
- Commit: `44ce057f` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+44ce057fe716560ed8579b389223b7c7a00f5caf` (`prelude-a-verify-deploy.txt`,
  `prelude-b-verify-deploy.txt`; `tools/verify-deploy.ps1` exit 0).
- Run: 2026-10-21 19:18 → 19:36 local · Host: physical machine · Guests: sandbox `<sandbox-guest>` and
  `<sandbox-alt>`.
- Dependencies: the eleven the preflight reported present (`RESULT: OK - a full two-client run is possible;
  the alternate third client is configured`; `preflight-enemy.txt`).
- Artifacts: `20261021-e/` under the directory named by `acceptance-artifacts-dir` — `prelude-a.log`,
  `prelude-b.log`, `session.json`, the prelude reads, the per-row tables (`a-row*`, `b-row*`, `a-row5-*`),
  the batch-local probes (`a-row4-*-scene.json`, `a-row6-*`, `b-row3-*`), the log windows
  (`a-row1-*-enemy.txt`, `a-row4-*-log.txt`, `a-row5-*.txt`, `a-row6-*.txt`, `b-row2-*-adopt.txt`,
  `b-row3-kill-*-log.txt`) and the damage/kill answers.

## The run

Session A: host + both members were in the lobby before the host started the run (`prelude-a.log`:
`lobby=109775241709179333`; the run starts at 19:19:09; all three `inWorld` at 19:19:13). The host bound
78 generation animals at 19:19:19.444, `[GenStream] done — 19 segments` at 19:19:19.585, and both entry
snapshots were sent (`snapshot sent to <member>: 78 enemies, 0 runtime spawns, run 1 layer 0`).

Session B: the host started a run alone at 19:27:40, its world ran until 19:28:24 (30 s solo; `prelude-b.log`
`host body ready` 19:27:54 → `host solo-world run window closed` 19:28:24), it created the lobby from inside
that running world at 19:28:45, and both members joined mid-run (`b-row2-*-adopt.txt`; the host's first bind
happened at the lobby's session start, after the world had run).

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Both members present at entry read `roster adopted: N rows (… aligned=True)`, N = the host's row count, zero unresolved and zero surplus, on the entry snapshot; no pairing-failure line | machine | **pass** | Guest 19:19:20.120 `[Enemy] roster adopted: 78 rows (0 kept, 78 reused, 0 built, 0 surplus destroyed, 0 stale bindings dropped, 0 unresolved) — aligned=True.`; alt 19:19:20.120 the same line; a second adoption after the host's boundary event at 19:19:21.075 is `78 rows (78 kept, 0 reused, 0 built, 0 surplus destroyed, 0 unresolved) — aligned=True.` The host's roster is 78 (`a-row1-host-enemies.json` `count=78`, and the entry send says `78 enemies`). Both members' tables read 78 rows, 0 runtime spawns. Whole-log greps for `pairing failed`: **0** on all three. |
| 2 | The drift shape: the host's world ran before the session; the member's row is reused or materialized at the host's position, aligned, and no local-only set | machine | **pass** | The host's world ran 30 s solo before the lobby existed (`prelude-b.log`: `19:27:54 host solo world settled; …` → `19:28:24 host solo-world run window closed` → `19:28:45 lobby=… (created from inside the running world)`). On entry the member adopted 76 rows: `[Enemy] roster adopted: 76 rows (0 kept, 72 reused, 4 built, 4 surplus destroyed, 0 stale bindings dropped, 0 unresolved) — aligned=True.` (guest 19:28:58.422; alt 19:29:04.489 the same). The 4 `materialized roster enemy … at (…)` lines are rows whose host hint had no same-prefab copy inside the 3-unit reuse radius; the 4 `destroyed surplus local enemy …` lines are the member's copies the roster does not contain. All three tables then hold the same 76 ids. |
| 3 | A member that joins mid-run after the host's animals moved: entry snapshot aligned with the host's rows, sees the same enemies at the host's positions, and a killed/removed enemy disappears from all three views | machine | **pass** | Both members joined a world that had already run (`b-row2-*-adopt.txt`, 76 rows, `aligned=True`, 0 unresolved). The three tables hold the same 76 ids; 74 of 76 positions match exactly and the two moving shadecrawlers differ by 3.61 and 6.26 units between the sequential reads (`b-row3-*-enemies.json`). A generated shadecrawler (id `…:41:0`) was killed by a structural blast at its own position (`b-row3-kill-blast.json`: `healthBefore 100 → healthAfter -654.347`); the host logs `[Enemy] host dropped 1 destroyed enemy binding(s).` and both members `[Enemy] guest removed enemy 8DF2F664D0B5836:41:0 from kernel batch.` The id is absent from all three tables afterwards. |
| 4 | Runtime spawns: the host triggers a runtime animal after a member is in the world; the member logs `live-adopted` for it within one stream pass, both members see it at the host's position, and it follows the host's state stream | machine | **fail** | Three runtime spawns, no `live-adopted` line anywhere (whole-log grep: **0** on host, guest and alt). Session A, first cavetick (`…:78:0`): host summoned at 19:20:16.997 and bound it at 19:20:17.048; both members created their copy at 19:20:17.030 but logged `[WRN] [Enemy] attack SpiderBite #1/#2/#3 arrived for unknown enemy 8DF2F650248BED1:78:0 — the snapshot binding may not have arrived yet; the attack is dropped.`; the row was adopted only by the 60 s repair snapshot at 19:20:21.035 (`79 rows (78 kept, 1 reused, …) — aligned=True`). The second cavetick (`…:79:0`) repeated it (bound 19:22:58.567; attacks dropped; adopted by the snapshot at 19:23:23.091). Session B, third cavetick (`…:76:0`): host bound it at 19:29:58.540; the alt adopted it only by an entry repair at 19:30:03.687 (`77 enemies, 1 runtime spawns`), and the guest **never received it** — its table stayed at 76 rows after the summon and 75 after the kill, while the host and alt held the runtime row (`b-row3-kill-*-enemies.json`). The member copies carry the creation key (`[EntitySpawn] created cavetick … (creation …)`), so the snapshot's creation-key pass reuses them, but the live pass never binds one. |
| 5 | Layer descent: all three descend; the new layer's rows adopt (aligned, zero unresolved), the old layer's bindings are dropped, neither member keeps an old-layer enemy | machine | **pass** | Host `skiplayer` at 19:26:11; the host logged `[Enemy] host roster is unstable (transition boundary) — snapshots are held until the new layer's capture is complete.`, `[LayerReset] dropped the previous layer's 71 enemy row(s)`, `[Enemy] host roster is ready over 71 enemies`, and `snapshot sent … 71 enemies, 0 runtime spawns, run 1 layer 1` (19:26:29). Both members logged `[Enemy] guest dropped its enemy bindings: the host reset the roster for a new layer.` and adopted `71 rows (0 kept, 67 reused, 4 built, 6 surplus destroyed, 0 stale bindings dropped, 0 unresolved) — aligned=True.` All three stayed `inWorld=true` through T+45 s, and all three tables hold 71 rows with counters 80–150 and **0** ids from the old layer. |
| 6 | Regression: damage, kills, removals, attack announcements and bites reach the bound copies; the host's screen carries no new warning; the member's 20 Hz drive still moves the bound copies | machine | **pass** (warning note below) | Damage: a structural blast took cavetick 78 from 15 to 5.883 health on the host (`a-row6-host-blast.json`); the host, guest and alt tables then all read `hp=5.883`, and the members' scene copies read `hp=5.883` (`a-row6-dmg-*-scene.json`). Kills/removals: both caveticks died and all three tables dropped from 80 to 78 rows with no cavetick left; a generated shadecrawler's removal is row 3. Attack announcements and bites: the host announced `SpiderBite #1…#13` and `[Enemy] host spider … bite action announced — the host's nearest bite candidate is its own body`; both members received `[EnemyAttack] announced SpiderBite #N …` and logged `[Enemy] spider bite … judged a hit on local limb N` / `… judged a miss`; the clones applied `[EnemyBite] applied to <host>'s limb N`. Drive: the guest's scene copies moved under the stream (3 animals over 4 s; the largest 12.9 units), and 76 of 78 scene copies match a table row within 0.5 units (`a-row6-drive-*`). The host's window since the damage mark carries two `[WRN] [Vitals] … divergence: limb N bleed model …` lines (the bleed reconcile after the bites) and **no `[Enemy]` warning**; the `unknown enemy` warnings are on the members, not the host. The blast's own near-body branch also ran: the host's body was 3.045 units from a range-1.5 blast (inside the game's 2.5 × range non-damage effect — consciousness 31, hearing loss, eye close/scare), and the scene read right after it shows the host `alive=true, conscious=true`. |

## The row-4 finding

The live pass cannot bind a runtime spawn that a snapshot has not yet introduced:

- `EnemyRosterBinder.TryAdoptNewRows` reads the prefab from the kernel fact
  (`EnemySyncService.TryGetEnemyPrefab`) but passes `allowKeyedCopy: state.RuntimeSpawned` from the
  **stream buffer row**.
- The 20 Hz wire row does not carry that flag: `EnemyStreamWireMapper.ToWireEnemyStreamState` sends position,
  velocity, rotation, health, flags and the presentation lists only, and its `ApplyTo` leaves
  `PrefabId`/`RuntimeSpawned`/`CreationKey` at their defaults for a NEW id (the merge preserves them only for
  an id the buffer already holds).
- The member's runtime copy is keyed (`RuntimeEntityCreation`; the snapshot's creation-key pass is what
  later pairs it), and `EnemyRosterAdoption.FindLiveCopy` refuses a keyed copy when `allowKeyedCopy` is
  false. The kernel fact does carry `RuntimeSpawned: true` (`a-row4-guest-kernel.json`), but the live pass
  never reads that flag.
- The second, independent gap: in session B the guest received no in-session repair group after
  `19:29:52.637` (the host's last `Sending the in-session repair group` for the guest; the alt got one at
  19:30:03 and adopted the runtime row, the guest did not), so the row that the live pass missed never
  reached the guest at all in the rest of the session.

## Residuals for the user

None: every row was judged from the run's own log/table/probe evidence.

## Limits

- No frames were captured; every row is machine-judged. Row 3's "sees the same enemies at the host's
  positions" is judged from the three tables and the members' scene copies, not from a window frame.
- One repetition per scenario (two runtime spawns in session A, one in session B); the session A second
  summon repeated the same shape.
- The drift's per-animal displacement before the host's first bind was not measured directly: the shape
  was staged (30 s solo world before the lobby) and the entry adoption's 4 materialized rows / 4 surplus
  copies show the hint/copy divergence exceeded the 3-unit reuse radius; a direct per-animal pre-bind
  distance would need a scene snapshot at generation end, which this run did not take.
- The guest's rolling log rotated during session B under Debug-level volume (255k lines at the close); the
  session-B guest log windows therefore come from the current `latest.log`, and the table/probe readings are
  the primary evidence for that session.
- The damage probe's structural damage is scaled by the game (`Random.Range(0, 2)`): 5 requested lost 9.117,
  500 requested lost 754.347; the health change is the evidence, not the parameter.
- The damage blast also ran the game's non-damage near-body branch on the host (body 3.045 units from a
  range-1.5 blast, inside 2.5 × range): the game writes consciousness 31, hearing loss and eye close/scare; the
  run read only `alive/conscious` (both true) afterwards, not those values, so a visual row judged in that
  frame would need the observer's state named first.
- The host's two `[Vitals]` bleed-divergence warnings are named above; they are the bleed reconcile after
  the cavetick bites, not the enemy path. A strict "no warning at all" reading of row 6 would fail on them.
- Session A's entry repair sent the enemy snapshot every 10 s while the entry window was open and then at
  the 60 s cycle; session B's guest received none after 19:29:52. The scheduling difference is part of the
  row-4 finding, not a separate judged claim.
