# Phase C — Structural Protocol and Save Switch

> Status: **Not started** (depends on Phase B)
> Source: target architecture §11-§13; migration roadmap "Phase C".

## Objective

Replace the old item-centric/hook-shaped wire messages and old save object graph with
the new envelope protocol and kernel-checkpoint save. This is the first phase allowed
to break old protocol and old save compatibility. After this phase, production paths
must use the new four-envelope model and new checkpoint save format.

## Scope

In scope:

- New protocol envelopes:
  - `CommandEnvelope`;
  - `CommittedBatchEnvelope`;
  - `CheckpointEnvelope`;
  - `StateStreamEnvelope`.
- Common header fields:
  - `ProtocolVersion`;
  - `RunEpoch`;
  - `SenderId`;
  - `MessageId`;
  - `OperationId` when applicable;
  - `BaseGlobalRevision`;
  - `PayloadType`.
- Wire schemas are domain events, not hook names:
  - item facts become domain event payloads such as `ItemRelocated`,
    `ItemsTransformed`, etc.;
  - dispatch no longer depends on which Harmony hook fired.
- Join flow:
  - host sends checkpoint at revision N;
  - host sends checkpoint chunks;
  - host sends batches N+1..M;
  - guest restores checkpoint, applies tail, sends Ready(M);
  - host starts normal Batch/Stream.
- Gap recovery:
  - guest requests revision ranges;
  - host sends journal ranges;
  - host falls back to a fresh checkpoint when the requested range is outside its window.
- Prediction/replay integration:
  - guest applies confirmed batches then replays pending predictions;
  - projection rebuild from checkpoint/query after projection failure.
- New save format:
  - `SaveHeader` (SchemaVersion, GameBuild, ModBuild, RunEpoch, GlobalRevision, CreatedAt);
  - `GameCheckpoint` (World, Players, Items, Entities, Traps, Fluids, RandomStreams);
  - checkpoint is authoritative; recent batches only diagnostic tail;
  - named random streams or decided results must be saved;
  - old saves are rejected or migrated only if an explicit migrator exists; no old DTO
    pollution in the new model.
- Remove old production item message family and compatibility code:
  - old item DTOs and old message enum entries are removed from production paths;
  - any remaining reference is test-only or explicitly not in the shipped path.
- Versioning:
  - envelope version;
  - checkpoint schema version;
  - event payload numeric IDs;
  - unknown critical payload -> disconnect/report;
  - unknown non-critical presentation effect -> ignore;
  - golden wire contract tests.

Out of scope:

- Full non-item domain migration (Phase D).
- Mod API surface redesign beyond protocol/tooling needs.
- Anti-cheat or strict validation (remains low priority).
- Permanent compatibility with old saves/protocol.

## Prerequisites

- Phase B exit criteria met: item authority is in the kernel; old tables are projections.
- Protocol and save formats are still internal; breaking allowed.
- Network simulation harness is ready or can be extended before wire cut-over.
- Existing replay traces can be translated to the new event names if needed.

## Work breakdown

- [ ] Define Protocol project:
  - wire DTOs for the four envelopes and headers;
  - codecs;
  - version constants;
  - golden byte-level contract tests.
- [ ] Define domain event wire IDs:
  - numeric payload types;
  - mapping from kernel Events to wire payloads;
  - rejection table for unknown critical vs unknown non-critical.
- [ ] Implement host-side send path:
  - Commands from guests;
  - committed batches from kernel;
  - checkpoint chunks;
  - state stream envelope for continuous fields.
- [ ] Implement guest-side receive path:
  - apply batches idempotently by OperationId/GlobalRevision;
  - request missing ranges;
  - rebuild from checkpoint on large gaps;
  - replay pending predictions after confirmed batch application;
  - produce minimal projection diffs.
- [ ] Implement join/reconnect flow:
  - checkpoint + tail;
  - late join after journal window expiry;
  - disconnect/reconnect with epoch validation.
- [ ] Implement RunEpoch filtering:
  - every envelope carries RunEpoch;
  - old-epoch packets are dropped;
  - session reset converges to kernel restore.
- [ ] Implement save/load:
  - write `SaveHeader` + `GameCheckpoint`;
  - read/validate/migrate/reject old saves per policy;
  - include named random streams / decided random results;
  - keep checkpoint as the only authoritative on-disk source.
- [ ] Remove old item wire DTOs from production:
  - identify all old item message usages;
  - replace with new envelopes or projections;
  - delete old message handlers/enums where no longer used;
  - keep golden tests updated.
- [ ] Add network simulation tests:
  - random latency, duplication, reordering, loss, disconnect, reconnect;
  - checkpoint insertion;
  - reliable Batch eventual consistency;
  - state stream convergence only.
- [ ] Add save round-trip tests:
  - checkpoint equivalence;
  - random stream determinism;
  - corrupt old save behavior;
  - schema version handling.
- [ ] Add projection rebuild tests:
  - all projections can be dropped and rebuilt from checkpoint+journal tail;
  - failed projection does not mutate authoritative state.
- [ ] Update docs:
  - `docs/tech-decisions.md`;
  - `docs/selfchecks/`;
  - `docs/architecture.md` if protocol sections become obsolete;
  - this phase doc and `status.md`.

## Exit criteria

- New four-envelope protocol is the only production network path.
- Late join, reconnect, gaps, duplicates, and out-of-order batches all pass network
  simulation without invariant violations.
- Old item message family and old compatibility code are removed from production paths.
- New checkpoint save is the only production save path; old saves do not silently load
  into the new model.
- Kernel events, checkpoints, and replay share the same reducer.
- Projection failure handling does not mutate authoritative state.
- Unknown critical wire payload causes a protocol-incompatibility disconnect; unknown
  non-critical presentation effect is ignored.
- Golden wire tests exist and pass.
- Full test suite and repo gates pass.

## Verification design

- Full build/test/format/gates.
- Network simulation under virtual time.
- Save/load round-trip and migration tests.
- Wire golden tests with deterministic bytes.
- Replay differential heritage tests for user-observable item semantics.
- L0 + static evidence, no manual acceptance.

## Deliverables

- Protocol project with envelopes and codecs.
- Host/guest sync paths.
- Join/reconnect/journalling.
- RunEpoch filtering.
- New save format and migrator/rejection.
- Removed old item wire paths.
- Network simulation and save tests.
- Self-check fact sheets and tech decisions.

## Open questions / risks

| Risk | Mitigation |
|---|---|
| Large wire cut-over is risky | Keep Phase B behavior stable, then move wire in one phase with simulation; no incremental half-wire production state. |
| Journal window sizing is unknown | Start with a generous window and measure; define fallback checkpoint behavior as the safety net. |
| Event payload IDs become a burden if late | Assign numeric IDs for critical kernel events immediately; keep a reserved range for future non-critical effects. |
| Old replay traces may be hook-shaped | Translate only user-observable semantic facts; golden tests use domain event payloads. |
| Save migration may be tempting | Do not carry old DTOs into the new domain models; reject old saves if migrator is not trivial. |

## Session handoff

- Mark Phase C complete only after network simulation and old-path removal evidence.
- Update `status.md` with the new protocol/save commit references.
- Leave the remaining domain list for Phase D.
- Delete or archive any old protocol design docs that are no longer true.

## Session log

| Date | Scope | Commits | Verification | Notes |
|---|---|---|---|---|
| _(none yet)_ | | | | |

## Next actions

1. Read Phase B completion evidence in `status.md` and `docs/selfchecks/`.
2. Design the Protocol project and wire event ID table first.
3. Implement host/guest join flow in a simulation-only branch before switching production.
