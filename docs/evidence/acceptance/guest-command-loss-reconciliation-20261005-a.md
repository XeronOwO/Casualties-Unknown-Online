# Acceptance record — Guest command loss: local pickup/drop result is not reconciled

- Ticket: `guest-command-loss-reconciliation` — verdict: back to `todo/` (`- Status: Todo — Rejected (row 2 diverges)`)
- Batch: 20261005-a — this is the batch's only ticket; the two `trap-layout-*` tickets were left for their own run because
  they need a different world setup (a located building support), not the item world this run built.
- Commit: `98aa7dae` (docs-only since the built `76ef80c2`) · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+76ef80c28162de716c38626075b69a2032fc8206`
- Run: 2026-10-05 17:51 → 18:10 · Host: physical machine · Guest: sandbox `Steam1` · Third client: sandbox `Steam2`
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`
  (preflight: 11 present, exit 0)
- Artifacts: `r1-*`, `r2-*`, `r3-*`, `r5*`, `r6-*`, `r7-*`, `r8*`, `r9-*` (JSON probe results and recipe
  returns) in the directory named by `acceptance-artifacts-dir`

Swallow injection: `net-receive-blackout` parks the receiving client's inbound dispatch, which is the production
lazy-P2P swallow (the sender's transport still reports the send as successful). Rows 1, 3, 8, 9 arm it on the
HOST, so the member's outgoing report is the frame that dies; row 5 arms it on the GUEST, so the host's
committed-batch receipt is the frame that dies. Every row uses one product instance on all three clients.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest pickup command dropped | machine | pass | `r1-guest-pickup.json` (native pickup of world item `9492956821`, slot 0), guest log `[ItemCommand] re-reported ItemPickup on item 9492956821 to the host (1/12)` then `converged after 1 re-report(s) — the host committed operation 3801889743155759709`; host `r1-host-tables-after.json`: transfer table holds `9492956821` (`isTransferred: true`), `worldCount` back to 274, `carried` names the guest; guest `r1-guest-local-after.json` carries it in slot 0 |
| 2 | Guest drop command dropped | machine | **fail** | `r2-guest-drop.json` (native drop of `9492956821`, slot 0) inside the host blackout; the report never re-reported — guest log shows `[ItemCommand] dropped 1 unacknowledged item report(s): the world baseline was restored at revision 950`. The host's own periodic checkpoint landed 3.4 s after the drop (host log `Sent kernel checkpoint at revision 950` at 18:01:49, `ResetPending` at the guest 18:01:49.6) and destroyed the queued report before its 5 s re-report was due. Host `r2-host-tables-after.json` still holds `9492956821` in the transfer table with the guest as carrier; guest `r2-guest-local-after.json` carries only `5197989525` — the two sides disagree and stay disagreed for the rest of the session (still disagreed at 18:08, `r7-host-tables2.json`) |
| 3 | Destroy command dropped while the host table is non-empty | machine | pass | `r3-guest-destroy.json` (`item-destroy` on world item `13787924117`), guest log `origin=OnItemDestroyed result=Committed(1)`, `re-reported ItemDestroy on item 13787924117 to the host (1/12)`, `converged after 1 re-report(s)`; host `r3-host-tables-after.json` no longer lists the id, `worldCount` back to 274 |
| 4 | Destroy command dropped while the host table is empty | machine | unproven | the row needs the host's world-item table to be empty; this world carries 274 world items and nothing in the run's vocabulary empties it, so the "keyframe is skipped for an empty table" half cannot be staged. Not judged |
| 5 | Duplicate/replayed re-report | machine | pass | `r5b-guest-drop.json` (drop of `22377858709`) with the guest's inbound parked for ~6 s so the committed-batch receipt dies; guest log `re-reported ItemDrop on item 22377858709 to the host (1/12)` then `converged after 1 re-report(s) — the host committed operation 3801889743155762762`; host log answers the repeat without a second commit: first report `[ItemDrop] cookies (id 22377858709) not present — requesting materialization`, the repeat `[ItemDrop] cookies (id 22377858709) present — re-placing at (11.8,423.0)` — one materialization, one operation id. The first attempt (`r5-*`) armed a 1.6 s window and never produced a repeat: the host's committed batch is emitted in batches, so the receipt arrived after the window closed and the report was acknowledged normally |
| 6 | Reconnect | machine | unproven | the run proved only the WORLD edge, not the SESSION edge: `r6-leave.json` leaves the world with two reports outstanding, and the window is deliberately KEPT (no `dropped … the session ended` line) — the still-live lobby re-reports them and they converge (`re-reported ItemSpawn/ItemPickup on item 39557727893` → `converged`). The row's own shape (session gone, rejoin re-baselines) needs a lobby leave/disconnect and a rejoin round trip, which this run did not stage |
| 7 | Third-party view | machine | unproven | the third client's proxy read (`r7-alt-clone2.json`) renders exactly one item on the guest (`owner=76561198863287957 count=1`) while the host's authoritative table names two (`9492956821`, `35262760597`, `r7-host-tables2.json`). The delta is row 2's divergence, not row 7's subject: the session was already split before this read, so the row's own expectation ("all peers agree on the healed report's location, no duplicate on the third party") cannot be separated from it. The read stands as third-party corroboration of row 2 |
| 8 | In-flight race (pickup before spawn report) | machine | pass | `r8b-provide.json` creates AND picks up `30967793301` with the host's inbound parked, so the whole pair dies; guest log replays both in send order — `re-reported ItemSpawn on item 30967793301 (1/12)` and `re-reported ItemPickup on item 30967793301 (1/12)`, then `ItemSpawn … converged` before `ItemPickup … converged`; host `r8b-host-tables.json` has the id out of the world table and carried by the guest, i.e. the player kept the carry |
| 9 | A drop lost and followed by a pickup | machine | pass | `r9-guest-drop.json` drops `30967793301` inside the host blackout, `r9-guest-repickup.json` takes it back after the window closes, so the pickup arrives while the host still believes the guest carries it: guest log `ItemPickup on item 30967793301 (operation 3801889743155764389) was refused by the host — it leaves the window after 0 re-report(s); 1 older report(s) stay outstanding`, then `re-reported ItemDrop on item 30967793301 (1/12)` and `ItemDrop … converged`; host `r9-host-tables.json` puts the item back in the world table with nobody carrying it, and guest `r9-guest-local.json` carries nothing |

## Residuals for the user

None: no row in this run needs a person's eyes — every row above is a machine row read from probe results and
log lines.

## Limits

- The Game Adapter's Unity half is exercised for the first time here (native `Body.PickUpItem`, `Body.DropItem`
  and `Item.OnDestroy` drive the reports), but the destroy trigger is a driven `Object.Destroy` on a runtime world
  item, not the native decay-to-zero the ticket's test names imply; the report path it takes
  (`Item.OnDestroy` → `ItemWorldSync.OnItemDestroyed` → `SendItemDestroyed`) is the same one.
