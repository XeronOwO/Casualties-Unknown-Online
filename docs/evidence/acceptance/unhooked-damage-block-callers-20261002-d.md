# Acceptance record — Host block damage reports: two native `DamageBlock` callers are not hooked

- Ticket: `unhooked-damage-block-callers` — verdict: **`review/`** (row 4 passes in batch `20261002-d`
  after the chain-query fix; row 2 stays blocked on this machine's missing spider-burrow path, so the
  ticket is not closed)
- Batch: `20261002-d` — scope and the run's full account: `docs/evidence/acceptance/20261002-d-scope.md`
- Commit: `b3aadc01` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+b3aadc019c2cf8993b7b9661712f350fe8659b71`
- Run: 2026-10-02, 06:24–06:31 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts (in the directory named by `acceptance-artifacts-dir`): `d-crush-host-place-1.json`,
  `d-crush-host-place-2.json`, `d-row4a-host.log`, `d-row4a-alt.log`, `d-row4a-guest.log`,
  `d-row4b-host.log`, `d-row4b-alt.log`, `d-b2-host-place.json`, `d-b2-guest.log`, `d-b2-alt.log`,
  `d-b2-host-answered.log`, `d-b3-guest-place.json`, `d-b3-host.log`, `d-b3-alt.log`,
  `d-b4-fill-guest.json`, `d-b4-census-*.json`, `d-b4-compare.txt`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host walks over a `health <= 1` block (footstep crush) | machine (+ audible residual) | **pass** | batch `20261002-d`: with the guest re-entered, both peers logged `presenting a relayed break` for all three crushed cells while the host's log carried no `answered` line (`d-b2-guest.log`, `d-b2-alt.log`, `d-b2-host-answered.log`) |
| 2 | Host's spider burrows through a wall | machine | **blocked** | no reachable spider-burrow path on this machine (declared before the earlier batches; unchanged — the ticket stays open on it) |
| 3 | Guest's own footstep crush, host listens | machine | **pass** | batch `20261002-d`: the host applied the guest's break (its own `presenting a relayed break` lines) and the third client presented it too (`d-b3-host.log`, `d-b3-alt.log`) |
| 4 | A remote apply (the CUO applier's own `DamageBlock` roll): no report, no echo | machine | **pass** | two single-variable runs: the guest left the world so the third client was the ONLY peer; the host crushed staged thin ice under its feet (cells (512..514,1010) then (512..514,1009)). In both runs the peer logged `presenting a relayed break` for every cell and NO `dropped the pending report` / `host answered` line, and the host's log carried no `[BlockSync] answered …report at` line at all (`d-row4a-alt.log`, `d-row4a-host.log`, `d-row4b-alt.log`, `d-row4b-host.log`; the offline guest's log is empty of them, `d-row4a-guest.log`). The presentation half still runs — the fix suppressed the report, not the break's presentation |
| 5 | Third peer | machine | **pass** | batch `20261002-d`: the third client presented the host's crush cells alongside the guest (`d-b2-alt.log`) |
| 6 | Report volume | machine | **pass** | batch `20261002-d`: exactly one `presenting a relayed break` per peer per crushed cell, three cells per crush, no per-frame stream (`d-b2-guest.log`, `d-b2-alt.log`); the row-4 window carries exactly three presentation lines and zero report lines at the same time |

## The fixed row, precisely

Batch `20261002-c` observed the peer's presentation write being reported back — the host answered the
only peer's write report for the relayed cells (`[BlockSync] answered <peer>'s report at (511,976) with
the authoritative block 0.`) and the peer cleared its pending entry on the answer. The cause was
`CallContext.Current` answering the INNERMOST origin: the damage patch pushes `DamageBlockOrigin` on top
of the caller's `RemoteApply` for the whole roll, so `WorldEventSync.OnBlockSet`'s early return stopped
seeing the remote application and the roll's own `SetBlock(0)` was reported as a local player break.
Commit `b3aadc01` adds `CallContext.IsWithin(Origin)` (a chain scan) and converts the 15 remote-apply
attribution guards; the presentation is untouched. This batch's own repetition shows the same crush
presenting on the peer and staying silent about it.

## Limits

- Two repetitions in one session: the report's ABSENCE is established for this shape; a rare re-entry of
  the echo through some other write is not excluded.
- The audible half is a residual for the user; the machine evidence is the receiving side's own
  presentation line, not a heard result.
- Row 2 is blocked, not failed: this machine has no reachable spider-burrow path (the selfcheck records
  the clone and path answers), so the ticket stays open with that row named.
