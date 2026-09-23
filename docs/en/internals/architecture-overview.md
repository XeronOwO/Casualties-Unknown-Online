# The shape of CUO

[Documentation](../README.md) > [Internals](README.md) > The shape of CUO

---

**After this page** you can say where any CUO mechanism belongs, and why the authoritative state
lives in a [kernel](../reference/glossary.md) instead of in the objects the game renders. Read
[What CUO is](../start/what-is-cuo.md) first if you have not played a session yet.

## One writer per fact

CUO turns a single-player game into a host-authoritative [session](../reference/glossary.md). The hard
part is not moving bytes over Steam: it is deciding, for every shared fact, which machine may change
it, and making every other machine reach the same value from the same inputs.

The rule that follows is **one writer per persisted gameplay fact**. An item's stack size, a player's
health, whether a door is open — each has exactly one authoritative write path, and everything that
shows it is derived from that path: the Unity object you click, the remote clone of a teammate, the
network cache, the save. A fact two places may write is a fact that will disagree with itself.

## The bottom of the tree

`CasualtiesUnknownOnline.GameState` is the typed deterministic kernel. It references no other CUO
project, and it is the only place persistent gameplay state lives. Its surface is deliberately small — quoted in full from
`src/CasualtiesUnknownOnline.GameState/IGameStateKernel.cs`:

```csharp
Decision Execute(GameCommand command, CommandContext context);
ApplyResult Apply(CommittedBatch batch);
GameCheckpoint CreateCheckpoint();
RestoreResult Restore(GameCheckpoint checkpoint);
RunEpoch RunEpoch { get; }
IReadOnlyDictionary<ulong, ItemState> QueryItems();
ItemState? FindItem(ulong instanceId);
RunState? QueryRun();
WorldEntityState? QueryWorldEntities();
PlayerStateTable? QueryPlayers();
EnemyStateTable? QueryEnemies();
FluidStateTable? QueryFluids();
```

- `Execute` is the authoritative side: it judges one typed [command](../reference/glossary.md) and, if
  it accepts it, commits the resulting events as one [batch](../reference/glossary.md).
- `Apply` is every other side: the same batch arrives over the network, from a save, or from a replay,
  and is applied without re-deciding anything.
- `CreateCheckpoint` and `Restore` move a whole world in and out, and `RunEpoch` names the run whose
  state the store is holding.
- The query methods beside them — `QueryItems`, `FindItem`, `QueryRun`, `QueryWorldEntities`,
  `QueryPlayers`, `QueryEnemies`, `QueryFluids` — are read-only views for the interface, the save and
  diagnostics. They are not a second way in.

The type carries its own rule: "Small stable kernel surface. Domain-specific behavior is expressed with
typed commands, not dozens of per-domain methods." Inside, each gameplay domain is a module the kernel
routes to; `src/CasualtiesUnknownOnline.GameState/Kernel/IDomainModule.cs` fixes that contract, and its
doc comment states the boundary: "domain code never sees another domain's internals."

## The layers above it

| Project | What it owns |
|---|---|
| `CasualtiesUnknownOnline.Abstractions` | the public mod-facing API; references nothing |
| `CasualtiesUnknownOnline.GameState` | the typed deterministic kernel; references nothing |
| `CasualtiesUnknownOnline.Protocol` | wire types and codecs only; references nothing |
| `CasualtiesUnknownOnline.Application` | the admission seam and kernel replication — the only way up from the kernel |
| `CasualtiesUnknownOnline.Runtime` | sessions, Steam, dependency wiring, mod loading, runtime projections |
| `CasualtiesUnknownOnline.GameAdapter` | the only project that references the game's assemblies |
| `CasualtiesUnknownOnline.Plugin` | the BepInEx entry point; a thin lifecycle driver |

The direction is data, not prose.
`tests/CasualtiesUnknownOnline.NormativeGates.Tests/ProjectDirectionPolicy.cs` holds the table the gate
reads, and refuses a reference a declared layer does not allow, a project the table does not classify,
and a Runtime that stops referencing Application — "reaching the kernel without the layer is exactly
what the gate forbids".

## One operation, one batch

```text
Command  ->  Decide  ->  CommittedBatch  ->  Reduce  ->  Effects
```

- A **command** asks for something to happen. It is typed, and it can be refused, with a reason.
- **Decide** runs against a working copy of the state and produces event drafts, or a refusal. It has no
  side effects: nothing outside the kernel changes while a command is being judged.
- A **committed batch** is the atomic result. `src/CasualtiesUnknownOnline.GameState/CommittedBatch.cs`
  records the operation id, the global revision, the actor, the authority kind, the run epoch, the
  expected revisions and the accepted events — "Batches are the only way confirmed state changes".
- **Reduce** applies those events to the authoritative state. Every machine reduces the same batch the
  same way, which is what makes a guest, a late joiner and a replay arrive at the same world.
- **Effects** are what an outer layer must do as a result: move a Unity object, play a sound, send a
  frame. They are derived by projections rather than stored, so a replay never has to persist
  presentation.

A composite operation — spend materials, create the product, update the player, unlock a recipe — is one
command made of inner commands, executed in order against the same working copy and committed as a
single batch. If any inner command is refused, the working copy is dropped and nothing is committed.

## Why deterministic, and what it buys

The kernel never reads a clock, a random number, a Unity transform or a file. Those arrive as explicit
inputs: a command context, a named random stream, a checkpoint. So the same batch sequence always
produces the same state, and that is what makes the simulation and replay harnesses
[evidence](../reference/glossary.md) rather than convenience: a defect reproduces without running the
game, and a divergence is a fact about the inputs instead of about somebody's frame rate.

## Projections can be dropped

The Unity scene, the interface, remote clones, network caches and saves are
[projections](../reference/glossary.md) of the committed state. None of them is authority, so none of
them has to be kept consistent by hand: when a projection disagrees or fails, rebuilding it from the
kernel's read model is always a legal repair, and a projection failure never rolls back a fact the
kernel already committed.

## What the kernel is not

- Not a universal entity-component system, and not a generic CRUD store: a domain keeps its own typed
  model and its own invariants.
- Not an event log: there is a bounded window of committed batches for retransmission and idempotency,
  while the state itself is typed snapshots.
- Not a god object: the kernel routes commands, makes the working copy, collects event drafts, commits
  atomically and publishes the batch. Item rules, fluid formulas and cooldowns belong to domains.
- Not a promise of backward compatibility: the protocol version is checked when a
  [guest](../reference/glossary.md) joins, so a change never has to keep an old wire shape alive.

## What keeps it true

The rules above are enforced, not remembered. `SourceShapeGateTests` fails on kernel state that is
string-keyed, on a command that declares no [authority](../reference/glossary.md), on a GameState
reference to another project, and on a legacy or dual-architecture marker coming back;
`ProjectDirectionGateTests` reads the layer table from the solution; the adapter's own gates keep its
seams narrow. The rule-to-gate map is `docs/evidence/normative-gates.md`.

## Related reading

- [Internals](README.md) — the rest of this section
- [Who decides what happens to a player](judgment-ownership.md) — the authority rule in practice
- [The four envelopes](envelope-protocol.md) — how a committed batch reaches the other machines
- [State streams and snapshots](state-and-snapshots.md) — what the kernel pushes, and what a reader sees
- [Repository map and pitfalls](../contributing/repository-map-and-pitfalls.md) — which project a file belongs to
- [Glossary](../reference/glossary.md) — kernel, command, event, batch, projection, deterministic

---

[Documentation](../README.md) > [Internals](README.md) > The shape of CUO
