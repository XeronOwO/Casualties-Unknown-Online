# Guest partial block damage has no re-report

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / world blocks
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row W2); split out of `review/guest-block-mutation-re-report.md` when the W1 half landed
- Related: `review/guest-block-mutation-re-report.md` (W1 — the terminal block state, landed), `todo/sync-cadence-review.md`

## Problem (evidence)

A guest's partial-damage report is one-shot, and the authoritative record plus the
absolute fallback are host → guest only:

- The live relay is `BlockDamaged` (bidirectional) but delta-based: a surviving
  block is reported immediately with the raw damage
  (`src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs:109`
  `_world.SendBlockDamaged(new NetVector2(pos.x, pos.y), dmg, bonusMetal, null, null);`).
- The authoritative record is host-only:
  `src/CasualtiesUnknownOnline.Runtime/Session/World/BlockDamageRegistry.cs:32`
  (`if (_session.Role != SessionRole.Host)`), so a swallowed guest report never
  enters the registry.
- The absolute fallback is `BlockDamageSnapshot` (89), `PacketHandler`
  direction `HostToGuest` (`src/CasualtiesUnknownOnline.Runtime/Session/World/BlockDamageRegistry.cs:74`),
  sent at world entry and on the 60 s cycle
  (`src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:130`).
- Consequence: the host's registry has no entry for that cell, so its snapshot
  omits it and cannot correct either side; the host's block keeps its higher
  remaining HP and the guest's crack sprite is local-only. The divergence
  persists until the break, whose terminal block state is now healed by W1
  (the air-write re-report also clears the crack via `OnBlockAirWrite`).

## Goal

A swallowed guest partial-damage report converges: the host's accumulated
damage for that cell reaches the guest's, and the host's snapshot then heals
every peer, without resurrecting damage on a block that has since broken or
been restored.

## Design direction (decide at implementation)

The live relay is additive (`world.DamageBlock(cell, dmg, …)`), so a re-report
cannot simply replay the delta — it would double-apply. The re-report must be
**absolute**, and the merge must not lose another player's concurrent damage:

1. **Absolute re-report + max-merge** — the guest keeps its per-cell absolute
   damage table and re-reports it (a `BlockDamageSnapshot`-shaped message in the
   guest → host direction). The host applies `max(host, reported)` per cell
   instead of an overwrite, so two players hitting the same block both count.
2. **Cumulative per-sender delta** — the guest re-reports the delta accumulated
   since its last acknowledged report, keyed by (guest, cell) on the host so a
   duplicate is idempotent (the medical cumulative-stream pattern).
3. **Accepted loss** — rejected: the divergence is user-visible (crack state and
   the next hit's break threshold differ per side).

Either way the apply must stay bounded by the game's 128-entry `blockDamages`
list, skip air cells and out-of-range values (the existing guards), and never
break the block (a break is the block-state channel's semantic).

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest damages a block; the report is dropped | The host's accumulated damage converges to the guest's; a later hit breaks on both sides |
| 2 | Two guests damage the same block; one report is dropped | The host keeps both contributions (no lost damage, no double count) |
| 3 | Duplicate delivery of the same absolute re-report | Idempotent — the host's damage is unchanged |
| 4 | The block breaks before the re-report lands | The re-report is ignored (air cell); no crack resurrects |
| 5 | The block is restored to full HP (layer regeneration / baseline) | No stale damage is re-applied |
| 6 | World entry / reconnect while a cell has outstanding damage | The host snapshot and the guest table agree |
| 7 | Game-side `blockDamages` cap (128) | The re-report respects the cap and logs the overflow |

## Non-goals

- The terminal break state (W1 — landed).
- Anti-cheat / strict validation of guest damage reports.
- Host-side cadence tuning (`todo/sync-cadence-review.md`).
