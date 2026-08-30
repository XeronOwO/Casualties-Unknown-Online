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

```text
src/
├── CasualtiesUnknownOnline.GameState/
│   ├── Kernel/
│   ├── Transactions/
│   ├── Journal/
│   ├── Projections/
│   └── Domains/
│       ├── Items/
│       ├── Players/
│       ├── Entities/
│       ├── Traps/
│       ├── Fluids/
│       └── World/
│
├── CasualtiesUnknownOnline.Application/
│   ├── Commands/
│   ├── Authority/
│   ├── Prediction/
│   └── Synchronization/
│
├── CasualtiesUnknownOnline.Protocol/
│   ├── Wire/
│   ├── Codecs/
│   └── Versioning/
│
├── CasualtiesUnknownOnline.GameAdapter/
│   ├── NativeObservation/
│   ├── UnityProjection/
│   └── Patches/
│
├── CasualtiesUnknownOnline.Runtime/
│   ├── Networking/
│   ├── Session/
│   ├── Persistence/
│   └── Diagnostics/
│
└── CasualtiesUnknownOnline.Plugin/
```

Dependency direction:

```text
Plugin
  ↓
Runtime ───────> Application <────── GameAdapter
  ↓                  ↓
Protocol         GameState

GameState references no other CUO project.
Protocol references only stable wire DTOs, not GameState implementation.
```

`Application` is the use-case orchestration layer. It does not own authoritative
gameplay state. It owns player/session context, prediction queues, and routing between
the kernel, network, and adapter.

## 5. GameStateKernel

### 5.1 External interface

Keep the public surface small and stable:

```csharp
public interface IGameStateKernel
{
    Decision Execute(GameCommand command, CommandContext context);
    ApplyResult Apply(CommittedBatch batch);
    GameCheckpoint CreateCheckpoint();
    RestoreResult Restore(GameCheckpoint checkpoint);
    QueryResult Query(GameQuery query);
}
```

Meanings:

- `Execute`: authoritative side validates a Command, then produces and commits an event batch.
- `Apply`: non-authoritative side or replay side applies an already-committed batch.
- `CreateCheckpoint` / `Restore`: complete authoritative state serialization seam.
- `Query`: read-only entry for UI, save, and diagnostics.

Do not expose dozens of per-domain methods on `IGameStateKernel`. Typed Commands and
Queries are routed to internal domain modules by a dispatcher.

### 5.2 Kernel state

```text
GameState
├── RunState
├── PlayerStateTable
├── ItemStateTable
├── EntityStateTable
├── TrapStateTable
├── FluidState
├── WorldState
├── GlobalRevision
└── CommittedOperationWindow
```

`CommittedOperationWindow` keeps only the Operation IDs needed to cover the retransmit
window. It does not grow forever. Checkpoints record the necessary watermark.

### 5.3 Domain module internal interface

