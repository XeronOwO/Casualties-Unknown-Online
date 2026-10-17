# Current Architecture: Typed Deterministic Game-State Kernel

This is the active architecture of CUO after Phases A–E. The typed deterministic
kernel is the only supported design; Unity objects, UI, remote clones, network
caches, and saves are projections. Historical phase plans and migration records live
in the sibling phase documents, not in this file.

> Status: **Active** (Phase E complete). See
> [README.md](README.md) for the reading path, [domains.md](domains.md) for domain
> ownership, and [protocol.md](protocol.md) for wire/data-flow details.
> Baseline of the architecture evolution: `208df31` (2026-08-27).
> Compatibility with pre-evolution protocols/saves is intentionally not retained.

## 1. Why this architecture

The core features were largely complete before the iteration. The remaining defects
were caused by fact ownership and implicit ordering, not by missing gameplay
features, and the project had no public release or save-compatibility burden:

- it already has simulation, replay, race, idempotency, and state-machine tests;
- several correct low-level pieces already existed:
  - `DropPendingState` extracts cross-hook timing into a pure state machine;
  - `CraftingSync` and `HeaterCookSync` realize "one operation = one fact batch";
  - `PendingItemCreations` makes registration-order races explicit (creation is
    settled before any operation on the same item; the earlier `PendingPickupQueue`
    hold window is deleted);
  - replay and simulation harnesses cover real defect families;
  - service splits reduce single-file complexity.

The higher-level authority problem was that an item fact was split across
`WorldItemTable`, `ItemArbitration._transferred`, `CloneFactTable`, Unity `Item`,
rollback caches, and the periodic snapshot. Other domains had similar split across
authority tables, network handlers, adapter state, scene objects, and replay
logic.

The architecture chosen for that problem was to build a deep module with one small
interface behind one complete behavior.

## 2. Architecture in one paragraph

A **typed, deterministic game-state kernel** (`CasualtiesUnknownOnline.GameState`)
owns all persistent gameplay facts. The Unity scene, UI, network caches, remote
clones, and saves are projections. Each gameplay domain keeps its own typed model and
invariants; the shared part is transaction, revision, idempotency, authority policy,
checkpoint, replay, and projection mechanics. The kernel is not universal ECS, not
generic CRUD, and not full event sourcing. Production state is typed snapshots; a
bounded committed-batch journal plus checkpoints serve replication, short-term
recovery, replay, and diagnostics. There is no general event-log store.

## 3. Core flow

```text
Command -> Decide -> CommittedBatch -> Reduce -> Effects
```

- **Command**: a request that something happen; it may be rejected.
- **Event**: a fact the kernel has accepted; it must reduce deterministically.
- **Effect**: an action an outer layer must perform (update a Unity object, play a sound,
  send a network batch).

## 4. Project and dependency structure

Actual production layout:

```text
src/
├── CasualtiesUnknownOnline.Abstractions/   # public mod-only API; no Unity/game/BepInEx
├── CasualtiesUnknownOnline.GameState/      # typed deterministic kernel; dependency-free
│   ├── Kernel/
│   ├── Domains/
│   │   ├── Items/
│   │   ├── Players/
│   │   ├── Entities/
│   │   ├── World/
│   │   ├── WorldEntities/
│   │   └── Fluids/
│   └── Projections/
├── CasualtiesUnknownOnline.Protocol/       # protobuf wire DTOs, codecs, versioning
│   ├── Wire/
│   ├── Codecs/
│   └── Versioning/
├── CasualtiesUnknownOnline.Application/    # admission seam + kernel replication
│   └── Kernel/
├── CasualtiesUnknownOnline.Runtime/        # DI, session, networking, kernel protocol,
│   │                                       # runtime projections, mod API, diagnostics
│   ├── Session/
│   ├── Networking/
│   ├── Protocol/
│   ├── Patching/
│   ├── Diagnostics/
│   └── ...
├── CasualtiesUnknownOnline.GameAdapter/    # the only layer referencing game assemblies
│   ├── Character/
│   ├── Items/
│   ├── Patches/
│   ├── Run/
│   ├── Tutorial/
│   ├── World/
│   └── WorldGen/
├── CasualtiesUnknownOnline.Plugin/         # thin BepInEx entry
└── CasualtiesUnknownOnline.ModExample/     # example mod
```

