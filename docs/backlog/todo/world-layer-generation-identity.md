# World/layer generation identity is missing from the wire

- Status: Todo
- Priority: Medium
- Category: Network / protocol / world generation (attribution of world reports)
- Source: `review/guest-break-drops-recovery.md` — the limitation its 2026-09-18 review round forced: a break report naming a cell the host still holds is REFUSED, because without a generation identity a stale previous-layer report cannot be told apart from a legitimate one
- Related: `review/block-break-first-writer-wins.md`, `review/guest-block-mutation-re-report.md` (W1), `review/guest-partial-block-damage-re-report.md` (W2), `review/enemy-snapshot-binding-recovery.md` (that domain solved its own version of "which generation does this fact belong to?" with a per-entity binding anchor)

## Problem (evidence)

The W1/W2 recovery converges a swallowed guest report through an absolute host answer plus a guest
re-report. Attribution breaks at a world/layer boundary because the direct world reports carry no
generation identity:

- The guest's break travels as a cell-keyed report plus a drops report
  (`GameAdapter/World/BlockBreakSync.cs`), and the host's cell keys are LAYER-RELATIVE: after a layer
  change the same `(x, y)` addresses a freshly generated block.
- `Runtime/Session/World/BlockBreakArbitration.cs` therefore refuses a break report that names a cell
  the host still holds. The 2026-09-18 round established why accepting it is wrong: a stale
  previous-layer report would take that verdict and `DamageBlock` would break a newly generated block
  with the report's REAL damage (only the fallback's re-send carries zero). The refusal costs a
  legitimate same-generation case — the air-write report was lost, the drops report arrived — whose
  drops are then rolled back and destroyed on the breaker.
- The kernel path already has the vocabulary: `RunEpoch` rides every `EnvelopeHeader` and is
  epoch-filtered on both sides (`Runtime/Session/Items/KernelProtocolService.cs`). The direct
  `NetMsg` world reports (`BlockDamaged`, `BlockPlaced`, `BlockDamageReport`, trap-layout and
  runtime-entity snapshots) do not carry it, and the enum comments in `Runtime/Protocol/NetMsg.cs`
  say so implicitly: only the kernel envelope mentions an epoch.

## Goal

A guest world report that belongs to an earlier generation is refused BECAUSE it is stale (with a
precise log), not because it merely looks stale; and the legitimate same-generation case the current
heuristic destroys (the air write lost, the drops report delivered) is accepted so its drops survive.

## Design direction (decide at implementation)

1. **Carry the generation identity on the direct world reports.** The kernel's `RunEpoch` is the
   existing vocabulary; the run state also knows the layer, so the implementer picks whichever the
   receiver can compare against its own current generation. The receiver drops a stale report with a
   `stale generation` log and accepts the same-generation shape the current heuristic refuses.
2. **Or route those reports through the kernel envelope** and inherit its epoch filter whole. Heavier:
   each domain has its own authority rules, and a cell-keyed report is not a kernel command today.
3. **Fix the family, not the one report.** Audit every direct world report whose key is
   generation-relative — block damage rows (`BlockDamageReport` applies damage to a cell the host may
   have regenerated), trap-layout entries, runtime-entity creation facts — and either give them the
   same identity or record why they are safe without it.
4. **Protocol cost**: this is a wire change, so `ProtocolVersion.Current` bumps and the matrix rows
   owning those messages update with it.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | The air-write report is lost but the drops report arrives in the SAME generation | Accepted (the cell is the current generation's); the drops are registered, not destroyed |
| 2 | A previous layer's break report arrives after the layer change | Refused as stale, logged as such, and the new layer's block is untouched (no damage applied) |
| 3 | The same generation, first-writer-wins | Unchanged: the host's applied air-write record decides, as today |
| 4 | A stale `BlockDamageReport` row for a regenerated cell | Not applied to the new layer's block (either refused as stale or verified against the generation) |
| 5 | Session end / new run | The identity resets with the run baseline; no cross-run attribution |
| 6 | Third-party view | Every peer's block table agrees with the host's after a layer change |

## Non-goals

- Changing the first-writer-wins rule itself.
- The item-domain drop bookkeeping (W1's `PendingBreakDropTable` and its 60 s window).
