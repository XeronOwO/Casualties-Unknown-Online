# Migration Roadmap

High-level route from the current architecture to the typed deterministic kernel.
Detailed plans live in each phase document.

## Strategy

The target is bold, but the migration is staged so every phase is runnable and
verifiable. Each domain follows the same repeated pattern:

```text
shadow model → authoritative switch → old tables become projections → delete old state
```

The first domain is **Items**, because it currently exposes the majority of the
architecture problems: multiple fact sources, one operation spread across several hooks,
event/snapshot competition, and scattered special-state handling.

## Phase sequence

| Phase | Theme | Key result | Risk |
|---|---|---|---|
| A | Shadow kernel | Kernel skeleton exists; Items spawn/pickup/drop/destroy run in shadow with zero online behavior change. | Lowest: no production authority switch. |
| B | Items authority | Items become the first authoritative kernel domain; old item tables become projections. | Medium: touches existing item flow; no new wire yet. |
| C | Protocol & save switch | Four envelopes, checkpoint+journal joining, new save format; old item messages/DTOs removed. | Higher: wire and on-disk format change at once; mitigated by simulation. |
| D | Full domain migration | World/Run, Traps/Buildings, Players, Enemies, Fluids migrate in order. | High per domain; each uses shadow→authority→projection→delete. |
| E | Delete dual architecture | All legacy/compat/double-write surfaces removed; architecture tests close the door. | Finishing risk; must not leave dual architecture alive. |

## Recommended phase D domain order

1. World / Run / Epoch — gives every later domain a clean epoch isolation seam.
2. Trap and Building Entity — bounded state machines, good first non-item domain.
3. Player terminal state and cross-player interaction — explicit terminal facts and authority policies.
4. Enemy / Entity — lifecycle, health, targeting/combat terminal facts.
5. Fluid persistent region — authoritative region checkpoint plus rebuildable local simulation.
6. High-frequency stream unification — align continuous fields with the kernel without creating/destroying aggregates.

Each of these follows the same three-step transition and has its own exit criteria
inside `phase-d-full-domain-migration.md`.

## First item slice

The first implementation cycle should stay small:

```text
ItemLocation
ItemState
SpawnItem
PickUpItem
DropItem
DestroyItem
ItemSpawned
ItemRelocated
ItemDestroyed
```

Supporting invariants: unique location, no Terminal resurrection, duplicate Operation
idempotency, wrong-revision rejection, RunEpoch isolation.

Integration approach for the first shadow slice:

- call the new Kernel from beside the existing `ItemMessageFlowService` decision path;
- do not send new network messages;
- compare old and new terminal facts via `DiagnosticsProjection`;
- add new-Kernel assertions to replay tests;
- collect differences and fix the model before changing the old path.

This slice is the highest-information-density proof that the unified kernel concept
works.

## Phase transition rules

- A phase may start only after the previous phase's exit criteria have written evidence.
- A phase may keep temporary legacy code only with a stated reason and a deletion phase.
- No new permanent `Legacy`/`Compat` layer may be introduced after Phase A.
- Phase C removes old wire and save compatibility rather than retaining both forever.
- Phase E is not optional; if dual architecture remains, the iteration is not complete.
