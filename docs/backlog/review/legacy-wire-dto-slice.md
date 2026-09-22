# Legacy wire DTOs: the kernel <-> wire vocabulary moves into the Application layer

- Status: Review
- Priority: Medium
- Category: Architecture / layering
- Source: discovered by `review/application-layer-first-slice.md` stage 3 (2026-09-21)
- Related: `review/application-layer-first-slice.md` (the move that exposed this), `docs/decisions/active.md` decision 215

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

## What landed

The blocker recorded against `KernelWireMapper` was STALE, and re-censusing the closure is what the
change rests on. Measured against HEAD `26632345`:

- `KernelWireMapper` calls only the PURE enemy-combat conversions — `ToWire(EnemyBiteResultEvent)`,
  `ToWire(EnemyLungeResultEvent)`, `ToWire(EnemyEffectResultEvent)`, `FromWireBiteResult`,
  `FromWireLungeResult`, `FromWireEffectResult`, `FromWireBiteCommand`, `FromWireLungeCommand`,
  `FromWireEffectCommand` — plus a limb conversion. Every legacy message the ticket named is touched
  by `EnemyCombatKernelCodec`'s message builders (`ToBiteMessage`/`ToLungeMessage`/`ToEffectMessage`)
  and by `EnemyCombatWireMapper`'s three `*Msg` overloads, and the ONLY caller of those overloads is
  `EnemyCombatKernelSubmitter` — inside the Runtime. The `GameAdapter` question the ticket raised
  never had to be answered.
- `PlayerInteractionWireMapper` has ZERO Runtime type references. Its production callers at HEAD are
  eight sites across two files — six in `KernelWireMapper` and two in `EnemyCombatWireMapper`'s limb
  helpers — plus three in `PlayerInteractionTestSession`; nothing else calls it.
  `ItemSpawnWireMapper` (60 lines) is the same shape: pure, one caller (`KernelWireMapper`).
- The ticket's Notes ("the kernel's own event payloads carry them") is not what the tree says:
  `EnemyBiteMsg`'s own doc calls itself "the presentation projection of the kernel
  `EnemyBiteResultEvent`", and the event carries kernel types only
  (`ulong VictimSteamId, EnemyCombatLimb Limb, float VenomTotal, float Adrenaline, float Happiness`).
  The projection runs kernel fact -> legacy message, never the other way.
- The component conversion existed THREE times at HEAD and all three are field-for-field identical:
  `KernelWireMapper`'s write copy (through a `ToWireField` helper), that same file's inline read copy
  inside `FromWireData`, and `PlayerInteractionWireMapper`'s write/read pair — every one mapping
  `ItemComponentState`/`ItemComponentField` to `WireComponentState`/`WireComponentField` with the same
  seven fields. `WirePlayerInteractionLimb`
  declares 22 fields, and `PlayerInteractionLimb` / `EnemyCombatLimb` carry the same 22.

So the vocabulary moved into the layer and the legacy adapters call INTO it:

- `KernelWireMapper` -> `Application/Kernel/KernelWireMapper.cs` (526 lines, unchanged behaviour).
- `ItemSpawnWireMapper` -> `Application/Kernel/ItemSpawnWireMapper.cs`.
- `PlayerInteractionWireMapper` -> `Application/Kernel/KernelPlayerInteractionWireMapper.cs`
  (renamed for the neighbourhood it now lives in).
- NEW `KernelEnemyCombatWireMapper`: the nine pure enemy-combat conversions.
- NEW `KernelLimbWireMapper`: the limb vocabulary — one spelling for `PlayerInteractionLimb` and
  `EnemyCombatLimb`, built on the kernel's own limb shape rather than a second wire spelling.
- NEW `KernelComponentWireMapper`: the component vocabulary, replacing all three copies (the only
  remaining wire-component construction in `src` after the change).
- Runtime side: `EnemyCombatWireMapper` is now its three `*Msg` overloads only;
  `EnemyCombatKernelCodec` lost the two pure limb conversions; `PlayerInteractionWireMapper` lost its
  private component conversions and forwards the limb ones to the shared vocabulary.

