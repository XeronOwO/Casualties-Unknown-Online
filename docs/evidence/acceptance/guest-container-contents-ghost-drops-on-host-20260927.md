# Acceptance record — Guest carried container contents appear as world drops on the host view

- Ticket: `guest-container-contents-ghost-drops-on-host` — verdict: **stays in `review/`** (the
  container scenario is split out; only the suite row is decided here)
- Batch: `20260927-d` — the item-domain ticket that the recipe headers used to name; it is not a
  carry-relation scenario, and this run carries no container setup
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine · Guest: Sandboxie
- Artifacts: `.acceptance/batch-d/full-tests-final.log` (the suite run this record uses)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | No periodic dog food appears as a world drop on the host while the guest carries the trash bag | visual | **blocked (setup gap)** | Needs a container scenario (a guest putting an item into a carried container and moving) that this batch does not carry; recorded as the ticket's blocker |
| 2 | The guest view and host view show the dog food inside the container consistently | visual | **blocked (setup gap)** | Same missing scenario |
| 3 | No duplicate/ghost item id, no transfer-table resurrection, no dropped item after reconnection | machine | **blocked (setup gap)** | The authoritative paths are unchanged by the fix, but the runtime claim needs the same scenario (and its reconnect half needs a reconnect run) |
| 4 | Existing container/item sync tests and repo gates remain green | machine | **pass** | This run's suites: gates `293/293`, main `4520/4520`, build 0 warnings / 0 errors (`full-tests-final.log`) |

## Limits
- Three of four rows are setup-gap blocked: the next run needs a container scenario recipe (pick up /
  load the trash bag on the guest, move, watch the host). Until then the ticket stays in `review/`
  with the gap named, not silently passed.
