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
  - `ItemPendingPickupArbiter` makes registration-order races explicit;
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
generic CRUD, and not full event sourcing. Production state is typed snapshots; event
logs serve replication, short-term recovery, replay, and diagnostics.

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
Runtime ──────> GameState
  │
  └───────────> Protocol
  │
  └───────────> Abstractions

GameAdapter ──> Runtime + game assemblies
GameState / Protocol / Abstractions reference no other CUO project.
```

There is no separate `Application` project today. Runtime is the orchestration and
projection layer; the GameState reference from Runtime is the current seam for
kernel access. GameState remains dependency-free and wire-free.

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

`CommittedOperationWindow` keeps only the Operation IDs needed to cover the retransmit
window. It does not grow forever; checkpoints record the necessary watermark.

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
internal tables. Cross-domain reads use typed transaction Read Sets; cross-domain writes
use event drafts and policies.

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
    IReadOnlyList<ExpectedRevision> Preconditions,
    IReadOnlyList<GameEvent> Events);
```

Effects are not stored in the persistent event list. They are derived from
Event -> Projection rules, so replay does not redundantly persist derivable presentation
information.

### 6.3 Dual revisions

- **Aggregate Revision**: detects stale operations against a single object.
- **Global Revision**: determines global ordering for cross-domain batches, checkpoints,
  and network deltas.

`OperationId` provides retransmission idempotency. If the same Operation ID arrives again,
the kernel returns the original decision and does not commit a second time.

### 6.4 Atomic commit algorithm

```text
1. Validate identity, role, and session phase
2. Validate ExpectedRevision
3. Create transaction working copy
4. Each domain Decide, collecting event drafts
5. Reduce drafts in order on the working copy
6. Validate domain and cross-domain invariants
7. Assign GlobalRevision
8. Atomically replace state and publish Batch
9. Projections consume the Batch and produce Effects
```

Steps 1-8 must not call network, Unity, or save code. An outer projection failure cannot
roll back an already committed domain fact; it is handled by projection retry or
checkpoint rebuild.

### 6.5 Cross-domain transactions

Cross-domain operations are orchestrated by typed Processes/Policies, not by kernel
switch statements. Example: Craft:

```text
CraftCommand
  ├─ Items: validate and consume materials
  ├─ Items: create product
  ├─ Players: update skill/reward
  └─ World: optionally unlock recipe
       ↓
  one CommittedBatch
```

A Process may use only public domain queries and command drafts. It does not access
private domain collections.

## 7. Authority policy, prediction, and rollback

### 7.1 Authority Policy

Every Command declares a policy:

| Policy | Use |
|---|---|
| `HostOnly` | World generation, saves, shared entity creation |
| `OwnerPredictedHostValidated` | Player movement, pickups, use, inventory drag |
| `TriggerObservedHostCommitted` | Native behavior that runs on the triggering side first and is hard to intercept before execution |
| `PresentationOnly` | Animations, particles, purely local sounds |

"Who simulates" is separated from "who commits the authoritative fact". A player can
simulate movement while the host validates the acceptable authoritative checkpoint.

### 7.2 Guest prediction model

```text
ConfirmedState
    + PendingPrediction[1..N]
    = LocalProjectedState
```

When a Host Batch arrives:

1. apply it to Confirmed State;
2. remove confirmed, rejected, or expired predictions;
3. replay remaining valid predictions in original order;
4. diff old/new LocalProjectedState;
5. emit the minimal Unity projection diff.

Pickup rollback positions and drag transients belong to Prediction Runtime, not to domain
authoritative state. Existing `PickupOrigins`, pending/claim/suppress mechanisms are
eventually absorbed by unified prediction and Native Operation layers.

### 7.3 What is not predicted

Default no-prediction areas: trading, cross-player take, complex crafting, save writes,
world generation, and multi-player shared goals. They may show a "request sent"
presentation without modifying the gameplay projection.

## 8. State layers and domain modules

State frequency layers, domain ownership/invariants, native operations, and projection
rules are maintained in:

- [domains.md](domains.md) — authoritative domain table, invariants, projection examples,
  Native Operation layer.
- [protocol.md](protocol.md) — state-stream limits, common header fields, versioning,
  save/checkpoint, and error/recovery.

This overview keeps only the kernel mechanics; the active detail lives in those layer
documents.

## 9. Domain modules

The kernel dispatches to typed domain modules: Items, Players, WorldEntities,
Entities/Enemies, Fluids, and World/Run. See [domains.md](domains.md) for the
authoritative domain table, invariants, and projection examples.

## 10. Native operations

See [domains.md](domains.md) → Native Operation layer.

## 11. Projections

See [domains.md](domains.md) → Ownership rules. Projections are rebuildable; a projection
failure never mutates authority.

## 12. Network protocol

See [protocol.md](protocol.md) for the four envelopes, join flow, state streams,
versioning, and command rejection.

## 13. Save format

See [protocol.md](protocol.md) → Save / persistence. Saves are checkpoint-only with
named random streams.

## 14. Error and recovery

See [protocol.md](protocol.md) → Error and recovery. Typed rejection reasons are defined
in `src/CasualtiesUnknownOnline.GameState/RejectionReason.cs`.

## 15. Testing architecture

See [verification.md](../verification.md) → Kernel and domain test architecture.
Replay/simulation, selfchecks, and gates are the evidence layer.

## 16. Architecture guards

See [architecture-guards.md](architecture-guards.md) for the active guard list. In
summary, the current architecture makes it a build/CI failure when:

- GameState references Unity, Runtime, Protocol codecs, or network packages;
- one domain references another domain's internal namespace;
- wire DTOs appear in domain public interfaces;
- Unity types appear in Command/Event/Checkpoint;
- an Event type lacks a reducer and serialization contract;
- a Command lacks an Authority Policy;
- persistent domain fields miss checkpoint round-trip;
- key aggregates miss invariant suites;
- core state uses string event names or `Dictionary<string, object>`;
- `Legacy`/double-write code remains without a deletion milestone after Phase E.

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
