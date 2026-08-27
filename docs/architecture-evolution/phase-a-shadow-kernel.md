# Phase A — Shadow Kernel

> Status: **Not started** (docs-only plan, baseline `208df31`)
> Source: target architecture §5-§9; migration roadmap "First item slice".

## Objective

Prove the unified kernel model without changing online behavior. Build the smallest
useful `GameState` kernel and run an Items shadow slice alongside the existing path.
The old path remains authoritative; the new kernel only observes/executes and produces
diagnostic comparisons.

This is the lowest-risk phase and the highest-information test of whether the proposed
architecture actually explains the current item defect families.

## Scope

In scope:

- New `CasualtiesUnknownOnline.GameState` project or equivalent project skeleton with
  no references to Unity, Runtime, Protocol codecs, BepInEx, Steam, or network packages.
- Minimal kernel mechanisms:
  - Command routing/dispatcher;
  - transaction working copy;
  - domain Decide/Reduce separation;
  - atomic Batch commit;
  - aggregate + global revisions;
  - operation idempotency;
  - minimal checkpoint seam.
- Items shadow domain, first slice only:
  - `ItemState` with Identity (InstanceId + DefinitionId), Revision, Location
    (`World`, `Carried`, `Contained`, `Terminal`);
  - `SpawnItem`, `PickUpItem`, `DropItem`, `DestroyItem`;
  - `ItemSpawned`, `ItemRelocated`, `ItemDestroyed` events;
  - invariants: unique location, no Terminal resurrection, duplicate Operation
    idempotency, wrong-revision rejection, RunEpoch isolation skeleton.
- Shadow integration: invoke the new kernel beside the existing item decision path,
  without sending new wire messages.
- Diagnostics projection: compare old terminal facts (WorldItemTable + transfer table +
  clone/rollback facts) with new ItemState, and record semantic differences.
- Tests: kernel contracts, item invariants, property-based random operations, existing
  item replay/race/simulation traces with additional kernel assertions.

Out of scope:

- Authority switch to the kernel.
- New network protocol envelopes.
- New save format for production.
- Deleting old item tables, old facades, or old wire messages.
- Full capability registry beyond what the first item slice needs.
- Any non-item domain.

## Prerequisites

- The architecture direction is approved (this documentation set).
- Existing item tests and replay harnesses are green at the baseline.
- A clear list of historical item defects is available to validate the shadow model
  (from `docs/selfchecks/`, `docs/tech-decisions.md`, `docs/backlog.md`).

## Work breakdown

- [ ] Create the `GameState` project with a placeholder/prototype assembly.
- [ ] Add project-level isolation rule and a build/CI check that `GameState` does not
      reference forbidden assemblies.
- [ ] Define the core C# contracts:
  - `IGameStateKernel` (Execute/Apply/CreateCheckpoint/Restore/Query);
  - `IDomainModule<TCommand>` (Decide/Reduce/AssertInvariants);
  - typed `GameCommand`, `CommandContext`, `Decision`, `CommittedBatch`,
    `GameEvent`, `GameCheckpoint`, and rejection types.
- [ ] Implement the minimal transaction loop:
  - working copy;
  - event draft collection;
  - deterministic reduce;
  - precondition/revision validation;
  - global revision assignment;
  - atomic swap;
  - batch publication.
- [ ] Implement the Items domain first slice:
  - `ItemLocation` variants;
  - `ItemState`;
  - Spawn/PickUp/Drop/Destroy commands and reducers;
  - item aggregate revision;
  - location/container skeleton invariants.
- [ ] Implement a minimal checkpoint for the shadow item table.
- [ ] Integrate as a shadow:
  - identify the existing `ItemMessageFlowService` / item decision path;
  - send the same accepted/observed facts into the kernel as Commands or
    NativeObservations;
  - do not change the old production order;
  - do not emit new wire messages.
- [ ] Implement `DiagnosticsProjection` that compares old terminal item facts vs new
      kernel terminal item facts and emits warnings.
- [ ] Add kernel contract tests:
  - idempotency;
  - revision monotonicity;
  - atomic batch;
  - checkpoint round-trip for item shadow state.
- [ ] Add item invariant property tests:
  - random operation sequences;
  - unique location;
  - no terminal resurrection;
  - no duplicate transfer.
- [ ] Add replay differential tests:
  - same input trace drives legacy and new kernel;
  - compare semantic item terminal facts;
  - any diff must be triaged as either a model bug or an old-path bug, not silently ignored.
- [ ] Collect the defect-family evidence:
  - known ghost/duplicate item bugs;
  - race cases;
  - show the shadow kernel can explain or reproduce each family.
- [ ] Document the shadow integration, known limitations, and open questions.

## Exit criteria

- The old online behavior is unchanged: all existing tests pass, no wire/protocol change.
- Real item replay traces produce zero semantic diff between legacy terminal facts and
  new kernel terminal facts, or every diff is explicitly triaged and resolved.
- Random generated operation sequences never violate the implemented item invariants.
- The shadow state is able to explain historical ghost/duplicate/race defect families
  (or a written analysis says why a specific family is outside the first slice).
- The kernel/domain boundary is proven: Commands are side-effect-free, Reduce is
  deterministic, and no Unity/network/save code is called inside commit steps 1-8.
- A `docs/selfchecks/` fact sheet exists with mechanism inventory, evidence, and results.

## Verification design

- `dotnet build`
- `dotnet test`
- `dotnet format`
- existing architecture/event/entity gates
- new kernel tests and property tests
- replay differential runs
- no manual acceptance during development per repo policy; use L0 + static evidence.

## Deliverables

- `GameState` project skeleton + minimal kernel.
- Items shadow domain with first slice commands/events.
- Shadow integration point.
- Diagnostics diff projection.
- Kernel and invariant tests.
- Replay differential harness or documented manual command.
- Phase self-check fact sheet.
- New tech-decisions entries with evidence.

## Open questions / risks

| Risk | Mitigation |
|---|---|
| Shadow integration may introduce latent ordering side effects | Use read-only/external observation; never let shadow code mutate old authoritative tables. |
| "Semantic diff" can be fuzzy | Define terminal fact comparison narrowly: location, identity, terminal state, ownership, container path, revision. |
| Historical replay traces may not cover a defect family | Extend replay traces or create a focused simulation; record explicitly what is not covered. |
| Building too much kernel abstraction too early | Keep the first slice exactly Spawn/PickUp/Drop/Destroy; do not add generic ECS or all domains. |
| Checkpoint seam may be over-designed for a shadow | Keep checkpoint minimal and only for the item shadow table; expand only when Phase B needs it. |

## Session handoff

At the end of any working session in Phase A:

- update this doc's `Session log`;
- leave a `Next actions` list;
- update `status.md`;
- if a delivery was completed, create or update the matching `docs/selfchecks/` fact sheet;
- do not mark Phase A complete until the exit criteria above have actual evidence.

## Session log

| Date | Scope | Commits | Verification | Notes |
|---|---|---|---|---|
| _(none yet)_ | | | | |

## Next actions

1. Start a fresh session with this phase doc.
2. Read `docs/architecture-evolution/session-workflow.md` and `status.md`.
3. Decide the minimal `GameState` project shape and confirm the exact first-slice C# contracts.
4. Implement the skeleton and item shadow; verify via replay diffs.
