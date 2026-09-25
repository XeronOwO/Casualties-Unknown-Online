# Protocol messages

[Documentation](../README.md) > [Reference](README.md) > Protocol messages

---

**After this page** you can look up what CUO puts on the wire: the one frame that carries authoritative
traffic, the four [envelopes](glossary.md) inside it, the header every envelope shares, the recovery
paths, and the id of every direct message outside the envelopes. The reasoning behind the design is in
[The four envelopes](../internals/envelope-protocol.md); read it first if you have not seen the kernel.

## One frame, one envelope

Everything authoritative rides one transport frame (`NetMsg.KernelEnvelope`, id 122) whose payload is a
`ProtocolFrame`. A frame carries exactly one envelope, and the kind is explicit so a receiver refuses
what it does not understand before it decodes the body.

The frame is checked structurally before anything acts on it.
`src/CasualtiesUnknownOnline.Protocol/Wire/ProtocolFrameValidator.cs` refuses:

- a frame that carries no envelope, or more than one;
- a header that disagrees with the envelope kind;
- a header whose sender is not the transport sender;
- a payload discriminator that does not belong to that envelope family;
- an unknown **critical** payload.

Presentation payloads are deliberately exempt: an unknown presentation payload is non-fatal, so a
future optional effect can ride the protocol without a new critical version bump. `KernelProtocolService`
calls the validator on every received frame
(`src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs`).

## The four envelopes

| Envelope | Direction | What it means | Source |
|---|---|---|---|
| `CommandEnvelope` | guest → host, and host → guest for a refusal | one intent, or one native observation, plus the refusal that answers it | `src/CasualtiesUnknownOnline.Protocol/Wire/CommandEnvelope.cs` |
| `CommittedBatchEnvelope` | host → guests | one atomic committed batch — the only confirmation that authoritative state changed | `src/CasualtiesUnknownOnline.Protocol/Wire/CommittedBatchEnvelope.cs` |
| `CheckpointEnvelope` | host → guest | one chunk of a complete state copy, on join, reconnect or gap recovery | `src/CasualtiesUnknownOnline.Protocol/Wire/CheckpointEnvelope.cs` |
| `StateStreamEnvelope` | host → guests, and guest → host for player reports | convergent high-frequency field updates | `src/CasualtiesUnknownOnline.Protocol/Wire/StateStreamEnvelope.cs` |

The frame and its enums: `ProtocolFrame.cs`, `EnvelopeKind.cs`, `EnvelopeHeader.cs` and
`WirePayloadType.cs`, all under `src/CasualtiesUnknownOnline.Protocol/Wire/`.

## The common header

Every envelope carries the same `EnvelopeHeader` fields:

```text
ProtocolVersion
RunEpoch
SenderId
MessageId
OperationId (when applicable)
BaseGlobalRevision
PayloadType
```

The run [epoch](glossary.md) is what keeps a previous run's traffic from polluting a new one: commands,
batches and state streams are dropped when their epoch does not match, and a checkpoint chunk set is
validated against the run identity the host announced with its world-join instruction
(`WorldJoinMsg.RunEpoch`) — refused before a chunk is buffered.

## The path of one action

1. A guest turns a gameplay intent into a `CommandEnvelope` and sends it. Commands cover spawn, pickup,
   drop, destroy, update, transfer, container sync, cook, player status, carry, and enemy/fluid facts.
2. The host validates the frame and routes the decoded command through the admission seam in the
   Application layer (`KernelProtocolCommandHandler`).
3. The kernel checks epoch and idempotency, routes to the correct domain module, and produces either an
   accepted `CommittedBatch` or a typed `Rejection`.
4. An accepted batch is broadcast to every guest as a `CommittedBatchEnvelope`, and the host projects it
   into its own runtime world tables, remote clones and other projections.
5. Every guest applies the batch to its own replay kernel and projects the result. `Apply` is idempotent
   by `OperationId`, so a duplicate batch is ignored.

## Joining and catching up

A guest never replays the session from the beginning. It gets a checkpoint plus the journal tail after
it:

```text
Host: checkpoint at revision N
Host: checkpoint chunks
Host: batches N+1..M (journal tail)
Guest: restore checkpoint → apply tail → Ready(M)
Host: start normal Batch/Stream
```

