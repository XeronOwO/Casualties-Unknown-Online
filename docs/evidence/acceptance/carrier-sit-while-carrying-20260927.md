# Acceptance record — Carrier can sit while carrying a player

- Ticket: `carrier-sit-while-carrying` — verdict: **stays in `review/`** (row 3's third-party half is
  split to a three-client run)
- Batch: `20260927-d` — carry/pose family (see `20260927-d-scope.md`)
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine · Guest: Sandboxie
- Artifacts: `.acceptance/batch-d/` (`a5`, `s1-s3`, `v4-v6`, `y3-y5`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The carrier cannot sit while carrying, in both host-carrier and guest-carrier directions | machine | **pass** | Guest carrier: `v4` forced `idleTime=13`, `v5/v6` read `0.006` with `clip=ExperimentRunBack`. Host carrier: `a5` read `idleTime=0.006` in the carrying state. Both directions keep the carrier's `idleTime` at zero |
| 2 | A carrier already sitting when the carry begins is returned to a valid standing/carry presentation | machine | **pass** | `y3/y5`: the guest was seated (`ExperimentSit`), the host climbed onto it, and the guest then read `clip=ExperimentIdle`, `idleTime=0.006`, `standing=true` |
| 3 | The suppression is visible on all participating and third-party views | machine | **unproven** | Participant views: the carrier's clip/idle readings above and the clone readings (`pin/drift/mounted`) cover the two participants. The third-party view needs a third client — split to a three-client run |
| 4 | Normal non-carried idle-sit behavior is unchanged | machine | **pass** | `s1-s3`: with no carry, forced `idleTime=13` reached `clip=ExperimentSit` within 0.5 s and stayed there |
| 5 | No carry authority, release semantics or wire protocol change | machine | **pass** | This batch changed no carry code (`72fdb446` is the scope page plus recipe-header corrections); the release paths are exercised by `z8/z9` and `y`-series |

## Limits
- Row 3 is the reason the ticket stays open; everything a two-client run can reach is green.
