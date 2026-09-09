# Trap-layout snapshot has no in-session recovery

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / world entities
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row W6, after the independent adversarial review reclassified it from OK)
- Related: `todo/runtime-entity-spawn-backfill.md`, `todo/sync-cadence-review.md`

## Problem (evidence)

`TrapLayoutSnapshot` (NetMsg 79) is the host's authoritative generated trap/mechanism
layout. It is sent only on a world-entry edge:

- Only send call site: `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntryFanout.cs:39`
  (inside the ordered group), via
  `src/CasualtiesUnknownOnline.Runtime/Session/World/EntityEventChannel.cs:341`
  (`public void SendTrapLayoutSnapshot(ulong targetSteamId) => _trapLayout.SendSnapshot(targetSteamId);`)
  → `src/CasualtiesUnknownOnline.Runtime/Session/World/TrapLayoutRegistry.cs:55`.
- The 60 s host cycle does **not** include it: `src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:129-135`
  re-sends block state, block damage, the kernel checkpoint and keypad codes only.
- The kernel checkpoint does not carry the layout (no `TrapLayout` type exists anywhere in
  `src/CasualtiesUnknownOnline.GameState`), so the kernel fallback cannot heal it either.
- Recovery is therefore edge-driven: world entry, layer regeneration, reconnect or
  death → re-entry. While a member stays continuously in the world there is no in-session
  repair, and the apply is partial by design (an entry whose prefab name does not load is
  skipped with a trace).

## Goal

A swallowed trap-layout snapshot converges while the member stays in the world, and the
layout stays host-authoritative (materialize missing, destroy surplus — never resurrect an
entity the world has since removed).

## Design direction (decide at implementation)

1. **Piggyback on the existing 60 s cycle** — add `SendTrapLayoutSnapshot` to the same
   in-world member loop as the block-state resend (small, absolute, idempotent).
2. **On-demand repair** — the guest reports a layout hash / missing-entry request and the host
   answers with the snapshot.
3. **Accepted loss** — rejected: a missing or surplus trap changes gameplay (trigger, damage,
   consumption).

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Host layout differs; the world-entry snapshot is dropped | Guest converges without an InWorld edge |
| 2 | Late joiner | Layout applied exactly once |
| 3 | Reconnect while in world | Same; no duplicates |
| 4 | Layer regeneration | New layout replaces the old one; no stale entries |
| 5 | Entity destroyed after the snapshot | Not resurrected |
| 6 | Prefab name fails to load | Skipped with a trace (existing partial-apply semantics preserved) |
| 7 | Third-party guest | All peers converge to the same layout |

## Non-goals

- Changing the deterministic generation or the physics-query divergence that makes the
  snapshot necessary.
- Runtime-created non-trap entities (`todo/runtime-entity-spawn-backfill.md`).
