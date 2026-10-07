# Acceptance record — Guest command loss: local pickup/drop result is not reconciled

- Ticket: `guest-command-loss-reconciliation` — verdict: moved to `done/`
- Batch: 20261005-b — this is the batch's only ticket
- Commit: `53d577e0` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+53d577e084465ea1a416e1073eb6e8ba03efba36`
- Run: 2026-10-05 19:49 → 20:09 · Host: physical machine · Guest: sandbox `Steam1` · Third client:
  sandbox `Steam2`
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`,
  `artifacts` (preflight: 11 present, exit 0)
- Artifacts: `b2-*` (session setup), `r1-*`, `r2-*`, `r3-*`, `r4*`, `r5-*`, `r6-*`, `r7-*`, `r8-*`,
  `r9-*` (probe JSON, log excerpts and the run's local probes) in the directory named by
  `acceptance-artifacts-dir`

Sessions: the batch ran **two client sessions of one artifact**. The first (19:49 → 20:00) carried rows
2, 6 and 7 plus row 4's first staging attempt; that attempt's layer change left the two members out of
the world with a repeating `[LayerMod] baseline divergence` warning storm, so the run restarted all
three clients cold (20:01) and re-judged rows 1, 3, 5, 8, 9 and 4 in the second session. Every row's
evidence below names the session it came from.

Swallow injection: `net-receive-blackout` parks the receiving client's inbound dispatch, which is the
production lazy-P2P swallow (the sender's transport still reports the send as successful). Rows 1, 2,
3, 4, 8 and 9 arm it on the HOST, so the member's outgoing report is the frame that dies; row 5 arms it
on the GUEST, so the host's committed-batch receipt is the frame that dies. One product instance on all
three clients throughout.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest pickup command dropped | machine | pass | `r1-guest-drop.json` (native drop of `5197989525` from slot 3) then `r1-blackout-on.json` / `r1-blackout-off.json` (host inbound parked 20:02:56.29 → 20:02:59.26) and `r1-guest-pickup.json` (the native pickup into slot 0 inside that window); guest log `r1-guest-log.txt`: `re-reported ItemPickup on item 5197989525 to the host (1/12)` at 20:03:02.682 then `converged after 1 re-report(s) — the host committed operation 3801889743155757697` at 20:03:02.788. Host `r1-host-tables-after.json`: `worldCount` back to 276, the id is out of the world table and the transfer table names the guest (`5197989525`, `isTransferred: true`) |
| 2 | Guest drop command dropped (the row this fix is about) | machine | pass | `r2-blackout-status.json` (`armed=false` before), `r2-blackout-on.json` (host inbound parked from 19:52:06.84), `r2-guest-drop.json` (native drop of `5197989525` from slot 3 at 19:52:10.76) — placed 4.0 s before the host's own periodic checkpoint, which the host log `r2-host-log.txt` dates at 19:52:14.790 (`Sent kernel checkpoint at revision 672`). Guest log `r2-guest-log.txt`: `[ItemCommand] kept 1 unacknowledged item report(s) across the world baseline restored at revision 672 (run 1) — the baseline mirrors the host's state and does not supersede a report travelling the other way.` at 19:52:14.894, immediately before `Restored kernel checkpoint at revision 672 (254 items, run 1)`; then `re-reported ItemDrop on item 5197989525 (1/12)` at 19:52:18.843 (still inside the second blackout) and `(2/12)` at 19:52:23.848 → `ItemDrop on item 5197989525 converged after 2 re-report(s) — the host committed operation 3801889743155758268` at 19:52:23.900. The host's first sight of the report is the re-report: `r2-host-log.txt` `[ItemDrop] emergencylight (id 5197989525) not present — requesting materialization at (-1.5,490.1)` at 19:52:23.896. `r2-host-tables-before.json` (transfer `5197989525` with the guest as carrier, `worldCount` 252) → `r2-host-tables-after.json` (`tableCount` 0, transfer cleared, `worldCount` 253, the item back in the world table); `r2-guest-local-after.json`: `localCount` 0 |
| 3 | Destroy command dropped while the host table is non-empty | machine | pass | `r3-blackout-on.json` / `r3-blackout-off.json` (host inbound parked 20:03:22.69 → 20:03:25.62), `r3-guest-destroy.json` (a runtime world item `1181436987331`); guest log `r3-guest-log.txt`: `re-reported ItemDestroy on item 1181436987331 (1/12)` at 20:03:29.055 → `converged after 1 re-report(s) — the host committed operation 3801889743155758024`; host `r3-host-tables-after.json`: `worldCount` 276 → 275 and the id is gone |
| 4 | Destroy command dropped while the host table is empty | machine | pass | Setup `r4-guest-provide.json`: the guest creates and picks up `13787924117` (bandage), and `r4c-host-tables-recovery.json` shows the host holding it in its transfer table. Attempt `r4d-*`: `r4d-host-blackout-on.json` (host inbound parked from 20:07:49.51) **and** `r4d-guest-blackout-on.json` (guest inbound parked 20:07:52.13 → 20:08:01.76, so the member keeps its body and its carried item through the layer transition), `r4d-skiplayer.json` at 20:07:55.35 → host log `r4d-host-log.txt` `[LayerReset] dropped the previous layer's world-rooted items; 3 item record(s) remain (carried items cross the boundary).` at 20:07:56.339, and the destroy probe `r4-destroy-carried.cs` answering `{"ok":true,"id":"13787924117","type":"bandage","carried":true,...}` at 20:07:57.4 while the host's inbound was still parked. `r4d-host-tables-t1.json` at 20:08:05.9: `worldCount` 0 and an empty transfer table — the keyframe had no world item to carry. Guest log `r4d-guest-log.txt`: `re-reported ItemDestroy on item 13787924117 to the host (1/12)` at 20:08:04.603 → `ItemDestroy on item 13787924117 converged after 1 re-report(s) — the host committed operation 3801889743155757412` at 20:08:05.499; host traffic line: `Receive ItemDestroyCommand=52B/1f`. Final state `r4d-host-tables-after.json`: `tableCount` 0 (the transfer entry for the guest's last carried item went away), the id is not in the world table, `r4d-guest-local-after.json`: `localCount` 0 |
| 5 | Duplicate/replayed re-report | machine | pass | `r5-guest-blackout-on.json` (the GUEST's inbound parked from 20:05:01.55), `r5-guest-drop.json` (drop of `9492956821` at 20:05:03.1), `r5-guest-blackout-off.json` (20:05:09.69) — the committed-batch receipt dies. Guest log `r5-guest-log.txt`: `re-reported ItemDrop on item 9492956821 (1/12)` at 20:05:07.996, `kept 1 unacknowledged item report(s) across the world baseline restored at revision 945` at 20:05:11.113, `(2/12)` at 20:05:12.997 → `converged after 2 re-report(s) — the host committed operation 3801889743155759261` at 20:05:13.040. Host log `r5-host-log.txt`: the first report at 20:05:03.021 is `[ItemDrop] soup (id 9492956821) not present — requesting materialization at (4.8,494.0)` (one materialization, one operation) and both repeats — 20:05:08.013 and 20:05:13.026 — are answered as `present — re-placing at (4.8,494.0)`; host traffic: `Receive ItemDropCommand=99B/1f` then `198B/2f`. `r5-host-tables-after.json`: `worldCount` 277, the item in the world table, transfer table empty |
| 6 | Reconnect | machine | pass | `marks-6.txt`; the guest picks `5197989525` back up (`r6-guest-pickup.json`, `r6-host-tables-before.json` names it as carried), then `r6-blackout-on.json` (host inbound parked 19:54:43.39), `r6-guest-drop.json` (the drop at 19:54:45.5), `r6-guest-leave.json` (`home.leave` applied at 19:54:47.4) and `r6-blackout-off.json` (19:54:53.36). Guest log `r6-guest-log.txt`: `[ItemCommand] dropped 1 unacknowledged item report(s): the session ended.` at 19:54:49.203, and `r6-guest-state-left.json` shows `lobby 0 / role None / inWorld false`. Rejoin `r6-guest-rejoin.json` (19:55:06 → 19:55:08) and `r6-guest-state-1.json` (`inWorld true` at 19:55:16). `r6-guest-rejoin-log.txt` carries no `re-reported` line after the session edge and shows the re-baseline: `World join received — starting a run to follow.` + `[CarryKernel] rebuilt carry mirror from checkpoint at revision 674` at 19:55:08.701 and the world-entry `World-item snapshot received (252 items)`. `r6-guest-local-after.json`: the guest carries `5197989525` again; `r6-host-tables-after.json`: the host still lists it in the transfer table with the guest as carrier — the declared session-boundary loss, not a divergence |
| 7 | Third-party view | machine | pass | `r7-alt-clone.json` (the alternate's clone read `mode=clone`): zero items under any remote owner — the healed drop materializes no duplicate on the third party. `r7-host-world-all.json`, `r7-guest-world-all.json`, `r7-alt-world-all.json`: all three clients hold exactly one instance of `5197989525` at the identical world position `(-1.472, 490.64)`, `fresh=false`, and all three count 253 world items |
| 8 | In-flight race (pickup before spawn report) | machine | pass | `r8-blackout-on.json` (host inbound parked from 20:03:49.28), `r8-guest-provide.json` (creation **and** pickup of `9492956821` into slot 1 at 20:03:50.91), `r8-blackout-off.json` (20:03:52.46). Guest log `r8-guest-log.txt`: `re-reported ItemSpawn on item 9492956821 to the host (1/12)` and `re-reported ItemPickup on item 9492956821 to the host (1/12)` in the same millisecond 20:03:55.793 (send order preserved), then `ItemSpawn … converged` (operation 3801889743155758355) before `ItemPickup … converged` (operation 3801889743155758357) at 20:03:55.830. Host log `r8-host-log.txt`: `[ItemSpawn] materializing soup (id 9492956821) at (0.0,496.6)` at 20:03:55.817 with `Receive ItemSpawnCommand=100B/1f` and `Receive ItemPickupCommand=64B/1f`. `r8-host-tables.json`: the id is out of the world table and carried by the guest — the player kept the carry |
| 9 | A drop lost and followed by a pickup | machine | pass | `r9-blackout-on.json` (host inbound parked 20:04:27.15), `r9-guest-drop.json` (drop of `5197989525` from slot 0 at 20:04:28.6), `r9-blackout-off.json` (20:04:30.16), `r9-guest-repickup.json` (the re-pickup at 20:04:31.6). Guest log `r9-guest-log.txt`: `ItemPickup on item 5197989525 (operation 3801889743155758868) was refused by the host — it leaves the window after 0 re-report(s); 1 older report(s) stay outstanding.` at 20:04:31.566 → `re-reported ItemDrop on item 5197989525 (1/12)` at 20:04:33.552 → `converged after 1 re-report(s) — the host committed operation 3801889743155758830` at 20:04:33.602. Host log/traffic: `Send CommandRejected=40B/1f`, `Receive ItemDropCommand=132B/1f`, `[ItemDrop] emergencylight (id 5197989525) not present — requesting materialization at (1.8,494.9)` at 20:04:33.581. `r9-host-tables.json`: `worldCount` 276, the id back in the world table with nobody carrying it, and `r9-guest-local.json` no longer lists it |

## Residuals for the user

None: every row above is a machine row read from probe results and log lines.

## Limits

- Row 4's shape needs the host's world-item table empty, and the only edge that empties it is a layer
  change (`[LayerReset] dropped the previous layer's world-rooted items`). That edge also reloads the
  member's scene, and three attempts were needed: the first (`r4-*` / `r4b-*`) parked only the host and
  the destroy probe found nothing to destroy — the member had no local body at +4.1 s
  (`container-read mode=local` → `no-local-body`); the second (`r4c-*`) froze both ends, landed the
  destroy (`carried:true`) and then lost it, because the member's freeze ran 17.1 s and tripped
  `GuestHostSilenceWatchdog` (`No frame from the host … for 15016 ms — ending the session locally.` at
  20:06:22.121, followed by `dropped 1 unacknowledged item report(s): the session ended.`); the recorded
  attempt (`r4d-*`) is the third, with the freeze held to 9.7 s. Each attempt costs a layer change, and a
  second layer advance follows the first on its own (~9 s later) — the `[LayerReset]` line therefore
  appears twice per attempt.
- The "keyframe is skipped for an empty table" half of row 4 is pinned at the mechanism level by
  `SwallowedDestroyOfTheLastCarriedItem_ConvergesWithAnEmptyWorldTable`; the machine row proves the
  empty-table convergence itself (empty world table, empty transfer table, one swallowed destroy frame,
  one re-report, the transfer entry gone) and not the send-path early return.
- Row 4's destroy trigger is a driven `UnityEngine.Object.Destroy` on the carried item (the run's local
  probe `r4-destroy-carried.cs`, because the committed `item-destroy` recipe deliberately skips items
  inside a body), not the native decay the ticket's test names imply; the report path
  (`ItemPatches.ItemOnDestroyPatch` → `ItemWorldSync.OnItemDestroyed` → `SendItemDestroyed`) is the same
  one, and the probe's own answer carries `"carried":true`.
- Consecutive layer changes are not free: after the first attempt the two members were dropped from the
  world and the sandbox logs filled with a repeating `[LayerMod] baseline divergence` warning (the
  guest's rolling log grew from 0.8 MB to 33.4 MB in about four minutes). The batch recovered by
  restarting all three clients; nothing in this ticket's rows depends on the layer hazard, and the hazard
  itself is now tracked by `docs/backlog/done/layer-change-member-dropout.md` — its diagnostic, the
  host-authoritative modifier model and the layer-transition behaviour are this repository's own code, so
  "pre-existing" was the wrong reading of it.
- The 60 s checkpoint cadence is the host's own in-session repair cycle, measured here as
  19:51:14.757 → 19:52:14.757 rather than read from a configuration value.
- One swallow window per row: a report that dies twice (its first send and its first re-report) is
  covered by row 5's second repeat and by row 2, not by repeated runs of rows 1, 3 or 9.
