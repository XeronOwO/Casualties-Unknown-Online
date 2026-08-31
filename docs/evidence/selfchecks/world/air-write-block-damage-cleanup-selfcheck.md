# Air-write block-damage cleanup — Self-Check (2026-08-31)

Owner cycle: backlog "Guest-mined block leaves ghost fragments on host".
A guest's break applies on the host via `BlockPlaced` → `SetBlock(0)` directly,
which does not remove the game's own `BlockDamage` entry/sprite. The block is
air, but its crack sprite remains — "fragmented air".

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Local break cleanup | `WorldGeneration.DamageBlock` removes its `BlockDamage` and destroys the crack sprite when damage reaches health (`WorldGeneration.cs:738-841`). |
| 2 | Remote break path | A guest's air write applies through `SetBlock(0)` directly (`WorldEventSync.OnRemoteBlockPlaced`), so the game's `blockDamages` list is not touched by `DamageBlock` (`WorldGeneration.cs:661-666`). |
| 3 | Stale visual | `BlockDamage.UpdateSprite` keeps a `SpriteRenderer` at the cell until the entry is removed (`BlockDamage.cs:8-44`); a direct air write leaves both. |
| 4 | Existing registry clear | CUO's `BlockDamageRegistry` (snapshot source) was already cleared by `BlockBreakSync.OnBlockAirWrite`; the game-side list was not. |

## 2. Root cause

Direct `SetBlock(0)` paths (remote air write, block-state snapshot, earthquake)
bypass `WorldGeneration.DamageBlock`, so the game's `BlockDamage` entry and its
crack sprite survive over an already-air cell.

## 3. Fix

- New `BlockDamageCleaner.ClearForAirWrite` removes the game's `BlockDamage`
  entry and destroys its sprite.
- `BlockBreakSync.OnBlockAirWrite` now calls the cleaner after clearing the
  runtime registry, covering every local air write and the host's remote air
  write path.
- `WorldEventSync.OnRemoteBlockPlaced` calls it on the guest branch (host
  broadcast relay), and `OnRemoteBlockState` calls it when a block-state
  snapshot applies an air cell.

## 4. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Pure game-list cleanup | `BlockDamageCleaner.ClearForAirWrite` removes an existing entry | `BlockDamageCleaner.cs`; `BlockDamageCleanerTests` (2 cases) |
| Local / host remote air write | `BlockBreakSync.OnBlockAirWrite` calls the cleaner | `BlockBreakSync.cs` |
| Guest air-write relay | `WorldEventSync.OnRemoteBlockPlaced` calls `OnBlockAirWrite` for block 0 | `WorldEventSync.cs` |
| Block-state air cell | `WorldEventSync.OnRemoteBlockState` calls `OnBlockAirWrite` | `WorldEventSync.cs` |
| Full suite | no regressions | 1849 tests green |

## 5. Verification

- Red→green: new `BlockDamageCleanerTests` failed before the helper existed,
  then passed after implementation.
- `dotnet test`: 1849 passed.
- `dotnet format`, `check-architecture`, `check-event-replay`,
  `check-entity-event-dispatch`, `check-delivery`: all pass.
- Runtime acceptance: not performed; final dual-client acceptance remains a
  user release action.