Dependency direction:

```text
Plugin
  ↓
Runtime ──────> Application ──────> GameState
  │                  │
  │                  └───────────> Protocol
  └───────────> Protocol
  │
  └───────────> Abstractions

GameAdapter ──> Runtime + game assemblies
GameState / Protocol / Abstractions reference no other CUO project.
```

The Application layer exists: the Runtime reaches the kernel through it
(Runtime -> Application -> GameState) and declares no direct GameState reference. It owns the
command admission seam (`Kernel/KernelCommandGateway.cs`, "who may submit") — a session-level
decision rather than a domain one — and the kernel replication surface
(`Kernel/KernelProtocolService.cs` with its command handler, state-stream, checkpoint and
domain-mapper companions), together with the pure kernel <-> wire vocabulary
(`Kernel/KernelWireMapper.cs`, `KernelPlayerInteractionWireMapper`, `KernelEnemyCombatWireMapper`,
`KernelLimbWireMapper`, `KernelComponentWireMapper`, `ItemSpawnWireMapper`). The replication surface
reads the Runtime through declared ports (`IKernelSessionFacts`, `IKernelFrameSender`,
`IKernelCommandExecution`, `IKernelCheckpointSource`, `IKernelBatchApplication`,
`IKernelPendingCommands`, `IKernelItemDataNormalizer`), each answered by the
service that owns the capability, so a Runtime detail does not leak back into the layer. The handler
layer narrows the same way: each handler declares only the narrow capability interface of
`HandlerContext` it actually uses, while `HandlerContext` implements them all and stays the composition
root's single instance, so no handler can reach a capability it has no business holding. Two types
stay in the Runtime, each with its blocker recorded in
`done/legacy-wire-dto-slice.md`: `KernelBatchItemProjection` (a materialization projection whose
contract and every output path carry the legacy item DTOs) and
`KernelEnvelopeHandler` (transport dispatch: frame decode, traffic accounting, the packet-handler
base). The declared direction is enforced by `ProjectDirectionGateTests`; the moved types' assembly
and the Application assembly's reference set are pinned by `KernelReplicationLayerBoundaryTests`
(in the Tests project, because the normative gates project targets net8.0 and cannot load the net48
layer), and GameState keeps its own isolation gate.

The Game Adapter boundary is the same rule on the game-facing side: the Runtime declares the contract,
`CasualtiesUnknownOnline.GameAdapter` is its only implementation, and
`Runtime/GameAdapter/IGameAdapter.cs` is the COMPOSITION of twelve capability ports with no member of
its own — `IGameIntegrationLifecycle` (the quit broadcast), `IAdapterCapabilityQuery` (the startup
capability report), `IWorldPresenceQuery`, `IStartGateState`, `ILocalHealItemQuery`,
`ITraderRecruitRequest`, `INativeInputBlocker`, `IRemoteInventoryPresentation`,
`IRemoteMedicalPresentation`, `IPlayerAnchorQuery`, `IJoinFlowPresentation` (the direct-join intent)
and `ICarryPresentationPump` (the late-frame carry pin) — so a consumer resolves the port whose
capability it uses, a version adapter implements per capability, and a test double implements only the
capability it stands in for. Every port resolves to the one adapter singleton, registered in the
adapter's OWN composition (`GameAdapterComposition.Register`, in the adapter project because the
Runtime must not reference it and the plugin must not know adapter types; the plugin's config pass
calls it once), and `AdapterCapabilityPortShapeTests`
fails when a member is declared back on the aggregate, when a port's member census changes, or when a
port is left unregistered. The patch lifecycle itself (probe/install/uninstall) is driven by
`ICuoService` on the same instance and is not a consumer port, and the members no call site reached
(`CaptureWorldParams`, `ApplyWorldParams`, `CloseRemoteBackpack`, `CloseRemoteMedical`) were removed
rather than ported — the call-site census and each removal's reason are in
`done/adapter-capability-ports.md`. The adapter still reads Runtime-owned decision values through the
recorded `InternalsVisibleTo` grant (`Runtime/AssemblyInfo.cs`): the patch-contract facts, the
capability-report types and the mod status/building tables are Runtime-owned values whose port would
have to make them public or copy them, so the grant stays with its census and reasons
(decision 212).

