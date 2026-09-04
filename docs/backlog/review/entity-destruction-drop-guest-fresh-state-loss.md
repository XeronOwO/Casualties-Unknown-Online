# Entity destruction drops lose fresh-drop presentation/initial motion on the guest view

- Status: Review
- Priority: Medium
- Category: Item sync / entity destruction presentation
- Source: User report (2026-09-04) — when the host destroys an entity/building, the dropped items on the guest view have no highlight and no no-gravity/fresh-drop feel, and the guest sees the item fall then get pulled back. Implemented in the 2026-09-04 cycle; waiting for unified acceptance.

## Goal

Make destruction drops from building/trap/entity death appear on every peer with the same fresh-drop presentation and the same initial motion phase as the attacker/host side, without causing the guest to first fall/step and then be yanked back by the authority stream.

## Current behavior / suspected root cause

- Block-break drops have a dedicated chain that preserves the full initial drop state:

  - `src/CasualtiesUnknownOnline.GameAdapter/World/BlockBreakSync.cs` and `src/CasualtiesUnknownOnline.Runtime/Session/Items/BlockDropSync.cs:60-62` — `BlockDropEntryMsg` is converted directly to a `WorldItem` including `FreshItemDrop`, position, velocity, rotation and angular velocity.

- Building/entity destruction drops use the atomic trap/building-death path instead:

  - `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemWorldSync.cs:215-241` — a building-death drop is captured into `TrapDropEntryMsg` with `FreshItemDrop`, position, velocity, rotation and angular velocity.
  - `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/TrapDropEntryMsg.cs` — the full initial drop state is present on the wire.
  - `src/CasualtiesUnknownOnline.Runtime/Session/World/TrapStateRegistry.cs:123-137` — the host folds those drops into the atomic kernel batch by creating only `SpawnItemCommand`s; the spawn command carries `ItemData` (save-shaped item state) but not `FreshItemDrop`, velocity, rotation or angular velocity.
  - `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelBatchItemProjection.cs:353-365` — `ToWorldItem` materializes the projected world item with `FreshItemDrop = false`, zero velocity, zero rotation.
  - `src/CasualtiesUnknownOnline.GameAdapter/World/EntityEventSync.cs:133-141` — on a host→guest entity-event relay, the guest only replays the trap/entity presentation; it does not materialize `EntityEventMsg.Drops` directly. The guest's dropped items are materialized from the kernel projection instead.

- The state stream (`WireItemMoveEntry`) later supplies position/velocity/rotation to the guest through `ItemPositionFollow`, but it does not restore the transient `FreshItemDrop` presentation flag. A newly projected guest item therefore starts without the glowing/floating fresh-drop effect and without the same initial physics phase; the later authority correction reads as a drop-then-pull-back.

- Relevant code:
  - `src/CasualtiesUnknownOnline.GameAdapter/Items/RemoteItemSceneOps.cs:347-406` — guest materialization; `FreshItemDrop` is added only when `WorldItem.FreshItemDrop` is true, and guest items are frozen until the first stream tick.
  - `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemPositionFollow.cs:129-176` — first stream tick switches a frozen guest item to local physics and aligns it to the host.
  - `src/CasualtiesUnknownOnline.Protocol/Wire/WireWorldItemState.cs:39` — the full state-stream snapshot does carry `FreshItemDrop`, but the kernel-projected world item used to build that snapshot already has it set to `false`.

## Missing behavior on the guest view

- No fresh-drop highlight/glow effect (`FreshItemDrop` component is not attached).
- No fresh-drop no-gravity/floating feel (as described by the user; needs runtime confirmation, but it points at the same missing `FreshItemDrop` presentation path).
- The item can fall locally and then be snapped/pulled back when the host's authority stream arrives, because the guest materializes at a different initial phase than the host.

## Required design direction (for the implementation cycle)

- Preserve the full transient spawn state for entity/building destruction drops across the wire/projection path without making continuous physics a kernel fact:
  - carry `FreshItemDrop`, initial velocity, rotation and angular velocity alongside the `SpawnItemCommand` / projected `WorldItem`, or
  - let the guest materialize the drop directly from `EntityEventMsg.Drops` during replay (the full state is already in the message), while the kernel remains the authoritative item fact source.
- Ensure the guest's materialized drop is marked fresh and starts frozen at the exact host spawn state, then switches to local physics on the first `ItemPositionFollow` tick.
- Keep the block-drop path working; do not regress its existing fresh-drop behavior.
- Add regression/runtime evidence for both host-triggered and guest-triggered entity destruction, and for a third-party peer view.

## Implementation (2026-09-04)

- Added `InitialDropStateMapper` (Runtime) as the single pure mapping for both
  block and trap/building initial-drop entries; `BlockDropSync` now reuses it.
- Added `ItemApplication.ApplyTrapDropPresentation`, which materializes a
  missing drop with the event's full state or enriches an already-projected
  world item with the missing fresh/velocity/rotation/angular-state facts.
- `EntityEventSync` now calls that path on the host-apply branch (guest-triggered
  traps) and on the guest-replay branch (host-triggered & third-party views).
- No wire/protocol change; `EntityEventMsg.Drops` remains the transient
  presentation source.
- Evidence: `docs/evidence/selfchecks/items/entity-destruction-drop-fresh-presentation-selfcheck.md`.

## Acceptance criteria (for the later implementation cycle)

- On a guest (and any other peer), entity destruction drops show the same fresh-drop highlight/floating presentation as the host/attacker view.
- The guest drops do not visibly fall-then-pull-back; they start at the same position/velocity phase and are later softly corrected by the normal item position stream.
- The fix works for destructive trap/building deaths, not only block breaks.
- Existing item/entity sync tests and repo gates remain green.
- No wire or authority regression; if a new field is needed, it must be documented and versioned appropriately.

## Non-goals

- Not adding continuous item physics into the kernel.
- Not changing entity/trap authority or the atomic composite design.
- No wire/protocol change was introduced; the existing entity-event drop payload is the transient presentation source.
