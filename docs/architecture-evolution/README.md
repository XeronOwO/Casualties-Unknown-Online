# CUO Architecture Evolution

This directory is the planning and tracking home for the long-term architecture
iteration: replacing scattered authority stores and hook-coupled sync paths with a
typed, deterministic game-state kernel while preserving each gameplay domain's own
semantics.

> Status: **Phase A — completed**. The typed deterministic kernel is running as a
> shadow beside the item path, all 30 item replay files produce zero semantic diff,
> and no online behavior has changed. Phase B is not started per current user scope.

## Why this exists

The current CUO implementation has proven many correct low-level mechanisms
(`DropPendingState`, `CraftingSync`, `HeaterCookSync`, `ItemPendingPickupArbiter`,
replay/simulation harnesses, service splits). The remaining problems are higher-level
fact ownership:

- One item fact is partially stored in `WorldItemTable`, `ItemArbitration._transferred`,
  `CloneFactTable`, Unity `Item`, rollback caches, and periodic snapshots.
- Similar duplication exists in other domains: authority tables, network handlers,
  adapter state, scene objects, and replay logic each keep part of the truth.
- New features therefore have to understand two or more fact models, which is the
  source of many small correctness and continuity issues.

The proposed direction is not generic ECS or full event sourcing. It is a **typed
deterministic kernel** with:

```text
Command -> Decide -> CommittedBatch -> Reduce -> Effects
```

Shared across all domains, while each domain keeps its own models, terms, and
invariants.

## Directory map

| File | Purpose |
|---|---|
| [target-architecture.md](target-architecture.md) | The full target design: kernel, transactions, projections, protocol, save, tests, non-goals. |
| [migration-roadmap.md](migration-roadmap.md) | Short phase-by-phase roadmap and the recommended domain order. |
| [status.md](status.md) | Live tracker: current phase, phase states, last update, handoff pointer. |
| [session-workflow.md](session-workflow.md) | How an independent phase session starts, records evidence, and ends; what must be auto-updated. |
| [architecture-guards.md](architecture-guards.md) | Planned CI/architecture rules that must exist before the dual architecture is accepted. |
| [glossary.md](glossary.md) | Stable vocabulary used across all phase docs. |
| [templates/phase-session.md](templates/phase-session.md) | Reusable session template for a phase or major sub-step. |
| [phase-a-shadow-kernel.md](phase-a-shadow-kernel.md) | Phase A: kernel skeleton and shadow item state. |
| [phase-b-items-authority.md](phase-b-items-authority.md) | Phase B: items become the first authoritative kernel domain. |
| [phase-c-protocol-save-switch.md](phase-c-protocol-save-switch.md) | Phase C: new protocol envelopes and new save format. |
| [phase-d-full-domain-migration.md](phase-d-full-domain-migration.md) | Phase D: migrate the remaining domains in the recommended order. |
| [phase-e-delete-dual-architecture.md](phase-e-delete-dual-architecture.md) | Phase E: delete the dual architecture and legacy surfaces. |

## Phase roadmap

| Phase | Short name | Exit signal | Status |
|---|---|---|---|
| A | Shadow kernel | Replay semantic diff zero for the item slice; shadow explains known defect families; no behavior change. | Completed |
| B | Items authority | Every item fact has one authoritative write path; old tables are projections. | Not started |
| C | Protocol/save switch | New envelopes and checkpoint join pass network simulation; old item wire DTOs removed from production. | Not started |
| D | Full domain migration | All persistent gameplay facts live in kernel domains; epoch isolation works. | Not started |
| E | Delete dual architecture | No `Legacy`/`Compat`/shadow double-write/two authority tables remain. | Not started |

See [migration-roadmap.md](migration-roadmap.md) and the individual phase docs for
entry/exit criteria.

## How to use this area in a new session

1. Read [session-workflow.md](session-workflow.md) first.
2. Read [status.md](status.md) to confirm which phase is active and what handoff was left.
3. Read the active phase doc and [target-architecture.md](target-architecture.md).
4. If the phase is already in progress, continue from its `Session log` / `Next actions`.
5. At the end of any phase work, update the phase doc, `status.md`, and the relevant
   `docs/selfchecks/` fact sheet as described in `session-workflow.md`.

## Non-negotiable direction

- The kernel is authoritative; Unity objects, UI, remote clones, network caches, and
  saves are projections.
- Core gameplay is expressed in typed domain models, never `Dictionary<string, object>`
  or string event names.
- Commands, events, and effects are strictly separated.
- One logical operation produces one atomic committed batch.
- The kernel is deterministic: no Unity time, transforms, random, network state, files,
  or ambient globals unless passed in explicitly.
- Every projection can be dropped and rebuilt from checkpoint + committed batches.
- Phase C and later actively delete old protocol/save compatibility paths; the danger
  is long-term dual architecture, not bold refactoring.
