# Acceptance record — Enemy snapshot binding has no recovery path (batch 20261003-c)

- Ticket: `enemy-snapshot-binding-recovery` — verdict: **all rows resolved; moved to `done/`** (row 2
  passes in this batch; rows 1, 5 and 6 stand from batch `20261002-k`, rows 3, 4 and 8 from batch
  `20261002-l`, row 7 lives in `review/enemy-hit-determination-local.md`)
- Batch: `20261003-c` — tickets `enemy-snapshot-binding-recovery`
- Commit under acceptance: `7cfe910ed8fe30cc4dc4ffac8119622c972314f4` — the commit that staged this batch's
  probes (`enemy-table-read`, `scene-repeat`). Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+7cfe910ed8fe30cc4dc4ffac8119622c972314f4`, re-verified after the run (exit 0).
- Run: 2026-10-03, 04:59–05:03 +08:00 · Host: physical machine · Guest: sandbox `Steam1`
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (`dotnet`, `game`,
  `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`, `logs`, `artifacts`)
- Artifacts: `c0-*`, `c1-*`, `c2-*` and `c3-*` in the directory named by `acceptance-artifacts-dir` under
  `20261003-c/`; this record cites artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 2 | An empty host enemy table sends nothing | machine | **pass** | Setup: both ends coupled on layer 1, 78 enemies each (`c0-enemies-host.json`, `c0-enemies-guest.json`). **Control (non-empty)**: the member's own readiness-window repeat report (`c1-repeat-guest.json`) produced the host's `Sending the in-session repair group …` at 05:00:19.042 + `[Enemy] snapshot sent to …: 78 enemies, 0 runtime spawns` at 05:00:19.077 + `Repeat scene report … (repair 1 of this entry)` at 05:00:19.082 (`c1-host-repair.log`), the member's `Scene state re-reported: InWorld` at 05:00:19.023 + `[Enemy] snapshot applied: 78 generated bound, 0 runtime spawns, mapping=True` at 05:00:19.264 (`c1-guest-apply.log`), and one real `EnemySnapshot` frame decoded off the member's own receive seam (8846 bytes — `c1-wire-read.json`). **The window (empty)**: the member's inbound blackout was armed (`c3-blackout-on.json`: `armed=true`, `subscribersAfter=0`), the host ran `game-console command=skiplayer,args=none` (applied — `c3-skiplayer-host.json`), and the host's table fell to 0 (78 → 0 → 7 → 78 over the descent — `c3-window-timeline.txt`, `c2-k1-timeline.txt`); there, the member's repeat report (`c3-repeat-guest.json`) produced the host's `Sending the in-session repair group to …` at 05:02:14.444 and `Repeat scene report …` at 05:02:14.445 with **no `[Enemy] snapshot sent` line in the window** (`c3-host-window.log`; the previous one is the member's 05:01:44.747 entry send, and the next window pass is the post-generation 60 s repair) — the product's own empty-table branch. The host's table still read 0 right after the send (`c3-host-table-after.json`) and the member's own table read 78 unchanged across it (`c3-guest-table-after.json`), with no `[Enemy] snapshot applied` on the member (`c3-guest-window.log`). The blackout was lifted after 5.7 s (`c3-blackout-off.json`), and the window closed into a repopulated table — host layer 2, 60 enemies (`c3-generation-host-after.json`, `c3-host-table-postwindow.json`) — so the empty moment was the generation, not a dead send path |

## Residuals for the user

None — the row is a `machine` row and its verdict is read from the product's own log, tables and live
probe results this run produced against the deployed artifact.

## Limits

- Rows 1, 5 and 6 stand from batch `20261002-k`, rows 3, 4 and 8 from batch `20261002-l`; they were not
  re-run. Row 7 is carried by `review/enemy-hit-determination-local.md` by the ticket's own design.
- **The carrier staged is the member's own readiness-window repeat report**
  (`ISessionControl.ResendSceneState` → the host's `SceneStateHandler` repeat branch → its entry-repair
  claim), which calls the same `WorldEntryFanout` group and the same `SendEnemySnapshot` as the 60 s pump
  and the entry fan-out. The pump's own timer, in-world filter and per-member enumeration remain the
  ticket's recorded untested limit; the row's claim (an empty table sends nothing) is judged on the send
  path both carriers share.
- **The entry-edge shape stays unstageable**: a member's InWorld edge fires only after its own generation
  finishes, and on this machine the host finishes generating first — "a member entering while the host's
  table is still empty" cannot be produced end to end (the same finding batch `20261002-l` recorded, now
  with the repair carrier as the substitute).
- **The member's unchanged set is a within-window reading**: the blackout suppresses every inbound frame,
  so the reading shows the member held its own set while the empty-table send happened; after the blackout
  was lifted the host's layer-2 kernel removals reached it (`[Enemy] guest removed enemy … from kernel
  batch`, `c3-guest-excerpt.log`) — the product's own layer-transition lifecycle, not an empty snapshot.
- The descent is the game's own debug console entry (`game-console command=skiplayer`), named as the
  setup; the blackout is a blunt instrument held 5.7 s, well inside the 15 s host-silence watchdog.
- One staging attempt was refused before it reached the client: the `game-console` recipe declares two
  arguments (`command`, `args`) and only `command` was supplied, so no answer file was written and the
  host stayed on layer 0 (`c2-generation-host.json`). The corrected call is the one used; the finding is
  folded into `docs/acceptance/lessons.md`.
- One sample per state, one session, one artifact; the control and the row ran against the same build.
