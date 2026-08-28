# Phase C — Structural Protocol and Save Switch

> Status: **In progress** (depends on Phase B; core protocol/save stack landed, production cutover continues)
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

- [x] Define Protocol project:
  - wire DTOs for the four envelopes and headers;
  - codecs;
  - version constants;
  - golden byte-level contract tests.
- [x] Define domain event wire IDs:
  - numeric payload types;
  - mapping from kernel Events to wire payloads;
  - rejection table for unknown critical vs unknown non-critical.
- [ ] Implement host-side send path:
  - [x] Commands from guests;
  - [x] committed batches from kernel;
  - [x] checkpoint chunks;
  - [x] state stream envelope for continuous fields (item move stream).
- [ ] Implement guest-side receive path:
  - [x] apply batches idempotently by OperationId/GlobalRevision;
  - [x] request missing ranges;
  - [x] rebuild from checkpoint on large gaps;
  - [ ] replay pending predictions after confirmed batch application;
  - [x] produce minimal projection diffs (world items from kernel batches).
- [ ] Implement join/reconnect flow:
  - [x] checkpoint + tail;
  - [x] late join after journal window expiry (fallback checkpoint on out-of-window range);
  - [ ] disconnect/reconnect with epoch validation.
- [x] Implement RunEpoch filtering:
  - every envelope carries RunEpoch;
  - old-epoch packets are dropped;
  - session reset converges to kernel restore.
- [ ] Implement save/load:
  - [x] write `SaveHeader` + `GameCheckpoint`;
  - [x] read/validate/reject old saves per policy;
  - [x] include named random streams / decided random results;
  - [x] keep checkpoint as the only authoritative on-disk source.
- [ ] Remove old item wire DTOs from production:
  - [ ] identify all old item message usages;
  - [x] replace spawn/pickup/drop/destroy with new envelopes;
  - [x] replace item move with StateStreamEnvelope;
  - [ ] delete old message handlers/enums where no longer used;
  - [x] keep golden tests updated.
- [ ] Add network simulation tests:
  - [x] duplication and idempotency;
  - [x] reordering/gap/epoch rejection;
  - [x] random latency and duplicate convergence;
  - [x] disconnect/reconnect simulation (checkpoint restore);
  - [x] checkpoint insertion/restore;
  - [x] reliable Batch eventual consistency (kernel state + world projection);
  - [x] StateStream convergence (item move).
- [x] Add save round-trip tests:
  - checkpoint equivalence;
  - [x] random stream determinism;
  - [x] corrupt old save behavior;
  - [x] schema version handling.
- [x] Add projection rebuild tests:
  - [x] world projection dropped and rebuilt from checkpoint;
  - [x] failed projection does not mutate authoritative state.
- [ ] Update docs:
  - [x] `docs/selfchecks/`;
  - [x] this phase doc and `status.md`;
  - [ ] `docs/tech-decisions.md`;
  - [ ] `docs/architecture.md` if protocol sections become obsolete.

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
| 2026-08-28 | Phase C core: `CasualtiesUnknownOnline.Protocol` project (four envelopes, wire DTOs, codecs, golden tests), `KernelWireMapper`, `WireCheckpointAssembler`, `KernelProtocolService` + `KernelEnvelopeHandler`, `KernelSaveFileStore`, guest world projection, RunEpoch/version/gap filters, checkpoint join hook. Old spawn/pickup/drop/destroy production sends switched to CommandEnvelope. | `755bc52` (re-signed; original unsigned `884ecc3` preserved on `backup/pre-resign-phase-c-20260828`) | Full suite 1637 tests green; build/architecture/event/entity gates pass. | Remaining: full old item DTO removal, request-missing-ranges/rebuild, StateStream projection, random streams, projection-rebuild tests, remaining tech-decision/doc fields. |
| 2026-08-28 | Phase C recovery/save/projection second cycle: guest range requests + out-of-order buffering + host journal fallback checkpoint, named random streams in GameCheckpoint/wire/save, checkpoint rebuild of guest world projection, latency/duplicate simulation. | `8db5105` (re-signed; original unsigned `acaceac` preserved on backup branch) | Full suite 1643 tests green; architecture/event/entity gates pass. | Remaining: full old item DTO removal, StateStream projection, disconnect/reconnect simulation, failed-projection-must-not-mutate test. |
| 2026-08-28 | Phase C StateStream cycle: item move host→guest now rides `StateStreamEnvelope` and resurface as `ItemMoveReceived`; disconnect/reconnect checkpoint-restore test added. | `a2ffdf5` (re-signed; original unsigned `809a808` preserved on backup branch) | Full suite 1644 tests green; architecture/event/entity gates pass. | Remaining: old item families use/slot/container/correction/carry/snapshot/cook, failed-projection test, final docs/tech-decisions. |

## Next actions

1. Convert the remaining old item wire families (use/slot/container-content/snapshot/correction/carry/cook) to envelopes or explicitly keep them as test-only projections.
2. Add a failed-projection-does-not-mutate-authoritative-state test.
3. Record remaining Phase C decisions in `docs/tech-decisions.md`, update `docs/architecture.md` if protocol sections are stale, and commit.