```csharp
internal interface IDomainModule<TCommand>
{
    DomainDecision Decide(TCommand command, ReadModel state, CommandContext context);
    void Reduce(DomainEvent @event, MutableState state);
    void AssertInvariants(ReadModel state);
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

## 8. State frequency layers

| Layer | Examples | Replication | Journal |
|---|---|---|---|
| Authoritative discrete state | ownership, death, container contents, trap triggers | reliable Batch | yes |
| Convergent continuous state | position, velocity, aim, regional fluid volume | unreliable State Stream | no |
| Presentation state | animation phase, local particles, non-critical sounds | Effect/local derivation | no |
| Checkpoint | full Run/Player/Item/Entity/Trap/Fluid | reliable chunks | separate save |

Continuous streams have hard limits: they may not create/destroy aggregates, change
ownership or container relations, or advance key gameplay state machines. They may only
update convergent fields on existing objects.

Terminal states that affect later logic (settled, landed, explosion finished, etc.) must
become domain Events, not remain dependent on the last UDP tick.

## 9. Typed domain modules

### 9.1 Items

Core state:

```text
ItemState
├── Identity: InstanceId + DefinitionId
├── Revision
├── Location
│   ├── World(position anchor, optional parent world container)
│   ├── Carried(owner, slot/path)
│   ├── Contained(root owner/world, container path)
│   └── Terminal(consumed | destroyed | replacedBy)
└── Capabilities
```

Core invariants:

- one ID has exactly one Location at a time;
- container graph is acyclic and a child has exactly one parent;
- Terminal items cannot be resurrected;
- display proxy is never an authoritative object;
- Cook/Craft connect source terminal and product creation in one Batch;
- the same Operation replay does not create, destroy, or transfer twice.

Item capabilities use a composition Registry: Battery, Liquid, Durability, Gun, Ammo,
Fuse, Cooldown, Consumable, BodyComponent, and similar. Each capability must define all
of Capture, Restore, Equivalent, Validate, and Presentation; a partial sync path is not
acceptable.

### 9.2 Players

Owns player identity, terminal health/limb state, skills, backpack root reference,
current interaction relations, and durable state. High-frequency coordinates may be
streamed separately, but death, unconsciousness, carry relations, etc. enter domain
events.

### 9.3 Entities

Owns shared entity identity, lifecycle, health, opened/locked state, and domain traits.
Display proxies and Unity components are not authoritative entities.

### 9.4 Traps

Uses explicit state machines: `Armed`, `Warning`, `Triggered`, `Cooldown`, `Disabled`.
Trigger results and resulting damage/drops are submitted as cross-domain batches.

### 9.5 Fluids

Separates:

- persistent authoritative totals/types in regions/containers;
- high-frequency simulation grids or visual approximations.

Not every fluid pixel is written to the event log. The host periodically commits an
authoritative region checkpoint; guest local simulation is a rebuildable projection.

### 9.6 World / Run

Owns run identity, seed, layer, stage, world-generation results, global rules, and
checkpoint epoch. When a new Run switches epochs, all old-epoch Commands, Batches, and
stream packets are rejected; this removes cross-run residue at the root.

## 10. Native Operation layer

A native game operation often crosses multiple Harmony Prefix/Postfix hooks and delayed
callbacks. GameAdapter should have `NativeOperationCoordinator`:

```csharp
Begin(kind, subject, before)
Observe(token, fragment)
Complete(token, after)
Abort(token, reason)
```

It owns:

- operation ID and trace;
- RemoteApply/Prediction/Native origin;
- before state, observed fragments, terminal state;
- same-frame and cross-frame waits;
- deferred destroy claims;
- abort all on scene/run end;
- one `NativeObservation` output per native operation.

`DropPendingState` may become an internal policy. Craft/Cook use the same transaction
framework while keeping their own semantics. External code no longer sees
`ShouldSuppressDestroy`, `PickupOrigins`, or frame caches.

## 11. Projection model

At least:

- `UnityWorldProjection`: real world objects;
- `LocalPlayerProjection`: local body and backpack;
- `RemoteCloneProjection`: remote display proxies;
- `NetworkProjection`: Batch/Checkpoint/Stream encoding;
- `PersistenceProjection`: save checkpoint;
- `DiagnosticsProjection`: trace, invariant, diff.

All projections consume the same Batch but may produce different outer Effects.
`CloneFactTable` eventually becomes a cache owned by `RemoteCloneProjection`, not an
independent fact store. The cache can be cleared and rebuilt from a Kernel Query.

Projection failure handling:

```text
Domain commit succeeds
  ↓
Projection apply
  ├─ success: record applied revision
  └─ failure: mark dirty → rebuild from checkpoint/query