When a guest notices a batch gap it sends a range request (`RequestRange` / `WireCommandKind.RangeRequest`).
If the requested range has already fallen out of the host's bounded journal window, the host sends a
fresh checkpoint instead of a range it can no longer serve. Either way the guest ends up at a revision
the host named and normal traffic resumes from there
(`KernelProtocolService.SendCheckpoint`, `RequestRange`, `HandleRangeRequest` in
`src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs`).

## State frequency layers

| Layer | Examples | Replication | Journal |
|---|---|---|---|
| Authoritative discrete state | ownership, death, container contents, trap triggers | reliable batch | yes |
| Convergent continuous state | position, velocity, aim, regional fluid volume | unreliable state stream | no |
| Presentation state | animation phase, local particles, non-critical sounds | local derivation | no |
| Checkpoint | full run/player/item/world-entities/enemy/fluid state | reliable chunks | separate save |

A stream may only update existing convergent fields. It may not create or destroy an aggregate, change
ownership or a container relation, or advance a key gameplay state machine — a value that can be dropped
must never decide who owns something. A terminal state that later logic depends on becomes a domain
event and rides a batch.

## The version check is the compatibility boundary

Both sides compare the protocol version during the join [handshake](glossary.md), and either side ends
the attempt when the numbers differ. That check is the whole compatibility story: a wire change ships
together with a version bump, so the code never keeps an old shape alive. The current value lives in
`src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs` — that constant's own comment is the
wire-change log, and this page deliberately restates no number.

The protocol's versioning rules:

- an explicit envelope version and checkpoint schema version;
- numeric event payload ids (`WireEventKind` carries the per-event discriminator);
- an unknown critical event is refused; an unknown non-critical presentation effect is ignored;
- golden wire contract tests.

## Error and recovery

| Failure | Handling |
|---|---|
| Command retransmission | return the original decision |
| Duplicate batch | silently idempotent by revision/operation id |
| Batch gap | request the journal range |
| Gap too large | resend a checkpoint |
| Invariant failure | do not commit; emit complete transaction diagnostics |
| Wrong epoch | drop — commands, batches and state streams by their envelope/batch epoch, a checkpoint chunk set by the run identity the host announced with its world-join instruction |
| Unknown critical payload | drop the frame and log; no automatic disconnect is implemented |
| Projection exception | the domain is marked dirty and rebuilt from the kernel read model by the main-thread pump (`ProjectionHealthCoordinator`); the committed batch is not rolled back |

## Command rejection

A refused command comes back as a `CommandEnvelope` carrying `WirePayloadType.CommandRejected` (and
`WireCommandKind.CommandRejected`) — not as its own frame type. This replaced the legacy dedicated
`NetMsg.ItemReject` frame; block-break drop refusal, for example, now uses
`RejectionReason.BlockAlreadyBroken`. If a message family used to have a dedicated rejection message,
that message is gone: the answer travels in the envelope that carried the question
(`src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolCommandHandler.cs`,
`IKernelProtocolControl.cs`).

## Messages outside the envelopes

Not every frame is one of the four. Session and control traffic, world mutation and presentation,
character presentation, enemy snapshots and attacks, trade and chat, the mod API, and player-interaction
requests still use their own direct `NetMsg` frames. They are active single-path protocols, not a second
place where authoritative state lives: a gameplay fact other players must agree on belongs in a
committed batch, and anything only one receiver acts on can stay direct.

The ids below are the enum in `src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsg.cs`, which is the
source of truth; the identifier is what code uses and the id is what the wire carries.

**Session control and membership**

| Id | Message | Direction | What it carries |
|---|---|---|---|
| 16 | `Handshake` | guest → host | protocol version, identity and the guest's declared mod list |
| 17 | `HandshakeAck` | host → guest | acknowledges every handshake, repeats included |
| 58 | `HandshakeAckAck` | guest → host | end-to-end confirmation; the host marks the member handshaken only on this |
| 18 | `SceneState` | host → guest | scene/loading state |
| 20 | `WorldJoin` | host → guest | start loading the world; carries the run identity checkpoint sets are validated against |
| 21 | `WorldReady` | host → guest | everyone finished loading — start playing |
| 32 | `PlayerJoin` | host → guest | self-activation plus roster announcement |
| 33 | `PlayerLeave` | host → guest | a synced member left |
| 110 | `WorldSnapshotComplete` | host → guest | the world-entry snapshot group is complete |
| 111 / 112 | `Kicked` / `Banned` | host → guest | this member was kicked / banned |
| 1 / 2 | `Ping` / `Pong` | both | diagnostics |

