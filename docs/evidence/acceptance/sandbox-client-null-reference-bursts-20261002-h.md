# Acceptance record — The sandboxed client's Item.Update NullReferenceException burst is CUO's

- Ticket: `sandbox-client-null-reference-bursts` — verdict: **stays in `todo/`** (the family is a CUO
  defect; the root cause and the fix direction are recorded on the ticket, the fix has not landed)
- Batch: `20261002-h` — tickets: `sandbox-client-null-reference-bursts`
- Commit: `fe56297482c8f4b57d1a564fa549b5e5d757dc30` (docs-only since the deployed build) · Deployed
  artifact: `CasualtiesUnknownOnline.dll`,
  `ProductVersion 0.1.0+9dd120fea21c4415ccc58f59d66d8bf684775364`; `tools/verify-deploy.ps1` exit 0
  ("Deployment matches this tree's build output"), two sandbox plugin dirs absent (no CUO shadow)
- Run: 2026-10-02, one session (host on the physical machine, guest and alternate in their sandboxes;
  three clients, one world) · Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports
  present · Entry gate: no `CasualtiesUnknown` process (`h-processes-before.txt`),
  `session-environment.ps1 -Mode status` = active=cuo, game-running=false (`h-session-env.log`)
- Windows staged with the verified member route (`leave-world` → `home.leave` → `join-lobby` → inWorld):
  window 1 = guest re-entry (the batch-20261002-d anchor), window 2 = alt re-entry (the batch-20261002-c
  anchor); every window was read for the DEDUPED diagnostic FIRST, the exception shape second, and an
  absence read covers the window plus a re-read
- Artifacts: `h-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Window 1 — guest re-entry: the burst and its culprit | machine | **pass (family reproduced)** | `h-w1-guest-brokenitem-final.log` (251 `[BrokenItemUpdate] … (world-null) in 'PreGen'`, 11:32:47.175–.255), `h-w1-guest-exception-mid.log` (the `Item.DMD<Item::Update>` stream), `h-w1-guest-lograte.txt` (42.26 MB/min during the storm) |
| 2 | Window 2 — alt re-entry: the same family on the other sandboxed client | machine | **pass (family reproduced)** | `h-w2-alt-brokenitem.log` (251 reports, 11:39:10.130–.179), `h-w2-alt-census-menu.json` (scene `PreGen`, `worldExists=false`, `worldComponents=0`, items=251, inactive=0, disabled=0, all parentless, every suspect `worldNull` with rb/affect/stats present, `condZero=9`, `breaksNow` false everywhere), `h-w2-alt-materializing.log` (the menu round's 251 materializing lines at 11:39:09.743–10.081), `h-w2-alt-snapshot-received.log` (`World-item snapshot received (251 items)`, ~10 s apart) |
| 3 | Peer control — the host in world logs neither line | machine | pass | `h-w1-host.log` / `h-w2-host.log` = 
O MATCHING LINES`; `h-w3-host-census-v2.json` (253 items at the census moment, 0 suspects) |
| 4 | Where the menu items come from (root cause) | machine + code | **pass (named)** | `h-materializing-rounds.txt` + `h-w2-alt-materializing.log` (first line 11:39:09.743) against `h-w2-alt-brokenitem.log` (first report 11:39:10.130) — 0.387 s apart; `ItemPositionAuthority.Update` → `ItemSnapshotService.SendPeriodicItemSnapshot` (a broadcast whose only gates are `Role == Host`, a non-empty table and `SessionActive`) |
| 5 | The instantiate-null half of the family | machine | **not reproduced** | `h-window-instantiate-counts.txt` (0 in both windows); no item in either menu census held `condition <= 0` AND `Stats.destroyAtZeroCondition` (`breaksNow` false for all 251) |

## What the run established

The burst is CUO's, and its cause is a contract gap on the host's side of the item stream:

1. A member that leaves the world keeps its session (it stays in the lobby; its scene becomes the game's
   `PreGen` menu). The host's `SceneStateHandler` ends the ENTITY sync for that member on the InMenu edge
   and has no item-side counterpart.
