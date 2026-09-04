# Guest carried container contents periodically appear as world drops on the host view (dog food in trash bag)

- Status: Todo
- Priority: Medium
- Category: Item/container sync / remote presentation
- Source: User report (2026-09-04) — a guest puts dog food into a trash bag, then while moving the host periodically sees a can of dog food drop from the guest's body; the guest's own view does not see it. Record only; no code action taken yet.

## Goal

Eliminate the host-only periodic dog food "drop" from the guest's carried container. The nested dog food should remain inside the trash bag in every view and in the authoritative item state; no ghost world item should appear on the host.

## Current context / likely suspect areas

- Guest puts the dog food into a trash bag (container). If the trash bag is a carried/world container, the container-content/carry chain owns the nested item's fact:
  - `src/CasualtiesUnknownOnline.GameAdapter/Items/ContainerItemSync.cs` — body-internal container moves report the parent container's full recursive capture; world-container loads use `ItemDropped` with `ParentItemId`.
  - `src/CasualtiesUnknownOnline.GameAdapter/Items/PickupSync.cs:105-131` — picking up a container explicitly reports each child content as picked up so the world table does not keep ghost entries ("the bag swallowed its contents / the dog food came back as a separate item").
  - `src/CasualtiesUnknownOnline.GameAdapter/Character/CloneFactTable.cs` and `CloneInventoryRenderer.cs` — remote clone carried/container contents are display proxies.
- A host-side periodic ghost/drop suggests one of:
  1. The dog food still has a stale world-table/kernel/projection entry or transfer-table entry after entering the container, and a periodic item snapshot/projection materializes it as a world item at/near the guest.
  2. A container-content sync or 1 Hz character snapshot is misapplied and re-adds the nested dog food as a top-level/world item on the host side.
  3. The host's remote clone renderer temporarily treats a nested display-proxy dog food as a standalone world object (e.g., it is unparented or not correctly marked), and the item domain or scene sees it as a drop.
  4. The guest's carried container state and the host's transfer table/character snapshot diverge during movement, and a correction/snapshot is projected as a world drop.

## Evidence to collect during implementation

- Host-side runtime log around the moment the ghost dog food appears:
  - is it materialized from `ItemSpawned` / `ItemDropped` / snapshot reconcile, or is it just a clone render object?
  - does it carry an `ItemInstanceId` and is it a world item (`IsStandaloneWorldItem`)?
  - which item id / event triggered it, and does the guest also receive that event?
- Confirm the exact sequence on the guest:
  - putting dog food into a world container vs a carried/body container,
  - picking up a container with contents,
  - moving afterward.
- Check whether the ghost appears after a periodic item keyframe/state stream, after a 1 Hz character snapshot, or after a container-content event.

## Required design direction (for the implementation cycle)

- Ensure every container-content transition completely removes the child from all world/projection tables:
  - world → carried container pickup,
  - carried container internal move,
  - world container load/unload,
  - reconnect/save restore.
- Preserve the kernel invariant that a contained item has exactly one parent and is never projected as a standalone world item.
- Keep remote clone display proxies out of the item domain; if any path still instantiates/restores nested clone contents without suppression, fix it.
- Add logs or tests that reproduce the exact host-side ghost and verify the nested dog food remains in the parent in all views.

## Acceptance criteria (for the later implementation cycle)

- No periodic dog food can appears as a world drop on the host while the guest carries the trash bag/container.
- The guest view and host view show the dog food inside the container consistently.
- No duplicate/ghost item id, no transfer-table resurrection, no dropped item after reconnection.
- Existing container/item sync tests and repo gates remain green.

## Non-goals

- Not changing the game's container behavior outside CUO sync.
- Not implementing in this cycle — this ticket is a backlog record only.