**Character state and presentation**

| Id | Message | Direction | What it carries |
|---|---|---|---|
| 37 | `CharacterData` | guest → host report, host → guest restore | the 1 Hz character snapshot |
| 53 | `HostCharacterData` | host → guest | the host's own 1 Hz character snapshot |
| 93 | `LimbStateEvent` | guest → host report, host → guest relay | a limb latch changed (break/mend/dismember); full post-event limb and health state |
| 94 | `CharacterSound` | report plus relay | a one-shot action sound (gun fire also carries the recoil kick) |
| 113 | `CharacterAttackAnim` | report plus relay | the owner's attack animation replay |
| 114 | `CharacterLandingVisual` | report plus relay | the landing clip and dust replay |
| 120 | `CharacterRagdoll` | report plus relay | the owner's ragdoll pose |
| 121 | `WorldBloodSpawn` | report plus relay | a transient blood decal at a world position |
| 123 | `PlayerColorUpdate` | report plus relay | a cosmetic marker colour (no authority) |
| 124 | `LocationPing` | report plus relay | a transient UI location marker |

**World mutation, blocks and world events**

| Id | Message | Direction | What it carries |
|---|---|---|---|
| 40 | `BlockDamaged` | guest → host report, host → guest relay | partial block damage |
| 136 | `BlockDamageReport` | guest → host | this sender's cumulative damage contribution for cells the host has not accounted for; the host answers with its own value per cell |
| 41 | `WorldBlockState` | host → guest | full block-state (damage table) snapshot on world entry |
| 89 | `BlockDamageSnapshot` | host → guest | current partial block damage (world entry and 60 s resend) |
| 42 | `BlockPlaced` | report plus relay | a block write; the reporter's own echo acknowledges it |
| 51 | `BuildingEntityDamaged` | report plus relay | damage to a building entity |
| 52 | `BuildingEntityOpened` | report plus relay | a crate/lock was opened |
| 55 | `EarthquakeStart` | host → guest | an earthquake began (duration); guests suppress their own quake |
| 57 | `KeypadCode` | host → guest | the keypad codes, generated host-side at world entry |
| 66 | `EntityEvent` | report plus relay | a triggered trap/mechanism event |
| 68 | `EntitySpawned` | report plus relay | a runtime world-entity creation |
| 69 | `GeyserStateSnapshot` | host → guest | the geysers' liquid types (world entry and 60 s resend) |
| 79 | `TrapLayoutSnapshot` | host → guest | the trap entities' authoritative positions |
| 106 | `RadiationLineState` | host → guest | the radiation line's active/time-gone state |
| 134 / 135 | `RuntimeEntitySnapshot` / `RuntimeEntityRejected` | host → guest / host → reporter | the absolute runtime-created entity table / the refusal that stops a pending re-report |
| 140 | `RunFacts` | host → guest | the absolute run clock base and the layer's radiation-timer accounting, stamped with the run-baseline generation |

**Fluid, world time and the tutorial**

| Id | Message | Direction | What it carries |
|---|---|---|---|
| 70 | `FluidRegion` | host → guest (unreliable) | an absolute RLE snapshot of a grid region: 10 Hz changed-box diff plus a 1 Hz full-viewport fallback |
| 71 | `FluidInteraction` | report plus relay | a consumed fluid cell |
| 96 | `FluidPresentation` | host → guest | one water push or waterflow sound at a grid cell |
| 90 | `WorldTimeRequest` | guest → host | a speed the guest already applied locally |
| 91 | `WorldTime` | host → guest | the authoritative world-time speed |
| 104 | `TutorialClawState` | host → guest (unreliable) | the tutorial-claw presentation snapshot (20 Hz, sequence-gated) |

**Items and inventory**