The static seam those patch classes read is frozen the same way (decision 214,
`done/patch-bridge-domain-ports.md`): `IPatchBridge` is the aggregate the patch classes read through
`PatchBridge` — 87 of the 118 files in `GameAdapter/Patches/` today; the rest patch without reporting or
reach their own seam — and a domain's patch surface now lives in its own port instead of on the
aggregate. The fluid domain's eight members moved into `IFluidPatchPort` — the aggregate neither
declares nor composes it, so a call written against `IPatchBridge` cannot reach them at all, and the
one implementation serves the port through `PatchBridge.Fluid` — which keeps the shared wall from
widening: a member added to the aggregate is a build failure, not a review note. The item, character,
world/run, interaction and mod-content domains keep using the aggregate and migrate one at a time, when
their own work touches them; `PatchBridgePortShapeGateTests` pins the census of every seam and
`PatchBridgePortContractTests` asserts the same facts on the built adapter.

The entry above it is a host shell (decision 213, `review/plugin-host-shell.md`): `Plugin.cs` is
BepInEx configuration, Unity lifecycle forwarding and the Steam callbacks, the presentation is
`OnlineUiHost` (overlay composition, the frame-time input/modal rules, the IMGUI pass), the lobby
policy is `LobbySwitchActions` (one policy for the Steam callbacks and the UI buttons), registration
is `PluginDependencyRegistrar` (the BepInEx config bridge and the plugin-side editors) plus
`GameAdapterComposition`, and the plugin project references NO game assembly —
`GameAssemblyReferenceGateTests` fails the build if one comes back.

## 5. GameStateKernel

### 5.1 External interface

The current kernel surface is small and typed:

```csharp
public interface IGameStateKernel
{
    Decision Execute(GameCommand command, CommandContext context);
    ApplyResult Apply(CommittedBatch batch);
    GameCheckpoint CreateCheckpoint();
    RestoreResult Restore(GameCheckpoint checkpoint);

    IReadOnlyDictionary<ulong, ItemState> QueryItems();
    ItemState? FindItem(ulong instanceId);
    RunState? QueryRun();
    WorldEntityState? QueryWorldEntities();
    PlayerStateTable? QueryPlayers();
    EnemyStateTable? QueryEnemies();
    FluidStateTable? QueryFluids();
}
```

Meanings:

- `Execute`: authoritative side validates a Command, then produces and commits an event batch.
- `Apply`: non-authoritative side or replay side applies an already-committed batch.
- `CreateCheckpoint` / `Restore`: complete authoritative state serialization seam.
- Query methods: read-only per-domain views for UI, save, and diagnostics.

Do not expose dozens of per-domain methods on `IGameStateKernel`. Typed Commands are
routed to internal domain modules by a dispatcher; queries are read-only views.

### 5.2 Kernel state

The authoritative store (`GameStateStore`) contains:

```text
GameStateStore
├── RunEpoch
├── GlobalRevision
├── Items            (ItemState by InstanceId)
├── Run              (RunState?)
├── WorldEntities    (WorldEntityState?)
├── Players          (PlayerStateTable?)
├── Enemies          (EnemyStateTable?)
├── Fluids           (FluidStateTable?)
└── Operations       (CommittedOperationWindow)
```

`CommittedOperationWindow` stores full `CommittedBatch` objects for idempotency and
retransmit deduplication, capped at 2048 operations
(`src/CasualtiesUnknownOnline.GameState/Kernel/CommittedOperationWindow.cs`,
`GameStateStore.cs`). Restoring a checkpoint clears the operation window; there is no
separate checkpoint watermark field.

### 5.3 Domain module internal interface

The internal domain contract is non-generic and dispatches by command/event type:

```csharp
internal interface IDomainModule
{
    bool CanHandle(GameCommand command);
    bool CanReduce(GameEvent @event);
    DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context);
    void Reduce(GameEvent @event, MutableKernelState state);
    void AssertInvariants(KernelReadModel state);
}
```

- `Decide` has no side effects.
- `Reduce` must be deterministic.
- `AssertInvariants` runs in tests, debug builds, and before critical commits.

### 5.4 Kernel must not become a god object

The kernel owns exactly five mechanism responsibilities:

1. route Commands;
2. create transaction working copies;
3. collect domain event drafts;
4. validate and atomically commit;
5. publish Batch/Effect.

