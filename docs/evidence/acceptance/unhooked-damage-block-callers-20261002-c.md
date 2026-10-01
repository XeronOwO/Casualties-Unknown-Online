# Acceptance record — Host block damage reports: two native `DamageBlock` callers are not hooked

- Ticket: `unhooked-damage-block-callers` — verdict: **back to `todo/`** (`- Status: Todo — Rejected (row 4 fails in batch 20261002-c: the presentation write is reported)`)
- Batch: `20261002-c` — tickets `block-damage-table-capacity-alignment`, `unhooked-damage-block-callers`
- Commit: `09a44f2d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+09a44f2d6f1bea56879c158249ab603ce425cd5b`
- Run: 2026-10-02, 01:05–01:20 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts (in the directory named by `acceptance-artifacts-dir`): `c-logmarks-before-crush.txt`, `c-row4-host.log`, `c-row4-alt.log`, `c-row4-guest.log`, `c-row4b-host.log`, `c-row4b-alt.log`, `probe-crush-place.cs`, `probe-crush-read.cs`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host walks over a `health <= 1` block (footstep crush) | machine (+ audible residual) | **pass** | batch `20261002-b` (not re-run) |
| 2 | Host's spider burrows through a wall | machine | **open** | no reachable spider-burrow path on this machine (declared before the earlier batches; unchanged) |
| 3 | Guest's own footstep crush, host listens | machine | **pass** | batch `20261002-b` (not re-run) |
| 4 | A remote apply (the CUO applier's own `DamageBlock` roll): no report, no echo | machine | **fail** | The single-variable experiment: the guest left the world so the third client was the ONLY peer; the host then crushed staged thin ice under its own feet twice (cells (511..513,976) and (510..512,975)). In both runs the host answered the third client's write reports for the relayed cells — `[BlockSync] answered <third-client>'s report at (511,976) with the authoritative block 0.` ×3 (`c-row4-host.log`; the repeat names (510..512,975) in `c-row4b-host.log`) — and the third client, whose only writes of those cells are the presentation itself, logged `presenting a relayed break` followed by `host answered (511,976) — dropped the pending report` (`c-row4-alt.log`, `c-row4b-alt.log`: three cells each run, `2 left` → `0 left`). The offline guest's log carries no such line (`c-row4-guest.log`), and the third client's own foot cells read (516..518,977) — never the relayed cells — so a step of its own cannot be the writer. The no-echo half the row names is therefore falsified: the presentation write IS reported back and the host re-answers it. |
| 5 | Third peer | machine | **pass** | batch `20261002-b` (not re-run); this batch's own single-peer run is the row-4 evidence above |
| 6 | Report volume | machine | **pass** | batch `20261002-b` (not re-run); the row-4 runs produced exactly three reports per crush, one per relayed cell |

## The failing row, precisely

The expectation the ticket recorded was: a remote apply stays silent (`!IsLocalAction` in the patch,
`BlockBreakSync.IsRemoteApply`, and `WorldEventSync.OnBlockSet`'s early return) — "a received break is
not re-broadcast". The observation is that the receiving side's presentation of a relayed break does
reach the host as a write report: the host logs `answered <third-client>'s report at (cell) with the
authoritative block 0.` for exactly the relayed cells, within ~10–30 ms of the peer's own
`presenting a relayed break` line, and the peer clears its pending entry on the answer. Two repetitions
in one session produced it both times. What this costs (arbitration traffic, and whether a receiver's
presentation write could ever answer with a value that differs from the host's own state) is not
measured here.

## Limits

- Single variable, two repetitions: the report's existence is established; its frequency and cost are
  not.
- The crush staging rests on the batch-`20261002-b` declared substitution (the shipped world generates
  no health-1 block); the roll, the report and the presentation are the native code under test.
- No stack or wire capture of the report itself was taken; the finding is read from the two clients'
  own bookkeeping lines.