```

Never mutate authoritative state in order to repair a Unity projection failure.

## 12. New network protocol

The protocol is rebuilt around four envelopes:

```text
CommandEnvelope
CommittedBatchEnvelope
CheckpointEnvelope
StateStreamEnvelope
```

Common header fields:

```text
ProtocolVersion
RunEpoch
SenderId
MessageId
OperationId (when applicable)
BaseGlobalRevision
PayloadType
```

### 12.1 Join flow

```text
Host: checkpoint at revision N
Host: checkpoint chunks
Host: batches N+1..M
Guest: restore checkpoint → apply tail → Ready(M)
Host: start normal Batch/Stream
```

If a Batch gap exists, the guest requests a revision range. If the range exceeds the host
journal window, the host resends a checkpoint. State Stream may drop; the next frame or
checkpoint self-heals.

### 12.2 No hook-shaped messages

Wire schemas should be domain facts: `ItemRelocated`, `ItemsTransformed`,
`TrapTriggered`, not `OnDropPostfixMsg`. Native hook changes should not force protocol
changes.

### 12.3 Versioning

Even though compatibility can be broken now, start versioning from the first new protocol:
envelope version, checkpoint schema version, explicit numeric Event payload IDs, hard
reject unknown critical Events, ignore unknown non-critical presentation Effects, and
golden wire contract tests.

## 13. New save format

Saves store authoritative checkpoint, not a Unity object graph:

```text
SaveHeader
├── SchemaVersion
├── GameBuild
├── ModBuild
├── RunEpoch
├── GlobalRevision
└── CreatedAt

GameCheckpoint
├── World
├── Players
├── Items
├── Entities
├── Traps
├── Fluids
└── RandomStreams
```

Randomness must be saved as named random streams or already-decided results, not by
re-calling Unity/System random sources at load. Recent N Batches may be attached as a
diagnostic tail, but load uses the checkpoint as authority.

During development an explicit `SaveSchemaVersion` migrator may exist. Because there is
no compatibility burden, the first switch may reject old saves outright; do not let old
DTOs pollute the new domain models.

## 14. Error and recovery

Typed rejection reasons:

```text
UnknownAggregate
WrongEpoch
WrongRevision
NotAuthorized
InvalidTransition
InvariantViolation
Conflict
AlreadyCommitted
MalformedCommand
```

Recovery table:

| Failure | Handling |
|---|---|
| Command retransmission | return original decision |
| Duplicate Batch | silently idempotent by revision/operation |
| Batch gap | request journal range |
| Gap too large | resend checkpoint |
| Projection exception | mark dirty and rebuild |
| Invariant failure | do not commit; output complete transaction diagnostics |
| Wrong epoch | drop; old run must not pollute new run |
| Unknown critical payload | disconnect and report protocol incompatibility |

## 15. Testing architecture

### 15.1 Kernel contracts

- same Command + State + Context produces same Decision;
- Event Reduce is deterministic;
- Batch atomicity;
- Operation idempotency;
- revision monotonicity;
- checkpoint round-trip equivalence.

### 15.2 Domain property tests

Automatically generate operation sequences and continuously check:

- item unique location, acyclic containers, no Terminal resurrection;
- player death and backpack/drop batch consistency;
- traps cannot pass through illegal states;
- entities cannot accept damage after destruction;
- no old state survives epoch switches.

### 15.3 Model tests

For key domains, keep a minimal reference model. Run random Commands against both the
reference model and the production kernel, then compare final state and rejection
results.

### 15.4 Replay and differential testing

Existing `.replay` traces first drive both old and new implementations:

```text
Same input trace
  ├─ Legacy path → observed terminal facts
  └─ New kernel  → committed terminal facts
                  ↓
               semantic diff
```

Compare only semantic facts, not old internal call counts or log text.

### 15.5 Adapter contracts

Verify:

- one native user operation produces exactly one Observation;
- RemoteApply does not echo;
- projection rebuild does not produce a local Command;
- display proxies do not enter authoritative capture.

### 15.6 Network simulation

Under virtual time randomly apply latency, duplication, reordering, loss, disconnect,
checkpoint insertion, and reconnect. Reliable Batches must eventually converge; State
Stream only requires subsequent state convergence.

### 15.7 Test replacement principle

When a deep-module interface covers the behavior, delete tests that lock the old shallow
module cooperation order. Keep wire golden tests, adapter contracts, domain model tests,
property tests, and user-observable replay tests.

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

The architecture is complete when:

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