It must not contain `if item is gun`, trap cooldowns, fluid formulas, or Unity logic.
Those rules belong to domain modules. Domains must not directly reference other domains'
internal tables. The current cross-domain mechanism is `CompositeGameCommand`: the
kernel executes inner commands in declaration order, each `Decide` against the current
working copy and each accepted event reduced immediately, so a later inner command sees
earlier staged results; then it validates invariants across all domains
(`GameStateKernel.ExecuteComposite`). There is no separate Process/Policy/ReadSet type
in the current kernel.

## 6. Transaction model

### 6.1 Three inputs

```text
IntentCommand       Player or game expresses "what it wants to do"
NativeObservation   Adapter observes that the native game already produced a result
CommittedBatch      Host has confirmed "what actually happened"
```

Guests usually send `IntentCommand`. Host-local native behavior that cannot be
intercepted before execution may be reported as `NativeObservation`, but it still must
pass through kernel validation and commit. Only `CommittedBatch` changes confirmed state.

### 6.2 CommittedBatch

```csharp
public sealed record CommittedBatch(
    OperationId OperationId,
    ulong GlobalRevision,
    ActorId Actor,
    AuthorityKind Authority,
    RunEpoch RunEpoch,
    IReadOnlyList<ExpectedRevision> Preconditions,
    IReadOnlyList<GameEvent> Events);
```

Effects are not stored in the persistent event list. They are derived from
Event -> Projection rules, so replay does not redundantly persist derivable presentation
information.

### 6.3 Revision model

- **GlobalRevision**: a `ulong` on every `CommittedBatch`; it determines global ordering
  for batches, checkpoints, and network deltas.
- **Per-item Revision**: `ItemState` carries its own revision for domain-local stale
  checks (`src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemState.cs`).
- **Preconditions**: `CommittedBatch` stores `ExpectedRevision` values, but the generic
  kernel does not validate them. Domain modules perform revision/precondition checks in
  their `Decide` methods (for example `ItemDomainModule`).

`OperationId` provides retransmission idempotency. If the same Operation ID arrives again,
the kernel returns the original decision and does not commit a second time.

### 6.4 Atomic commit algorithm

```text
1. Check epoch and OperationId idempotency
2. Create transaction working copy
3. Each domain Decide, collecting event drafts
4. Reduce drafts in order on the working copy
5. Assert invariants (single-domain Execute uses the handling domain;
   composite/Apply run all domains)
6. Assign GlobalRevision
7. Atomically replace state and publish Batch
8. Projections consume the Batch and produce Effects
```

Steps 1-7 must not call network, Unity, or save code. Revision/precondition checks are
performed inside domain `Decide` methods. An outer projection failure cannot roll back
an already committed domain fact; `ProjectionHealthCoordinator` marks the affected
domain dirty and rebuilds it from the kernel read model on the main-thread pump.

### 6.5 Cross-domain transactions

Cross-domain operations use `CompositeGameCommand`, not a kernel switch statement or a
separate process/policy layer. The kernel executes inner commands in declaration order:
each `Decide` sees the working copy after all previous inner events have been reduced,
its accepted events are reduced immediately, and the whole list then emits one
`CommittedBatch`. If any inner command is rejected, the working copy is discarded and
nothing is committed.

Example shape:

```text
CompositeGameCommand
  ├─ Items: validate and consume materials
  ├─ Items: create product
  ├─ Players: update skill/reward
  └─ World: optionally unlock recipe
       ↓
  reduce all event drafts -> one CommittedBatch
```

Implementation: `src/CasualtiesUnknownOnline.GameState/CompositeGameCommand.cs` and
`GameStateKernel.ExecuteComposite` (`GameStateKernel.cs`). Inner commands are still
routed through the same domain modules; there is no private cross-domain table access.

## 7. Authority policy, prediction, and rollback

### 7.1 Authority Policy

Every Command declares a policy:

| Policy | Use |
|---|---|
| `HostOnly` | World generation, saves, shared entity creation |
| `OwnerPredictedHostValidated` | Player movement, pickups, use, inventory drag |
| `TriggerObservedHostCommitted` | Native behavior that runs on the triggering side first and is hard to intercept before execution |
| `PresentationOnly` | Animations, particles, purely local sounds |

