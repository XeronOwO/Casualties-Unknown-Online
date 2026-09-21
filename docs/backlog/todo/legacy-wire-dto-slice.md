# Legacy wire DTOs: the precondition for moving the remaining kernel mappers

- Status: Todo
- Priority: Medium
- Category: Architecture / layering
- Source: discovered by `review/application-layer-first-slice.md` stage 3 (2026-09-21)
- Related: `review/application-layer-first-slice.md` (the move that exposed this), `review/adapter-capability-ports.md`

## Problem (evidence)

Stage 3 moved the kernel replication surface into `CasualtiesUnknownOnline.Application` behind
declared ports, and three of the nine named types could not move because their dependency closure
leaves the layer:

- **`KernelWireMapper`** (555 lines in the tree this ticket describes, 554 at HEAD `515d197a`: the
  move added one `using` to it, `Runtime/Session/Items/`) is a pure GameState <-> Protocol
  mapper EXCEPT for the enemy-combat branches: `ToWireEvent` calls
  `EnemyCombatWireMapper.ToWire(EnemyBiteResultEvent/EnemyLungeResultEvent/EnemyEffectResultEvent)`
  and `FromWireCommand` calls `EnemyCombatWireMapper.FromWireBite/Lunge/EffectCommand`. That mapper
  and its `EnemyCombatKernelCodec` work on the LEGACY protobuf messages `EnemyBiteMsg`,
  `EnemyLungeMsg`, `EnemyEffectMsg` (`Runtime/Protocol/Messages/`), and those messages are referenced
  by `CasualtiesUnknownOnline.GameAdapter` as well (`Character/CharacterDataSync.cs`,
  `Character/CloneFactTable.cs`, `Character/EnemyCombatReplay.cs`, `IPatchBridge.cs`,
  `Character/EnemyProximitySync.cs`, `Patches/EnemyProximityPatches.cs`,
  `Patches/TrapGrabberPlantPatch.cs`) — `GameAdapter` may reference `Runtime` only, so the messages
  cannot simply follow the mapper into `Application` (or into `Protocol`) without a decision about
  that reference.
- **`KernelBatchItemProjection`** (452 lines) has the legacy item DTOs in its own contract:
  `WorldItemTable`, `WorldItem`, `CharacterItemMsg` and `NetVector2` appear in its constructor and
  in the delegates its callers pass (`ItemService`, `RestoredWorldItemSet`), and it BUILDS
  `CharacterItemMsg` itself (`BuildFullItem`, `BuildContents`, `ToWorldItem`). Moving it is a
  projection redesign — "shape the legacy snapshot in the Runtime, project kernel state in the
  Application layer" — not a move.
- **`KernelEnvelopeHandler`** (27 lines) is the transport side of the seam: it inherits the Runtime's
  `PacketHandlerBase<TPacket, TContext>`, reads `CurrentFrameLength` and records traffic through
  `NetworkTrafficMonitor` before forwarding to `IKernelProtocolControl`. The packet-handler base,
  `HandlerContext`, `IPacketHandler`, the dispatch attribute and the traffic monitor are Runtime
  dispatch infrastructure, so this one is a taken responsibility split (recorded in the
  first-slice ticket), not a blocked move.

## Goal

Give the legacy wire vocabulary a home where the Application layer may speak it — or make the
legacy-dependent branches of the mapper injectable — so `KernelWireMapper` and
`KernelBatchItemProjection` can follow the rest of the replication surface. `KernelEnvelopeHandler`
stays unless the packet-handler infrastructure itself is moved, which is its own decision.

## Candidate mechanisms (decide in the change, with evidence)

1. **Move the legacy message set down a layer.** `NetVector2`, `CharacterItemMsg`, `NetMsg` and the
   enemy-combat messages are wire vocabulary; `Protocol` is the wire project. That requires
   `GameAdapter` (and any mod-facing surface) to reference `Protocol`, i.e. a declared change to
   `ProjectDirectionPolicy.AllowedReferences` — an architecture decision to take deliberately, with
   the churn measured (57/69/… files reference `NetVector2`/`CharacterItemMsg` today).
2. **Make the legacy branches injectable.** Keep the mapper where it is, split the enemy-combat
   conversions behind an Application-declared port implemented in the Runtime (the
   `IKernelWireCodec` precedent), and move the remaining pure mapping into the layer. This is
   narrower but turns a static mapper into an instance seam with call-site churn.

## Acceptance

- The remaining mapper/projection types live in the Application layer, or the decision that they do
  not records the structural reason with the same evidence standard as the first-slice ticket.
- Behaviour-preserving: the full suite is green and no behaviour diff is claimed.
- The layer-boundary test (`KernelReplicationLayerBoundaryTests`) is updated in the same change, so
  the move is a deliberate edit to the recorded list rather than a silent drift.

## Notes

The enemy-combat messages are the hard half: they are the GameState kernel's event payloads
(`EnemyBiteResultEvent` etc. carry them) as well as wire messages, so "move the DTO" also means
deciding what the kernel's own event vocabulary is allowed to name. Measure before choosing.
