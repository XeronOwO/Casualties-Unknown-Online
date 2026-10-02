# Acceptance record — Re-entry landing convergence (batch `20261002-j`)

- Ticket: `sandbox-client-null-reference-bursts` — verdict: **to `done/`** (status field:
  `- Status: Done — accepted by batch 20261002-j`). Every row passes against the deployed artifact.
- Batch: `20261002-j` — tickets: `sandbox-client-null-reference-bursts`
- Commit: `cfbf76d11f98f8214ddebdfb91fe4f28a8ebaf2a` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+cfbf76d11f98f8214ddebdfb91fe4f28a8ebaf2a`; `tools/verify-deploy.ps1` exit 0
  ("Deployment matches this tree's build output"); both sandbox plugin dirs absent (no CUO shadow)
- Run: 2026-10-02, one session (host on the physical machine, guest and alternate in their sandboxes;
  three clients, one world) · Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports
  present · Entry gate: no `CasualtiesUnknown` process, `session-environment.ps1 -Mode status`
  = `active=cuo`, `game-running=false`
- Windows staged with the verified member route (`leave-world` → menu dwell across at least two
  keyframe cycles → `home.leave` → `join-lobby` → inWorld), every window read for the DEDUPED
  diagnostic FIRST, and an absence read re-read once after a bounded pause
- Artifacts: `j4-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact
  ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | menu dwell: the out-of-world client receives no item rows (no `World-item snapshot received`, no `[ItemSpawn] materializing`) and the host targets the in-world member only | machine | pass | `j4-w1-guest-menureads.log` / `j4-w2-alt-menureads.log` = `NO MATCHING LINES` over the menu mark; `j4-w1-menu-probe.json` + `j4-w1-menu-probe-2.json` / `j4-w2-menu-probe.json` + `j4-w2-menu-probe-2.json`: scene `PreGen`, `hasWorld=false`, `total=0` at both ends of the dwell; `j4-w1-host-itemkeyframe-menu.log` / `j4-w2-host-itemkeyframe-menu.log` = `[ItemKeyframe] sent 286 item(s) to 1 in-world member(s)` |
| 2 | the whole window (leave + dwell + re-entry) logs no `[BrokenItemUpdate]` and no `Item.DMD<Item::Update>` / `Unity:Exception` burst | machine | pass | `j4-w1-guest-brokenitem.log` + `j4-w1-guest-brokenitem-reread.log` and `j4-w2-alt-brokenitem.log` + `j4-w2-alt-brokenitem-reread.log` = `NO MATCHING LINES`; the re-read confirms over the full window |
| 3 | in-world peer control: the keyframe keeps arriving | machine | pass | `j4-w1-alt-snapshot-menu.log` = 4 receipts 15:41:06–15:41:36 (~10.0 s); `j4-w2-guest-snapshot-menu.log` = 4 receipts 15:42:40–15:43:04 (~8.1 s) |
| 4 | host and the third peer clean in the window | machine | pass | `j4-w1-host-brokenitem.log` / `j4-w1-alt-brokenitem.log` and `j4-w2-host-brokenitem.log` / `j4-w2-guest-brokenitem.log` = `NO MATCHING LINES`; the `-reread` pair confirms over the full windows |
| 5 | re-entry converges: no id-less world item beside a materialized copy, census at the host's | machine | pass | `j4-w1-settled-probe-guest.json`: `total=289 withId=287 noId=2 noIdWithComponent=0`; `j4-w1-settled-dupcheck-guest.json` 289 (287+2) against `j4-w1-settled-dupcheck-host.json` 289 (286+3), `dupIds=0`, `extraCopies=0`; the alternate mirror `j4-w2-settled-probe-alt.json` / `j4-w2-settled-dupcheck-alt.json` against `j4-w2-settled-dupcheck-host.json`; the window's own story: `j4-w1-window-guest.log` = 283 `[ItemBind]` + 3 `[ItemSpawn] materializing` + `[Reconcile] 286 items: killed 0, spawned 0, not taken 0, dropped 3 late id-less world item(s)`, `j4-w2-window-alt.log` the same shape |
| 6 | initial entry not regressed: the generation publish still binds and no duplicate-of-entry class appears | machine | pass | `j4-entry-genitems-host.log` = `[GenItems] host published 286 ground items (modifier 3)`; `j4-entry-genitems-guest.log` = `applied 286 entries: 0 bound, 286 materialized`; `j4-entry-genitems-alt.log` = `applied 286 entries: 281 bound, 5 materialized`; `j4-entry-probe-*.json` and `j4-entry-dupcheck-*.json` = 289 items on every side, `dupIds=0`, `noIdWithComponent=0` |
| 7 | host sender log names the target set | machine | pass | `j4-w1-host-itemkeyframe-menu.log` / `j4-w2-host-itemkeyframe-menu.log` = `sent 286 item(s) to 1 in-world member(s)` for every cycle of both dwells (the dwelling client has left the world; the other guest is the one in-world member) |