"Who simulates" is separated from "who commits the authoritative fact". In the current
kernel, `AuthorityKind` is recorded on every command/batch but is **not** enforced by
`GameStateKernel.Execute` itself; runtime callers choose which policy they STAMP — the item authority
services stamp `HostOnly` for their own writes and `PlayerInteractionAuthorityPolicy` maps a domain
authority to a kernel `AuthorityKind` — and until the Application layer existed nothing applied that
declaration to a member's submission at all.

A command a MEMBER submitted over the wire passes the admission seam in the Application
layer (`KernelCommandGateway`): it binds the command's actor to the transport sender, refuses the
authority kinds a member may not author (`HostOnly`, `PresentationOnly`), and applies the item
eligibility rules once each — the destroy rule moved out of the kernel protocol command handler's
entry point, and one shared rule over the item a report NAMES: world content (or anything whose
container chain is world-rooted), or the member's own carried, installed or contained item. That rule
covers a destroy report, a state report and a container sync's parent, and the container sync has a
second half of its own (a listed child may not be an item another member carries or wears, which is
what stops a report pulling a peer's item into the reporter's own container). It
decides ELIGIBILITY only (the 2026-09-18 ruling: never what happens to another player's body, reach
or timing), the kernel still owns every domain verdict, and the seam occupies the position the old
check held so an eligibility refusal cannot mask a domain one. Two things it deliberately does NOT
cover: the handler's protocol heals (a container-sync report and an update for an unknown carried id,
which materialize the reporter's own carried parent — the host's own write, not a member-authored
fact; both are judged by the handler's own creation bounds first, which answer a report the member's
judged carried-set statement does not account for) and a guest's range request, which never becomes a
command.

