# Acceptance record — Block-damage tables: CUO's registry and the game's own list disagree about capacity and eviction

- Ticket: `block-damage-table-capacity-alignment` — verdict: **stays in `review/`** (row 2 passes; row 1 proven for the connected peers but its late joiner was not driven; row 3 not driven)
- Batch: `20261001-y` — tickets `block-damage-table-capacity-alignment`, `guest-partial-block-damage-re-report`, `unhooked-damage-block-callers`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-block-mutation-re-report`
- Commit: `4b28a64d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+4b28a64ddbdbb9068379db2f534f9ede0f472c00`
- Run: 2026-10-01, 22:12–22:26 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `y-fill-guest.json`, `y-fill-host.json`, `y-census-before-*.json`, `y-census-guestfill-*.json`, `y-census-after-*.json`, `y-census-after-compare.txt`, `y-crush-host-find.json`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A long run with more damaged cells than the cap: host, every guest and a late joiner hold the SAME damaged-cell set | machine | **unproven** (proven for the connected peers) | the guest filled its own table past the cap (`y-fill-guest.json`: `damaged=140, complete=true, tableBefore=13, tableAfter=128, evicted=25`) and host+guest then held byte-identical sets (`y-census-guestfill-*.json`: 128 rows each, 128 shared, 0 one-sided). The host then filled past the cap (`y-fill-host.json`: `damaged=140, tableBefore=128, tableAfter=128, evicted=140`) and all three clients read the same 128 rows (`y-census-after-*.json`, `y-census-after-compare.txt`: union 128, in-all-three 128, differing rows 0). The LATE JOINER was not driven — no client entered the world after the fill, so the row's third participant is unproven. |
| 2 | Eviction: a cell leaving the table leaves it on every side | machine | **pass** | the 13 cells every client held before the fills were evicted and none survived anywhere: the guest's own 13-row baseline had 0 of 13 rows left after its fill, the host's fill reported `evicted=140`, and the three post-fill censuses agree on exactly 128 rows — no side kept a crack the authority dropped. |
| 3 | Unhooked writers: damage written by the `DamageBlock` callers CUO does not hook reaches the shared view, or the gap is named | machine | **unproven** | its live half is the footstep crush, which this run could not stage (see `unhooked-damage-block-callers-20261001-y.md`: `crush-find` returned 0 candidates in every layer searched). The hook-anchor half is unchanged from the landed change and its static gate still passes. |

## Limits

- Both fills used `dmg=0.5` on fresh bands, so `broke=0` and every row is a partial-damage row; the run
  never needed the crack-render half.
- The late-joiner path the row names is the world-entry snapshot; this run's three clients were all in the
  world before the fills, and the rejoin wedge batch `20261001-x` recorded made a rejoin-only plan
  unattractive for the remaining window.