## What the run established

- The batch-`20261002-i` residual (row 5) is closed against the deployed artifact: both re-entering
  clients settle at the host's census, their `noId` count is the two carried items only, the
  `ItemInstanceId` component count on id-less objects is zero, and no instance id appears twice on
  either side.
- The window's own story is the sweep-only landing this batch ships: the world-entry repair binds 283
  rows, the three rows whose local object did not match are materialized, and the keyframe's
  late-local sweep drops the late id-less locals (`dropped 3 late id-less world item(s)`), so nothing
  strands beside a materialized copy.
- The host targeting holds at the sender in both dwells (`to 1 in-world member(s)`), and both
  sandboxed clients kept zero `[BrokenItemUpdate]` / `Item.DMD<Item::Update>` / `Unity::Exception`
  lines in the dwell and over the whole window.
- The generation publish is not regressed: the host published 286 items and both guests applied 286;
  the alternate bound 281 of them (its local generation objects had registered by the publish) and the
  guest materialized its 286 — the same split batch `20261002-i` measured as `0 bound`. All three
  sides sit at 289 items with zero duplicate ids.

## The decision that shaped this batch

- The deferred-landing queue an earlier revision of this batch added (`DeferredWorldItemLanding` +
  `PendingWorldItemRows`) was removed before this acceptance (commit `cfbf76d1`). Its staging probe —
  the re-entering client pinned to 5 fps — deferred the same seven rows at entry and at the re-entry
  and every `[ItemSpawn] deferred rows:` summary read `adopted 0 late`: the seven late local objects
  sat 1.8–3.6 units from the authority row, outside the 1.5-unit adopt tolerance `FindExistingAt`
  shares, so the retry could never adopt them and the queue only delayed materialization by 2 s. The
  sweep-only landing converges the same window (this run's row 5).

## Residuals for the user

None — every row is machine-judged.

## Limits

- One session, two windows; the keyframe cadence is adaptive (8–10 s here), so the dwell is measured
  in the peer's receipts, not on a wall-clock constant.
- The probes are point-in-time snapshots; totals can drift by one or two objects between moments.
- The count vocabulary changed with this batch: `materialized` no longer includes deferred rows (there
  is no deferral any more), and the keyframe reconcile's summary reads `not taken` where it read
  `deferred`; cross-batch comparisons of those counters must account for it.
- The instantiate-null half did not reproduce (no `breaksNow` item); batches `20261002-c`/`-d` remain
  its only direct evidence.
- The receiver guards and the late-local sweep have no automated coverage (the GameAdapter's Unity
  dependency keeps the scene half out of the suite); this staging run is their runtime evidence.
- The seven-row adopt failure is measured, not root-fixed: those local objects are dropped by the
  sweep. The decision evidence bounds itself to those seven rows.