WHICH wire kind declares what is `WireCommandAuthorityPolicy`'s table, one entry per mapped kind,
filled from the senders this tree actually has rather than from the blanket value the mapper used to
stamp: the item reports and the limb-item relocations are member-authored owner-predicted, an item
creation and the three enemy-combat results are member-authored trigger-observed (the native effect
ran on the reporting member's own client first), and the run baseline, the world-entity facts, the
player status, the carry relation, the enemy table, the fluid regions and the item transfer are
`HostOnly` because the host's own simulation authors them and no member sender emits them. A member's
submission of one of those is therefore refused by name at the seam instead of reaching its domain, and
`WireCommandSenderAuthorityGateTests` keeps that table and the `SendCommand` call sites under `src/` one
list in both directions, so neither drift can pass unnoticed. A console verb has an identifiable wire path
of its own (`WireCommandKind.ItemDataWrite`), which is what lets the host judge the VERB rather than only the
state it carries: `HostRules.AllowMemberItemDataWrite` (off) reaches the seam through `IMemberCommandRules`,
and a refusal settles the value the writer had already applied to its own object back onto the session's
record (`ItemDataWriteSettlement`), keyed by the kind the answer names (`WireCommand.RefusedKind`).

The seam is not the only place a member's report is judged, and the second one sits in the Game Adapter
because the fact it needs is the game's own: a destruction report's drops are bounded against the thing that
break destroyed (`DropBoundPolicy` judges, `GameBlockDropTable` carries the game's switch and a custom
tile's authored drops, `GameBuildingDropTable` a dying building's own prefab data, `BlockDropBoundJudge` is
the host-side user). The roll itself is NOT reproducible — the game draws it from the process-global
unseeded `UnityEngine.Random` — so the host bounds instead of re-rolling: the destroyed block id is its OWN
read from before it applied the air write (`BlockBreakArbitration` carries it through the fresh, repeat and
lost-air-write verdicts), the report's drops are matched as a set against that block's table (a drop no
assignment of the report to the table's rows can account for is refused by name, and only those drops — the
rest keeps its normal fate), and the buildings that lost their support block in that break have their own
tables read at the MARK, the one moment their components still exist, keyed by the cell the report already
carries. What every other member-authored wire member is bounded against — or the anchor it lacks — is the
census in [member-report-validation.md](member-report-validation.md); the stages still open are
`todo/validation-and-source-checks.md`'s and `todo/member-report-bounds.md`'s.

### 7.2 Guest prediction model (not implemented in current code)

The planned model is a future design:

```text
ConfirmedState
    + PendingPrediction[1..N]
    = LocalProjectedState
```

There is no `PredictionRuntime`, `ConfirmedState`, or `PendingPrediction` type in the
current `src/`. Existing `PickupOrigins`, the pending-pickup queue, `DropPendingState`,
and `NativeOperationCoordinator` remain non-kernel active-path mechanisms, and the
generic Prediction Runtime is tracked in `docs/backlog/README.md`. See `domains.md`
(Predictions and no-prediction boundaries) and `docs/decisions/active.md` #154/#157.

### 7.3 What is not predicted

Default no-prediction areas: trading, cross-player take, complex crafting, save writes,
world generation, and multi-player shared goals. They may show a "request sent"
presentation without modifying the gameplay projection.

## 8. State layers and domain modules

State frequency layers, domain ownership/invariants, native operations, and projection
rules are maintained in:

[- domains.md](domains.md) — authoritative domain table, invariants, projection examples,
  Native Operation layer.
[- protocol.md](protocol.md) — state-stream limits, common header fields, versioning,
  save/checkpoint, and error/recovery.

This overview keeps only the kernel mechanics; the active detail lives in those layer
documents.

## 9. Domain modules

The kernel dispatches to typed domain modules: Items, Players, WorldEntities,
[Entities/Enemies, Fluids, and World/Run. See domains.md](domains.md) for the
authoritative domain table, invariants, and projection examples.

## 10. Native operations

[See domains.md](domains.md) → Native Operation layer.

## 11. Projections

[See domains.md](domains.md) → Ownership rules. Projections are rebuildable; a projection
failure never mutates authority.

## 12. Network protocol

[See protocol.md](protocol.md) for the four envelopes, join flow, state streams,
versioning, and command rejection.

## 13. Save format

[See protocol.md](protocol.md) → Save / persistence. Saves are checkpoint-only with
named random streams.

## 14. Error and recovery

[See protocol.md](protocol.md) → Error and recovery. Typed rejection reasons are defined
in `src/CasualtiesUnknownOnline.GameState/RejectionReason.cs`.

## 15. Testing architecture

[See verification.md](../evidence/verification.md) → Kernel and domain test architecture.
Replay/simulation, selfchecks, and gates are the evidence layer.

## 16. Architecture guards

[See architecture-guards.md](guards.md) for the full guard list. The
five guards currently automated in `SourceShapeGateTests` are:

- GameState project isolation (`GameStateIsolation_NoForbiddenReferencesOrTokens`);
- item projection ownership (`ItemAuthority_NoDirectProjectionMutation`);
- no legacy/dual markers (`NoLegacy_NoRemovedDualArchitectureMarkers`);
- every `GameCommand` carries authority (`CommandAuthority_EveryGameCommandDeclaresAuthority`);
- no string-keyed/Hashtable kernel state (`KernelShape_NoStringKeyedStateOrHashtable`).

Other aspirational guards — event reducer/serialization registration, checkpoint
round-trip enforcement, and invariant-suite registration — are currently covered by
tests/processes but are **not** automated by the C# source-shape gates today.
Report-validation completeness (guard 11) is automated outside `SourceShapeGateTests`,
by `ReportValidationCensusGateTests` over
[member-report-validation.md](member-report-validation.md).

## 17. Explicit non-goals

- No universal ECS replacement for the original game object model.
- No permanent storage of all high-frequency events.
- No treating Network Handlers as domain modules.
- No adapter-maintained authority tables.
- No micro-interface per message type.
- No inheritance subclass per item type.
- No reflection as the only schema for critical gameplay state.
- No permanent old protocol/old save compatibility layer in the final architecture.
- No flattening of domain-specific terminology into generic CRUD.

## 18. Success criteria

The architecture meets these criteria:

- every persistent gameplay fact has exactly one authoritative write entry;
- one logical operation corresponds to one atomic Batch;
- network events, checkpoints, and replay use the same reducer;
- any projection can be dropped and rebuilt;
- duplication, reordering, disconnects, and stale epochs break no invariants;
- adding a new item capability fails tests unless Capture/Restore/Equality/Validation/
  Presentation are all present;
- adding a new domain behavior does not require changing the kernel core switch;
- Unity hook changes do not directly change wire semantics;
- old service facades, dual state tables, and patch-style suppression caches are removed;
- historical item replay retains the same user-observable semantics under the new architecture.
