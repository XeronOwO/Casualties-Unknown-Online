# The partial-damage absolute report and the live delta can overlap

- Status: Todo
- Priority: Low-Medium
- Category: Network / sync coverage / world blocks
- Source: Independent adversarial review of the W2 landing (2026-09-18) — the landing's recorded limitation described only the under-count half
- Related: `review/guest-partial-block-damage-re-report.md` (W2 — the absolute re-report, landed), `todo/sync-cadence-review.md`

## Problem (evidence)

The W2 recovery merges a guest's ABSOLUTE re-report into the host's row with a per-cell
maximum: `GameBlockDamageTable.DecideMerge` raises the host's row only when the reported value
is strictly above it, and `Merge` writes `existing.damage = entry.Damage`
(`src/CasualtiesUnknownOnline.GameAdapter/World/GameBlockDamageTable.cs`). The live relay
stays a DELTA and the receiving side accumulates it — the game's own
`WorldGeneration.DamageBlock` body does `blockDamage.damage += dmg`
(`reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs`, stable decompiled line). The
two halves therefore disagree in BOTH directions:

- **Under-count.** Two senders whose contributions were never visible to each other and whose
  reports were both swallowed converge to the higher absolute value, not the sum.
- **Over-count.** A delta that arrives AFTER the absolute re-report covering it has already
  been merged raises the row a second time: the merge lands the reported `D`, then the delayed
  delta adds another `D`, so the host's row ends above what either side ever held. `KeepHost`
  cannot catch it — the duplicate arrives as a delta, not as a second absolute value.

The over-count needs a DELAYED (not dropped) delta. `PacketSender` defaults to reliable, so
the lazy-P2P swallow window drops frames outright; a retransmitted frame that lands after the
report's answer is what opens the window, and that is the same session phase W2 exists for.

## Goal

A sender's contribution to a cell is counted exactly once on every side, whatever interleaving
of live deltas and absolute re-reports the transport produces, and without changing when a
block breaks.

## Design direction (decide at implementation)

1. **Per-sender ledger** — the host keeps (sender, cell) → that sender's cumulative
   contribution: a delta raises the entry, an absolute report REPLACES it, and the cell's row
   is re-derived as the sum over senders plus the host's own. That is the semantics the guest's
   absolute value and the current merge already assume.
2. **Sequenced deltas** — every delta carries a per-(sender, cell) sequence or cumulative
   value, so a receiver drops a stale frame instead of adding it blindly.
3. **Accepted loss** — rejected: the row can exceed the block's health, which changes when the
   block breaks — a user-visible divergence, not a presentation artifact.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | One sender: delta, then the absolute re-report | Counted once |
| 2 | One sender: absolute re-report, then the delayed delta | Counted once |
| 3 | Two senders, both interleavings | The sum, never the maximum |
| 4 | Duplicate absolute re-report | Idempotent |
| 5 | A cell that has since broken | Ignored (air) — the block-state channel owns it |
| 6 | The game's 128-entry cap | The same refusal semantics as the snapshot apply |
| 7 | Third-party view | The same total on every side |

## Non-goals

- The W2 recovery itself and its entry lifetime (landed).
- Optional members / relay re-sends (`review/sync-event-and-periodic-fallback-coverage-audit.md`).
- Cadence tuning (`todo/sync-cadence-review.md`).
