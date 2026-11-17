- Status: Done
- Priority: Medium

# Split the block-break remote apply path out of `BlockBreakSync`

## Why

`BlockBreakSync` carried 594 aggregate lines before 2026-11-13; holding a relay that arrives while the world
generates (the fix for `docs/backlog/review/1.0-block-table-diverges-between-clients.md`) pushed it to 688 —
over the 600-line ceiling. The class owned two jobs: reporting this side's own damage/break, and applying
(and arbitrating) a relay the peer sent. The 2026-11-18 split separates them.

## The split

- New `src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakRemoteApply.cs`: `Apply(...)` — the body of
  the old `ApplyRemoteReport` — plus `AcceptBreak` and `OnRemoteDamageBrokeBlock`, taking
  `ISessionControl`, `IWorldControl`, `IItemControl`, `WorldBuildingEntitySync`, `BlockBreakArbitration`,
  `BlockDropBoundJudge`, `OperationTrace` and the logger. It logs through `ILogger<BlockBreakSync>` on purpose,
  so every moved log line keeps its exact prefix.
- `BlockBreakSync` (403 lines) keeps: the local report path (`OnBlockDamaged`, `FlushPendingBlockBreak`), the
  deferred relay queue (it is the arrival side), the arbitration table's lifetime, and the subscription targets
  (which delegate to the new collaborator).
- The `BlockBreakSync` entry is deleted from `docs/architecture-debt.json`; the gate refuses a recorded ceiling
  once the class is back under the line.

## Acceptance reading (2026-11-18)

**PASS** on the deployed `0.1.0+94f886c43a52d7db923a4ec3fd6acdf6cbee40d0` (`.acceptance/blocksync-split`):

- `block-sync` PASS — `InRun`, case `1.0` s, total `1.5` s, host 0/0 and member 0/0 log errors, suite wall `26.9` s.
- Host break at (3,489): `blockBefore 1, blockAfter 0`; the member's `block-read-at` then read `block 0` on the same
  cell. Member break at (5,489): `blockBefore 4, blockAfter 0`; the host's read returned `block 0`. So both
  directions of the relay still cross after the move.
- The moved half emitted no `[BlockBreak` line at Information level in this run (the row's assertions are the
  evidence); the log category is unchanged, so any future line keeps the same prefix.
- Gates `836/836`, unit `6747/6747`, `dotnet format` exit 0 on the committed tree.

## Run sheet

- [x] Read the current path end to end and name the seam (`ApplyRemoteReport` → `AcceptBreak` → `OnRemoteDamageBrokeBlock`).
- [x] Extract the remote-apply half with the eight collaborators it needs.
- [x] Delete the debt entry and count the new file in the sweep.
- [x] Repoint the two origin pins (presentation gate, sync-coverage quotes) at the file that owns the code.
- [x] Build, unit suite, gates, format.
- [x] Commit, deploy `-Acceptance`, and read the focused `block-sync` row on the real game.

## Residual

The `BlockBreak` log lines the ticket originally asked to compare were not emitted at Information level on this
run, so "identical log lines" is not claimed; the row's own assertions (both directions of the break) are what the
reading proves.