**The port was DELETED, not kept.** `IKernelWireCodec`'s declared reason to exist was the mapper's
absence from the layer ("this layer declares the capability instead of reaching for that mapper
directly"). With the mapper inside the layer, every one of its ten members except one was a pure
forwarder to a type the layer now owns. The one conversion that genuinely needs the Runtime — the
wire item data that must arrive through the same legacy character-snapshot normalization the host's
spawn commands take — is declared as the single-member `IKernelItemDataNormalizer`, implemented by
`KernelItemDataNormalizer`. `WireCheckpointAssembler.Split`/`Assemble` lost their codec parameter and
call the layer-local mapper; `KernelProtocolService` and `GuestCheckpointReceiver` no longer take a
codec.

## The decision for the types that stayed

- **`KernelBatchItemProjection` stays in the Runtime BY DECISION.** It is not a mapper but a
  MATERIALIZATION projection, and the legacy item vocabulary is in its contract on both sides: its
  primary-constructor contract takes `ItemKernelAuthority`, `WorldItemTable` and delegates typed
  `Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2>` /
  `Action<CharacterItemMsg>` / `Action<ulong, WorldItem>`; `ToWorldItem(ItemState)` and
  `FireCookedEventFromBatch` build `WorldItem` + `NetVector2` through
  `ItemKernelAuthority.ToCharacterItem`; `BuildFullItem`/`BuildContents` build the recursive
  `CharacterItemMsg` tree; every public method writes `WorldItemTable`
  (`Apply`, `ApplyWorldTableOnly`, `Rebuild`, `RebuildWorldTableOnly`, `RebuildFromKernel`).
  Moving it therefore means redesigning the output contract — kernel facts out of the Application
  projection plus a Runtime materializer that owns the table and the adapter events — which changes
  the adapter boundary (`ItemService`, `RestoredWorldItemSet`, the `ItemSpawned`/`ItemDropped`/
  `ItemCooked`/correction event shapes) and needs its own acceptance surface. That is a projection
  redesign, not a move, and doing it inside this ticket would hide a behavioural change behind a
  layering change; the blocker is recorded in `KernelReplicationLayerBoundaryTests` so the move stays
  a deliberate edit to a recorded list.
- **`KernelEnvelopeHandler` stays with its earlier reason** (transport side of the seam: frame decode,
  traffic accounting, and the Runtime's own `PacketHandlerBase`); moving it means moving the packet
  dispatch infrastructure, which is its own decision.

## Verification

- Build: 0 warnings, 0 errors. `dotnet format` (write mode): exit 0 — a record that formatting ran,
  not an independent proof of a formatted tree (the read-only `--verify-no-changes` check is never
  clean in this repository: its remaining reports are generated `obj/` files, none in this change).
- Normative gates: 139/139 (including `SyncCoverageGateTests`, whose row files were re-pointed to the
  moved path, and `ProjectDirectionGateTests` against the declared layer table).
- Full suite with build: see the cycle's evidence file for the recorded counts.
- Field-set machine check: the four limb conversions (4 x 22 fields), the component conversions
  (7 fields) and the nine enemy-combat conversions were compared field-by-field against the HEAD
  versions extracted with `git show`; zero missing and zero extra fields.
- No behaviour is claimed to have changed and no wire shape was touched; the protocol version is
  untouched.

## Notes

The enemy-combat messages are the hard half: they are the GameState kernel's event payloads
(`EnemyBiteResultEvent` etc. carry them) as well as wire messages, so "move the DTO" also means
deciding what the kernel's own event vocabulary is allowed to name. Measure before choosing.

Correction (this cycle): the sentence above is the ticket's original hypothesis and the tree
contradicts it — the kernel events carry kernel types, and the legacy message is their projection
(`EnemyBiteMsg`'s own doc says so). The measurement that mattered was the CALL GRAPH, not the type
names: the mapper's dependency on the legacy half was a call to three pure overloads that happened to
sit in a class which also speaks the legacy messages.
