# Trap-layout snapshot has no in-session recovery

- Status: Review
- Priority: Medium
- Category: Network / sync coverage / world entities
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row W6, after the independent adversarial review reclassified it from OK)
- Related: `review/runtime-entity-spawn-backfill.md`, `todo/sync-cadence-review.md`, `todo/trap-layout-entry-snapshot-staleness.md`

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
- Runtime-created non-trap entities (`review/runtime-entity-spawn-backfill.md`).

## Landing record (2026-09-18)

**What landed.** The snapshot now has an in-session repair path, and the table it sends is
re-derived from the live host scene first:

- `WorldEntryFanout.SendInSessionRepair(steamId)` owns the periodic in-world repair set
  (block state, block damage, trap layout, kernel checkpoint, runtime-entity snapshot).
  `WorldEventSync`'s 60 s host block calls it once per in-world member, so the layout no
  longer depends on a world-entry edge. The Runtime seam is where a future absolute
  in-world table has to be added: the trap layout was missed for exactly as long as the
  entry group and the cycle's subset were owned in two different places.
- The repair re-derives the table from the LIVE scene before sending:
  `TrapLayoutScanner.RefreshLayout` (host, skipped while generating, run once per cycle and
  only when at least one member is in world) → `IWorldControl.ReplaceTrapLayout` →
  `TrapLayoutRegistry.Replace`. A generation-time record can therefore no longer resurrect a
  trap the world has since removed (a self-destructed turret, a broken crystal).
- `TrapLayoutRegistry.Replace` refuses an EMPTY scan against a non-empty table and reports
  that in its return value, which the scanner logs as a WARNING: inactive and unloaded
  objects are invisible to the scene scan, so the fail-safe direction is a stale entry —
  cleared at the next generation edge, which is also the only escape when a layer genuinely
  emptied — rather than a mass destroy on every guest. The refusing branch is observable in
  the field instead of silent.
- The dead `IKernelProtocolControl` dependency was deleted from `WorldEventSync`,
  `GameAdapterDomains` and `GameAdapter` — its only use was the checkpoint send that moved
  into the repair set.

**Tests.** `TrapLayoutSimulationTests` (11 cases, 6 new): the repair delivers the layout with
no world-entry edge (`InSessionRepair_DeliversTheLayout_WithoutAWorldEntryEdge`); the entry
edge alone delivers nothing, so the repair is load-bearing
(`WithoutTheRepairCall_TheEntryEdgeAloneNeverDeliversTheLayout`); the repair carries every
absolute table the cycle owned, including a checkpoint the GUEST really restores
(`InSessionRepair_CarriesEveryAbsoluteTableTheCycleOwned`, subscribing
`ItemKernelAuthority.CheckpointRestored`); `Replace` drops a removed entry and returns true
(`ReplaceLayout_DropsTheRemovedEntity_SoTheRepairCannotResurrectIt`); the empty-scan fail-safe
REFUSES the replace (`Assert.False`) and the kept entry still rides with its identity
(`ReplaceLayout_EmptyScanAgainstANonEmptyTable_KeepsTheTable`); an empty layout sends nothing
(`InSessionRepair_EmptyLayout_SendsNothing`).

**Verified / not verified.** Machine-checked: the Runtime repair seam, the registry
replace semantics and the wire delivery to a member. NOT exercisable in the test host:
`WorldEventSync.Update` and `TrapLayoutScanner.RefreshLayout` are Unity-typed
(`Time.unscaledTime`, `FindObjectsOfType`, `HarmonyTraverse.IsGenerating`), so the adapter
wiring is code-reviewed and belongs to the unified dual-client acceptance pass.

**Residuals.** (1) The world-entry fanout still sends the table as last derived, so a
member entering between two repairs can materialize an entity the host has since removed
(transient — the next repair makes the guest destroy the surplus): recorded in
`todo/trap-layout-entry-snapshot-staleness.md`. (2) The repair sends the full table per
in-world member per minute; the snapshot is small and absolute, and the first-resend
latency question stays with `todo/sync-cadence-review.md`.

**Review.** The independent adversarial pass ran on the frozen code and reproduced both
commands (10/10 focused at the time, gates green after the evidence re-point). Its findings,
fixed in the same commit: the empty-scan refusal was silent (now `Replace` returns false and the
scanner logs a warning), two of the new cases pinned counts rather than identity (now full
kind/position/prefab assertions), the kernel leg asserted only that an envelope arrived (now a
real checkpoint restore on the guest), and the scene re-derivation now runs only when at least
one member is in world, so the refusal warning cannot be drowned by menu-time scans.

**Evidence.** `docs/evidence/sync-coverage-matrix.md` row W6 → `OK` (48 OK / 7 event-only
/ 0 fallback-only / 9 transient); the gap-list entry is closed with this ticket; the
inline anchors that quoted the moved sends were re-pointed to the new owner
(`WorldEntryFanout.SendInSessionRepair`) and three W6 anchors were added (824 entries).
