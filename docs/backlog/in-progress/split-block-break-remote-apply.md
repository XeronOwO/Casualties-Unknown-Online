- Status: In progress
- Priority: Medium

# Split the block-break remote apply path out of `BlockBreakSync`

## Why

`BlockBreakSync` carried 594 aggregate lines before 2026-11-13; holding a relay that arrives while the world
generates (the fix for `docs/backlog/review/1.0-block-table-diverges-between-clients.md`) pushed it to 688 —
over the 600-line ceiling. The class now owns two jobs: reporting this side's own damage/break, and applying
(and arbitrating) a relay the peer sent. Those are separable.

## The split

- New `src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakRemoteApply.cs`: `Apply(...)` — the body of
  today's `ApplyRemoteReport` — plus `AcceptBreak` and `OnRemoteDamageBrokeBlock`, taking
  `ISessionControl`, `IWorldControl`, `IItemControl`, `WorldBuildingEntitySync`, `BlockBreakArbitration`,
  `BlockDropBoundJudge`, `OperationTrace` and the logger.
- `BlockBreakSync` keeps: the local report path (`OnBlockDamaged`, `FlushPendingBlockBreak`), the deferred
  relay queue (it is the arrival side), the arbitration table's lifetime, and the subscription targets
  (which delegate to the new collaborator).
- Delete the `BlockBreakSync` entry from `docs/architecture-debt.json` in the same change — the gate refuses a
  recorded ceiling once the class is back under the line, so the entry cannot rot.

## How it is verified

- The `block-sync` acceptance case (host breaks a real terrain cell; the member's own `block-read-at` reads
  the same cell changed) plus the `gates` and the unit suite; the `BlockBreak` log lines must stay identical on
  a passing run.

## Recorded ceiling

`docs/architecture-debt.json` names this ticket while the class stays over 600 lines.
