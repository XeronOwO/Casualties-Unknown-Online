# The remaining generation-relative report families

- Status: Todo
- Priority: Medium
- Category: Network / protocol / world generation (attribution of world reports)
- Source: `review/world-layer-generation-identity.md` — the family audit of the cycle that stamped the block-report family; these two families were audited and could NOT be recorded safe, so they are carried here instead of being declared covered
- Related: `review/world-layer-generation-identity.md`, `review/trap-layout-snapshot-recovery.md`, `review/runtime-entity-spawn-backfill.md`

## Problem (evidence)

The block-report family (block state, block damage, partial damage) now carries the kernel run
baseline's `(RunEpoch, LayerIndex)` on the wire and refuses a stale previous-layer report
(`review/world-layer-generation-identity.md`). Two other families were audited in that cycle and are
generation-relative in the same way — their keys are resolved against generated terrain — but they
were not stamped:

1. **Trap layout** (`TrapLayoutSnapshot` / `TrapLayoutEntryMsg`, host → guest): the entries are
   positions of generated entities and the guest MATERIALIZES them. The fan-out is generation-time
   and the 60 s repair re-derives the table from the host's live scene, but a repair landing across
   the guest's own layer change materializes the previous layer's traps into the new one — nothing on
   the message attributes it.
2. **Runtime entity creation** (`EntitySpawnedMsg`, guest → host, with a 60 s re-report): the host
   materializes a copy at the reported position. The reporter's pending table is dropped at its own
   generation boundary, which bounds the exposure to the in-flight window, but the wire carries no
   generation, so the host cannot tell a report of the world it is simulating from one of the world
   the reporter just left.

The families that WERE recorded safe stay safe for the recorded reason (the receiver resolves the
entity through its own live scene and refuses a position where nothing exists —
`EntityEventMsg`, `BuildingEntityDamagedMsg`, `BuildingEntityOpenedMsg`; presentation-only and
item-keyed messages carry no generated-terrain key).

## Goal

Both families carry the same stamp vocabulary as the block-report family (`WorldGenerationMsg` plus
the `Current` / `Stale` / `Unknown` comparison) on the send path that owns the generation, and the
receiving seam refuses a `Stale` one with a precise log naming both generations, before any
materialization or world write.

Mapping the creation report's generation is the NEW work here; the rejection ANSWER that ends a
refused reporter's pending entry already landed (`review/runtime-entity-creation-rejection.md`, decision
161), so row 2's answering clause is a regression guard over that path, not new behaviour.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | A trap-layout repair crosses the guest's layer change | Refused as stale; no previous-layer trap is materialized in the new layer |
| 2 | A runtime-entity creation report crosses the host's layer change | Refused as stale; the host neither creates nor relays it, and the reporter's local copy is answered (its pending report does not re-report forever) |
| 3 | Same generation | Unchanged: the entry materializes exactly as today |
| 4 | No stamp / no baseline | Pre-stamp behaviour (UNKNOWN is never treated as fresh) |
| 5 | Third-party view | Every peer's trap/entity world agrees with the host's after a layer change |

## Non-goals

- Re-stamping the families the previous cycle already covered or recorded safe.
- A shared frame-level generation header: the transport carries no world concept, and the previous
  cycle recorded why the per-message carrier was chosen (`review/world-layer-generation-identity.md`).