2. The host keeps broadcasting the world-item keyframe. `ItemPositionAuthority.Update` calls
   `ItemSnapshotService.SendPeriodicItemSnapshot` on its adaptive cadence (observed: 251 rows about every
   10 s; the extract's 20 receipts average 9.8 s), and that send's only gate is
   `Role == Host && SessionActive` — unlike every sibling absolute table, which rides
   `WorldEntryFanout.SendInSessionRepair` and is filtered per member by `member.InWorld`.
3. The out-of-world member applies those rows. Its world scene was destroyed by the scene load, so
   `RemoteItemSceneOps` finds neither an id hit nor a generation-time bind target and MATERIALIZES the
   whole table into the menu scene: the menu round of 251 `[ItemSpawn] materializing` lines at
   11:39:09.743–10.081 on the alt — that client's first report follows at 11:39:10.130, 0.387 s after the
   first materialization (`h-materializing-rounds.txt`,
   `h-w2-alt-materializing.log`, `h-w2-alt-brokenitem.log`).
4. Those items live where `WorldGeneration.world == null`; the game's `Item.Update` dereferences it on its
   first line and throws every frame for every item. CUO's deduped diagnostic
   (`ItemUpdateDiagnosticPatch` → `ItemWorldSync.OnBrokenItemUpdate`) reports each object once — a
   `HashSet<Item>` (`_brokenUpdateReports`) — which is why the 251 reports do not grow with the 505
   materializations each client performed.
5. On re-entry the scene load destroys the menu copies and the NRE stream stops: the guest produced zero
   `Item.DMD` and zero `Unity:Exception` lines after its 11:35:39.207 re-entry. The keyframe does not fire
   while the client is in the menu only, though: each client also materialized a 251-line round at its
   re-entry, before its world was ready (guest 11:35:28.0; alt 11:41:00.839–01.34, between its
   11:41:00.837 keyframe receipt and its 11:41:11.923 re-entry). That round is the related finding below,
   not a second menu storm.

The materialization rounds measured per client (`h-materializing-rounds.txt`; full line extracts in
`h-w1-guest-materializing.log` and `h-w2-alt-materializing.log`, 505 lines each):

- guest: 3 at the initial entry (11:32:09–11), 251 in the menu after leave-world (11:32:46–47), 251 at
  the re-entry (11:35:28);
- alt: 3 at the initial entry (11:32:08–11), 251 in the menu after leave-world (11:39:09–10), 251 at the
  re-entry (11:41:00–01).

Size of the burst: 831,352 `Item.DMD<Item::Update>` frame lines since the window-1 mark (byte-slice count
of the guest's rolling log; each line heads a two-line exception block, so the window holds about 1.66 M
lines). Rolling logs at close (`h-close-checks.txt`): host 3,763,482 B, guest 144,300,129 B (0.86 MB at
the window mark → 144.30 MB), alt 80,812,081 B (80.81 MB) — decimal units throughout.

## The sibling sweep (why the fix is host-side targeting)

- Entity sync ends on the member's InMenu edge (`ctx.Entities.EndMemberSync`) and the periodic absolute
  tables (`checkpoint`, run facts, block state/damage, trap layout, enemy snapshot, runtime entities,
  recipe unlocks, roster) are sent only to `member.InWorld`.
- The two item streams are the outliers: `SendPeriodicItemSnapshot` (above) and `SendItemMove`
  (`ItemService.SendItemMove` → `SendStateStream`, `Role/Active` gate only) are session broadcasts.
- The fix direction: target the item keyframe (and sweep the move stream) per member, InWorld only; and
  gate the apply side on a live world as defense in depth.

## Related finding — duplicate world items after a fast re-entry (not the burst family)

- `h-w3-guest-dupcheck.json`: after re-entry the guest held 505 items (252 carrying a CUO
  `ItemInstanceId`, 253 without) against the host's 254 (`h-w3-host-dupcheck.json`: 251 + 3); zero
  duplicate ids on either side. The same state has two readings: the census probe read 504 items
  (`h-w1-guest-census-world-v2.json`) and the host census probe read 253 (`h-w3-host-census-v2.json`) at
  their own moments.
- Reading: the keyframe's re-entry round lands while the re-entering member's own generation is still
  producing items — `FindExistingAt` has no bind target yet, so copies carrying the host's item ids are
  materialized, and the generation then adds its own id-less items beside them. Same root shape (rows
  applied before the receiver's world baseline is ready), different symptom. The fix cycle must either
  cover it (gate on the generation baseline) or split it; it is not silently carried.

## Limits

- One session, two windows; the keyframe cadence is adaptive (~10 s here), so a storm's start aligns to
  the next keyframe after the leave and another session may see a different delay.
- The menu NRE stream stops on re-entry, but materializations also happen at entry and at re-entry; only
  the menu round produces the worldless objects, and only its objects are reported (per-object dedupe).
- The instantiate-null half did not reproduce: neither window held an item with `condition <= 0` AND
  `Stats.destroyAtZeroCondition`, so the break branch (`Resources.Load("ItemBreakParticle")` → the
  `ArgumentException`) was never entered. Batches `20261002-c`/`-d` remain its only direct evidence.
- The `GroundBlood.Start` native frame from batch `20261002-c` was not staged here; both windows produced
  the `Item.DMD<Item::Update>` shape (batch `20261002-d`'s anchor).
- The counts are byte-slice measurements of the rolling log, not a frame-exact exception census; the
  census rows are point-in-time snapshots, and the post-re-entry item counts differ between probes
  (505/504 guest, 254/253 host).
- The duplicate finding's mechanics (which row materialized before which generation item) is a
  next-cycle measurement.