| Id | Message | Direction | What it carries |
|---|---|---|---|
| 64 | `ItemIdWatermark` | both | guest → host the counter it allocated up to; host → guest the grant it must resume from |
| 65 | `CarriedInventory` | guest → host | the guest's carried inventory with self-assigned ids |
| 105 | `DynamiteExplosion` | report plus relay | a dynamite detonation (the terrain/building/item facts ride their own channels) |
| 97 | `PlayerInventoryTakeRequest` | guest → host | take one carried item from another in-world player |
| 125 / 126 | `RemoteInventoryIntentRequest` / `RemoteInventoryIntent` | guest → host / host → owner | one NATIVE inventory intent captured from the viewer's own drag release — the native call plus its operand ids — and its replay on the owner's real items, with the game's own guards, animations and sounds |

**Trade, speech and chat**

| Id | Message | Direction | What it carries |
|---|---|---|---|
| 72 | `TraderState` | host → guest | a trader's full authoritative state and stock (every interaction plus a reliable 5 s base fallback) |
| 73 | `TraderAction` | guest → host | a locally executed trader interaction |
| 115 | `TraderSwing` | report plus relay | a trader's hostile swing animation |
| 74 | `SpeechMsg` | report plus relay | one spoken bubble (the text is data: the speaker applied localization, random and distortion) |
| 109 | `Chat` | guest → host report, host → guest relay | one text-chat line |

**Enemies and crafting**

| Id | Message | Direction | What it carries |
|---|---|---|---|
| 81 | `EnemySnapshot` | host → guest | the full enemy snapshot (ids, spawn positions, runtime spawns) |
| 83 | `EnemyAttack` | host → guest | an announced attack (enemy, kind, per-enemy sequence); each guest judges on its own view and reports the terminal state through kernel combat events |
| 76 | `CraftReport` | report plus relay | one whole crafting operation: consumed/changed materials plus products |
| 77 | `RecipeUnlock` | report plus relay | a blueprint unlock |
| 139 | `RecipeUnlockSnapshot` | both | the unlocked recipe-index set (world entry, 60 s repair, and the answer that ends a guest's re-report) |

**Player interaction and medical operations**

| Id | Message | Direction | What it carries |
|---|---|---|---|
| 99 / 100 | `PlayerCarryStartRequest` / `PlayerCarryStopRequest` | guest → host | start/stop carrying an unconscious or dead in-world player |
| 102 | `PlayerHealRequest` | guest → host | use a carried medical item on another in-world player |
| 116 | `PlayerItemUseRequest` | guest → host | use a carried drink/food on another in-world player |
| 118 / 119 | `PlayerPushRequest` / `PlayerPushResult` | guest → host / host → all | a push request and the authoritative force |
| 107 / 108 | `TraderRecruitRequest` / `TraderRecruitResult` | guest → host / host → target | recruit a dead player at a trader and the authoritative post-revive body state |
| 127–133 | `MedicalOperationStartRequest`, `StartAck`, `Update`, `State`, `EndRequest`, `EndCommitted`, `Cancel` | operator ↔ host ↔ clients | the medical operation session: host-owned registry, reservations, incremental progress, one terminal commit |
| 137 / 138 | `MedicalOperationTargetCheckRequest` / `Answer` | host → target / target → host | the target's own client answers whether its live body allows the operation start |

**Mods and the kernel**

| Id | Message | Direction | What it carries |
|---|---|---|---|
| 75 | `ModMessage` | report plus relay | the shared mod-message frame: the sending mod's id plus an opaque payload |
| 86 / 87 | `ModCommandRequest` / `ModCommandResult` | guest → host / host → requester | host-authoritative mod command execution and its result |
| 122 | `KernelEnvelope` | both | the four-envelope kernel protocol |

## Related reading

- [The four envelopes](../internals/envelope-protocol.md) — why the kernel is shaped this way
- [State streams and snapshots](../internals/state-and-snapshots.md) — how a stream becomes readable state
- [How a world is saved](../internals/save-archive.md) — what a checkpoint becomes on disk
- [Who decides what happens to a player](../internals/judgment-ownership.md) — which side decides before anything is sent
- [The mod API contract](mod-api.md) — the mod-facing messages in their contract
- [Glossary](glossary.md) — envelope, batch, checkpoint, state stream, epoch, revision, handshake

---

[Documentation](../README.md) > [Reference](README.md) > Protocol messages
