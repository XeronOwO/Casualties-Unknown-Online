# Acceptance record — Block-damage tables: CUO's registry and the game's own list disagree about capacity and eviction

- Ticket: `block-damage-table-capacity-alignment` — verdict: **moved to `done/`** (row 1's late-joiner half passes in this batch; rows 2 and 3 stand from the earlier batches)
- Batch: `20261002-c` — tickets `block-damage-table-capacity-alignment`, `unhooked-damage-block-callers`
- Commit: `09a44f2d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+09a44f2d6f1bea56879c158249ab603ce425cd5b`
- Run: 2026-10-02, 01:05–01:20 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts (in the directory named by `acceptance-artifacts-dir`): `c-fill-guest.json`, `c-fill-guest-retry.json`, `c-fill-host.json`, `c-fill-host-2.json`, `c-census-pre-leave-host.json`, `c-census-pre-leave-guest.json`, `c-census-pre-leave-alt.json`, `c-census-pre-leave-compare.txt`, `c-census-alt-pre-reentry.json`, `c-census-late2-host.json`, `c-census-late2-guest.json`, `c-census-late2-alt.json`, `c-census-late2-compare.txt`, `c-host-snapshot-log.txt`, `c-alt-snapshot-log.txt`, `c-host-restore-log.txt`, `c-host-saveseam-log.txt`, `c-alt-rejoin.json`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Long run, more damaged cells than the cap: host, every connected guest and a late joiner hold the SAME damaged-cell set | machine | **pass** | The connected-peers half was re-derived: after the guest's fill (`damaged=140, complete=true, evicted=140`) and the host's (`damaged=140`), all three tables read the same 128-row set (`c-census-pre-leave-*`, `union=128 in_all=128 missing_somewhere=0 damage_mismatch=0`). The late-joiner half was then driven on the re-filled table (`c-fill-host-2.json`: `damaged=140, tableBefore=5, tableAfter=128, evicted=17`): the third client left the world and the lobby and rejoined (`c-alt-rejoin.json`: `active=true`, `gateText="Starting…"`); the host answered the re-handshake with a direct `WorldJoin` and sent the world-entry snapshot group (`c-host-snapshot-log.txt`: `Sending world-entry snapshot group to …`, traffic `Send BlockDamageSnapshot=1671B`); the rejoining client received and applied it (`c-alt-snapshot-log.txt`: `Block-damage snapshot received (128 cells, authoritative state)`, `Block-damage snapshot applied (128/128 cells, 0 not applicable, 0 cleared)`), and its post-entry read was 128 rows (`c-census-late2-alt.json`). The three sets compared `union=128 in_all=128 missing_somewhere=0 damage_mismatch=0` — `verdict: IDENTICAL` (`c-census-late2-compare.txt`). |
| 2 | Eviction: a cell leaving the table leaves it on every side | machine | **pass** | batch `20261001-y` (not re-run; the change set under acceptance is unchanged since it) |
| 3 | Unhooked writers: damage written by the `DamageBlock` callers CUO does not hook reaches the shared view, or the gap is named | machine | **pass** | batch `20261002-b` (not re-run); its live half is the staged crush, whose staging substitution this batch re-used |

## The late-joiner half, as driven

The batch first ran the `leave → continue` path recorded in `20261002-b-scope.md` and observed why it
cannot stage this row here: `continue` restores the world named by `lastOpenedWorldId`, which a new run
does not move, so it brought back another world's entry `layer-end` cut — `0 world-block` and
`0 partial-damage` rows (`c-host-restore-log.txt`) — and the host's table read `tableCount=0`
afterwards (`c-census-post-continue-host.json`; `c-census-post-continue-host-2.json` shows the 5 rows
that had happened since). This session's own leave had written a `MenuReturn (MidRun)` cut of the world
it played, `128 world-block row(s)` (`c-host-saveseam-log.txt`) — the pointer, not the cut, is what the
continue missed. **Update 2026-10-02**: that is the missing first-cut write of the Continue pointer, fixed
in `WorldSaveService.OnCutReported` — a run's own world now becomes the Continue target on its first
committed cut (`docs/backlog/done/layer-mod-baseline-divergence-on-continue.md`), so the `leave → continue`
route opens the run's own world. That observation is recorded, not judged: it is a staging route, and the row was then
driven on the member re-entry route instead — host stays in the world, the member re-handshakes, the
host's direct `WorldJoin` brings it back, and the world-entry fan-out sends the snapshot read at send
time. The rejoining client's table was empty right after re-entry (`c-census-late-alt.json`: the read
taken before the snapshot landed) and 128 rows after it (`c-census-late2-alt.json`), which is the
snapshot's delivery shown end to end.

## Limits

- One session is not a race proof; each row was read from the state the ends converged to. The
  connected-peers half's own over-cap check is what batch `20261001-y` established; this batch
  re-derived it once more without changing the code under acceptance.
- The crush staging of row 3 rests on the batch-`20261002-b` declared substitution (the shipped world
  generates no health-1 block), re-used here unchanged.
- The two fills in the late-joiner half used directions that overlapped one another's region on the
  first guest fill; the tables still converged on one identical set, and the row's own expectation (one
  set) is what was judged.