- Rows 4, 6 and 7 are `unproven` for the reasons above, not for a missing dependency; every ticket row that
  needed a capability had one (preflight exit 0).
- One swallow window per row: a report that dies twice (both the first send and the re-report) is covered by
  row 8's pair, not by a repeated run of rows 1, 3 or 9.
- The 60 s checkpoint period is the host's own; the run measured it over five consecutive sends
  (17:59:49 → 18:00:49 → 18:01:49) rather than reading a configured value.

## Finding carried into the ticket (row 2)

`GuestCommandReconciliation` empties its queue whenever the guest restores a host checkpoint
(`OnCheckpointRestored` → `ResetPending`, `src/CasualtiesUnknownOnline.Runtime/Session/Items/GuestCommandReconciliation.cs`,
the line reading `ResetPending($"the world baseline was restored at revision {checkpoint.GlobalRevision}")`).
With a 5 s re-report cadence and a 60 s checkpoint cadence, any swallowed report that happens to be outstanding
when a checkpoint lands is dropped for good: the host never learns the operation and its authoritative table keeps
the stale ownership, while the guest keeps its own local result. The checkpoint rebuilds the guest's mirror of the
HOST's state and says nothing about reports travelling the other way, so "the baseline was restored" is not a
reason to discard them. Row 2 reproduced this deterministically on the first attempt and the divergence was still
present seven minutes later.
