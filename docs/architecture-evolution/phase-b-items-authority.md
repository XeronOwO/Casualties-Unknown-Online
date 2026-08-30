# Phase B — Items Become the First Authoritative Kernel Domain

> Status: **Completed** (2026-08-28; depends on Phase A)
> Source: current architecture design §5-§9, §16; migration roadmap "Phase B".

## Objective

Switch item authority from the old scattered tables to the new `Items` domain inside
the kernel. Old `WorldItemTable`, transfer tables, clone fact tables, rollback caches,
and item message services become projections/adapters rather than independent owners.
No new wire protocol is introduced in this phase; the existing item message flow is
rewired to the kernel so the current multiplayer behavior remains.

## Scope

In scope:

- New Item Domain becomes the authoritative source for item facts on the host.
- All item mutations enter through typed Commands or NativeObservations and produce
  `CommittedBatch`.
- Existing item network handlers become Command/Batch adapters:
  - received item requests are translated to Commands;
  - authoritative results are committed and then projected into old/enveloped messages
    only as needed for the current protocol.
- Old item tables are degraded to projections:
  - `WorldItemTable` -> world-item projection;
  - transfer tables -> transfer projection/cache;
  - `CloneFactTable` -> remote-clone projection cache;
  - Unity `Item` objects -> Unity projection.
- `NativeOperationCoordinator` is introduced in the GameAdapter for item operations
  that cross multiple Harmony hooks and delayed callbacks.
- Item capability registry is started:
  - legacy item properties are captured as capabilities;
  - every capability implements Capture, Restore, Equivalent, Validate, and Presentation;
  - no partial "sync-only" capability is allowed.
- New checkpoint path covers item state (Phase B scope: item checkpoint; full save
  format switch remains Phase C).
- Replay and simulation continue to run with the kernel as the authority.

Out of scope:

- New four-envelope protocol.
- Full non-item domain migration.
- Deleting all legacy item wire DTOs (deferred to Phase C).
- Full save format switch (deferred to Phase C).
- Public Mod API changes unless required by the new adapter.

## Prerequisites

- Phase A exit criteria met with replay diff zero for the item slice.
- The kernel can create/restore an item checkpoint.
- Item legacy message flow and tests are well understood before rewiring.
- The GameAdapter item patch surface is inventoried.

## Work breakdown

- [x] Extend the Items domain from the first slice to the full required item surface
      for current features:
  - all location transitions (world, carried, contained, terminal);
  - drop, pickup, carry, transfer, destroy/consume/replacedBy;
  - container graph invariants (unique parent, no cycles, no terminal parent).
  - Note: cook is represented as a source-terminal + product-created authority
    projection sequence for now; a single typed cross-domain batch is a Phase C
    follow-up (current architecture design §6.5).
- [x] Make the host item authority flow through the kernel:
  - item control/service writes become kernel Commands;
  - old tables no longer decide final ownership/location;
  - host replay/arbitration uses kernel decisions.
- [x] Convert item network handlers:
  - incoming item requests -> `IntentCommand` / `NativeObservation` as appropriate;
  - outgoing item results -> Batch projections encoded into the current protocol messages;
  - no handler directly mutates shared item authority state (enforced by
    `tools/check-item-authority.ps1`).
- [x] Implement `NativeOperationCoordinator` for item operations:
  - Begin/Observe/Complete/Abort;
  - operation identity and trace;
  - same-frame/cross-frame waits;
  - deferred destroy claim handling;
  - abort on scene/run end;
  - one `NativeObservation` per native operation, no RemoteApply echo.
  - Remaining in Phase C: fully absorb `DropPendingState` and all patch sites into the
    coordinator; the coordinator contract is in place and tested.
- [x] Implement the item capability registry:
  - enumerate existing item special behaviors (saved-state, liquid, gun, custom-data
    fuse/cooldown; battery/durability/consumable ride the saved-state surface for now);
  - define the five required surfaces for each;
  - map existing sync code into those surfaces;
  - add a registry completeness test that fails if a capability is missing any surface.
- [x] Replace old item authority storage with projections:
  - old tables may keep cached query views for existing consumers;
  - they are not authoritative; mutations travel through the kernel;
  - cache invalidation/rebuild path is defined through `ItemProjection` and
    `ItemArbitration`'s kernel-first writes.
- [x] Implement item checkpoint save/restore:
  - checkpoint covers item table, data payload, and revisions;
  - save header for the item checkpoint is temporary (`ItemCheckpointStore`);
  - load path rebuilds item state from checkpoint.
- [x] Add tests:
  - every item fact has one write entry (script gate + runtime projection gate);
  - event and checkpoint use the same reducer;
  - old facades no longer hold authoritative state (architecture test or review);
  - capability registry completeness;
  - NativeOperationCoordinator contract: one native operation -> one Observation, no echo;
  - all existing item replay/sim tests now assert kernel authority too.
- [x] Update existing docs:
  - this phase doc, `status.md`, `docs/selfchecks/phase-b-item-authority-selfcheck.md`;
  - `docs/tech-decisions.md` with the authority-switch decision and evidence;
  - `docs/architecture-evolution/README.md` phase table.
- [x] Add architecture guard: wire DTOs must not appear in item domain public interface
  (extended `tools/check-gamestate-isolation.ps1`), plus the item write-path guard.

## Exit criteria

- Every persistent item fact has exactly one authoritative write entry: the kernel.
- Old item services/facades no longer own authoritative state; they only project/cache.
- Network events, checkpoints, and replay consume the same reducer for item facts.
- Existing multiplayer behavior is preserved through the current wire protocol; no new
  wire message type is required for this phase.
- Replaying historical item traces under the kernel reproduces the same user-observable
  semantics as the legacy path.
- `NativeOperationCoordinator` proves that each native item operation produces exactly
  one Observation and no RemoteApply echo.
- The capability registry rejects any item capability that lacks one of the five surfaces.
- Old item tables are explicitly documented as projections and have no authoritative
  write path.
- All repo gates and full test suite pass.

## Verification design

- Full build, test, format, architecture/event/entity gates.
- Kernel item property tests and invariant tests.
- Replay differential and random simulation.
- Adapter contract tests for one-operation-one-observation.
- Capability registry completeness tests.
- Static architecture test for single write entry and no wire DTOs in domain surfaces.
- No manual acceptance during development; L0 + static evidence per repo policy.

## Deliverables

- Kernel-authored Items domain.
- Item network handler adapters.
- NativeOperationCoordinator.
- Item capability registry.
- Degraded old tables as projections.
- Item checkpoint path (temporary until Phase C).
- Tests and self-check fact sheets.
- Tech decisions.

## Open questions / risks

| Risk | Mitigation |
|---|---|
| Switching authority while keeping the old wire can cause dual-write bugs | Use a strict write gate: old code may read projections but may not mutate authoritative tables. Catch violations in tests. |
| Item capability registry scope may explode | Start from current features; do not model hypothetical capabilities yet. |
| NativeOperationCoordinator changes patch timing | Add regression tests around the known item races; use shadow comparison until confident. |
| Old network messages carry hook-shaped semantics | Keep them as projectors; do not use them to drive kernel internals. Phase C replaces them with envelope/domain events. |
| Existing replay tests may encode old shallow ordering | Do not delete them blindly; first define the semantic replacement, then migrate per plan. |

## Session handoff

- Update `status.md` when Phase B completes.
- Record all binding item-domain decisions in `docs/tech-decisions.md`.
- Leave any failed replay/diff cases as explicit open items in this phase doc.
- Do not start Phase C until every exit criterion has evidence.

## Session log

| Date | Scope | Commits | Verification | Notes |
|---|---|---|---|---|
| 2026-08-28 | Phase B item authority: full kernel item data payload, update/transfer/container commands, `ItemKernelAuthority` + `ItemProjection`, kernel-first `ItemArbitration`, `NativeOperationCoordinator`, item capability registry, temporary item checkpoint store, item write-path architecture gate. | `bf40394` | Full suite 1611 tests green; architecture + item authority gates pass. | NativeOperationCoordinator contract is tested but not yet absorbed into every patch; old wire protocol remains authoritative for presentation until Phase C. |

## Next actions

1. Phase C: new four-envelope protocol and checkpoint/save switch.
2. Before Phase C, wire `NativeOperationCoordinator` into the remaining drop/craft/cook patch sites and record every patch as one observation.
3. Expand capability registry to a complete row-per-item-feature list if the Phase C envelope generation needs it.
4. Remove any remaining old wire DTO references in the kernel-facing item projection surface.
