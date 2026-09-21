# Protocol and Data Flow

This is the mechanism layer for the current typed-kernel architecture: the four
envelopes, how commands/batches/checkpoints/state streams flow, and how the
authority boundary is enforced on the wire.

Reading path: [domains.md](domains.md) → [protocol.md](protocol.md) →
[verification.md](../evidence/verification.md).

## Four production envelopes

The kernel protocol rides one existing transport frame (`NetMsg.KernelEnvelope`,
id 122) with a `ProtocolFrame` payload. The frame is designed to carry exactly one
envelope; the kind is explicit so receivers can reject unknown envelopes before
decoding the body. `ProtocolFrameValidator.TryValidate` rejects a frame that does
not carry exactly one envelope, a header that disagrees with the envelope kind,
and an unknown critical payload; `KernelProtocolService.HandleFrame` calls it on
every received frame (`src/CasualtiesUnknownOnline.Protocol/Wire/ProtocolFrameValidator.cs:40-48`,
`src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs:186`).

| Envelope | Direction | Meaning | Source |
|---|---|---|---|
| `CommandEnvelope` | Guest → Host; also Host → Guest for `CommandRejected` | Intent/native observation; command rejection feedback | `src/CasualtiesUnknownOnline.Protocol/Wire/CommandEnvelope.cs` |
| `CommittedBatchEnvelope` | Host → Guests | One atomic committed kernel batch | `src/CasualtiesUnknownOnline.Protocol/Wire/CommittedBatchEnvelope.cs` |
| `CheckpointEnvelope` | Host → Guest | One checkpoint chunk during join/reconnect/gap recovery | `src/CasualtiesUnknownOnline.Protocol/Wire/CheckpointEnvelope.cs` |
| `StateStreamEnvelope` | Host → Guests and Guest → Host | Convergent high-frequency field updates (player reports are guest→host) | `src/CasualtiesUnknownOnline.Protocol/Wire/StateStreamEnvelope.cs` |

The frame and enums:

- `ProtocolFrame` — `src/CasualtiesUnknownOnline.Protocol/Wire/ProtocolFrame.cs`
- `EnvelopeKind` — `src/CasualtiesUnknownOnline.Protocol/Wire/EnvelopeKind.cs`
- `EnvelopeHeader` — `src/CasualtiesUnknownOnline.Protocol/Wire/EnvelopeHeader.cs`
- Wire payload types — `src/CasualtiesUnknownOnline.Protocol/Wire/WirePayloadType.cs`

## KernelProtocolService

`KernelProtocolService` is the production protocol controller. It is bidirectional:
on the host it executes decoded commands and broadcasts committed batches; on the
guest it sends commands, restores checkpoints, and applies committed batches to the
replay kernel. It also owns the journal, checkpoint chunks, pending batches, and
session reset.

- `src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs`
- `src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolCommandHandler.cs`
- `src/CasualtiesUnknownOnline.Runtime/Session/Handlers/KernelEnvelopeHandler.cs`

## Normal data flow

### Guest → Host: command

A guest sends a `CommandEnvelope` for a gameplay intent (spawn, pickup, drop,
destroy, update, transfer, container sync, cook, player status, carry, enemy/fluid
facts). The envelope carries the common header (protocol version, run epoch,
sender, message id, operation id, payload type).

### Host: decide and commit

The host routes the decoded wire command through `KernelProtocolCommandHandler`
into `GameStateKernel.Execute`. The kernel:

1. checks epoch/idempotency,
2. routes to the correct domain module,
3. produces an accepted `CommittedBatch` or a typed `Rejection`,
4. broadcasts the accepted batch to guests with `CommittedBatchEnvelope`.

For accepted batches, the host also projects the batch into runtime world tables,
remote clones, and other projections.

### Host → Guests: committed batch

A batch is the only confirmation of authoritative state change. Guests apply it to
their replay kernel and then project it into their Unity world/remote clones. The
kernel `Apply` path is idempotent by `OperationId`; a duplicate batch is ignored.

### Host → Guest: checkpoint + journal tail

Join/reconnect uses a checkpoint plus the journal tail after it:

```text
Host: checkpoint at revision N
Host: checkpoint chunks
Host: batches N+1..M (journal tail)
Guest: restore checkpoint → apply tail → Ready(M)
Host: start normal Batch/Stream
```

If a batch gap exists, the guest sends a range request (`RequestRange` /
`WireCommandKind.RangeRequest`). If the range exceeds the host's journal window,
the host resends a checkpoint. See `KernelProtocolService.SendCheckpoint`,
`KernelProtocolService.RequestRange`, and `KernelProtocolService.HandleRangeRequest`
(`src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs`).

### Host → Guests: state stream

Continuous, high-frequency fields ride `StateStreamEnvelope` (player/enemy
streams, item moves, item snapshots, world-item snapshot streams). The stream is
unreliable by design; the next stream tick or a checkpoint self-heals. A stream
may only update existing convergent fields — it may not create/destroy aggregates
or change ownership.

- Item moves / item snapshots: `KernelProtocolService.SendStateStream*`
- Player/enemy streams: `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/PlayerStreamExchange.cs`
- Stream wire shape: `src/CasualtiesUnknownOnline.Protocol/Wire/WireStateStream.cs`

## Common header fields

All envelopes share `EnvelopeHeader` fields:

```text
ProtocolVersion
RunEpoch
SenderId
MessageId
OperationId (when applicable)
BaseGlobalRevision
PayloadType
```

## State frequency layers

| Layer | Examples | Replication | Journal |
|---|---|---|---|
| Authoritative discrete state | ownership, death, container contents, trap triggers | reliable Batch | yes |
| Convergent continuous state | position, velocity, aim, regional fluid volume | unreliable State Stream | no |
| Presentation state | animation phase, local particles, non-critical sounds | Effect/local derivation | no |
| Checkpoint | full Run/Player/Item/WorldEntities/Enemy/Fluid | reliable chunks | separate save |

Continuous streams may not create/destroy aggregates, change ownership or container
relations, or advance a key gameplay state machine. They may only update convergent
fields on existing objects. Terminal states that affect later logic must become
domain events.

## Versioning

The current protocol uses:

- explicit envelope version, checkpoint schema version;
- numeric Event payload IDs;
- hard reject unknown critical Events;
- ignore unknown non-critical presentation Effects;
- golden wire contract tests.

See `src/CasualtiesUnknownOnline.Protocol/Versioning/`,
`src/CasualtiesUnknownOnline.Protocol/Wire/WireEventKind.cs` (per-event numeric
discriminator), and `src/CasualtiesUnknownOnline.Protocol/Wire/WirePayloadType.cs`
(payload-type discriminator).

## Error and recovery

| Failure | Handling |
|---|---|
| Command retransmission | return the original decision |
| Duplicate Batch | silently idempotent by revision/operation |
| Batch gap | request journal range |
| Gap too large | resend checkpoint |
| Invariant failure | do not commit; output complete transaction diagnostics |
| Wrong epoch | drop; old run must not pollute new run — commands, batches and state streams by the envelope/batch epoch, and a checkpoint chunk set by the run identity the host announced with its world-join instruction (`WorldJoinMsg.RunEpoch`), which is refused BEFORE a chunk is buffered |
| Unknown critical payload | drop the frame and log (`ProtocolFrameValidator.TryValidate` at `KernelProtocolService.HandleFrame`); no automatic disconnect is implemented |
| Projection exception | the domain is marked dirty and rebuilt from the kernel read model by the main-thread pump (`ProjectionHealthCoordinator`); the committed batch is not rolled back |

## Command rejection

A rejected command is returned as a `CommandEnvelope` with
`WirePayloadType.CommandRejected` (and `WireCommandKind.CommandRejected`). This
replaced the legacy dedicated `NetMsg.ItemReject` frame. Block-break drop refusal,
for example, now uses `RejectionReason.BlockAlreadyBroken`.

- `src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolCommandHandler.cs`
- `src/CasualtiesUnknownOnline.Application/Kernel/IKernelProtocolControl.cs`
- `docs/decisions/active.md` #158

## Save / persistence

The save path is a projection of the authoritative checkpoint:

- The production save path is the **CUO world archive** of
  `docs/architecture/save-archive-format.md` (decisions 162–169), implemented
  under `src/CasualtiesUnknownOnline.Runtime/Persistence/`. The layer advance the
  kernel commits (`ItemKernelAuthority.BatchCommitted` carrying a
  `RunAdvancedEvent`) writes a layer-end cut; the host's `/save` command and its
  deliberate menu return ARM a mid-run cut that the Game Adapter pump takes at its
  frame-end seam (`SaveCutSeam`, decision 167), where the transient policy decides
  whether the cut may be taken yet. The host's Continue entry restores the
  selected world, and CUO never reads or writes the native `save.sv`
  (`SaveSystemTryLoadGamePatch` blocks the native load).
- The **run baseline carries the two rarity multipliers** (`WireRunState`,
  decision 169). They are world-generation inputs — the game accumulates them once
  per layer and scales every loot/trap distribution by them — so a guest that
  generated with the game's fresh `1f` built a different layer than the host's, and
  a restore had no way to hand the layer the value it was generated with. The
  host's generation-boundary capture reads them from the live world
  (`WorldParamsService.CaptureAtBoundary`); the guest applies them with the rest of
  the world params. A sender that predates the field sends none, which means the
  game's own starting value.
- `WorldSnapshotEncoder`/`WorldSnapshotDecoder` map `GameCheckpoint` to and from
  the archive's per-domain files; the domain payloads are the same wire DTOs a
  late-joining guest receives, so a restored host holds what a join would have
  given it.
- `GameCheckpoint.RandomStreams` exists in the data model and round-trips through
  wire, but no production domain populates it — the decision was re-verified with
  S3.3 (no domain's restore decision consumes a stream, and keypad/geyser values
  are carried as DECIDED values instead), so the encoder's refusal guard stays: a
  non-empty stream set refuses the cut rather than writing a payload with no file
  to restore it from (decision 168).
- The retired protobuf single-file store (`KernelSaveFileStore`, `KernelSaveFile`,
  `SaveHeader`) was deleted with S2: it had no production caller, and keeping it
  would have left two competing disk shapes for one checkpoint.

Sources:

- `src/CasualtiesUnknownOnline.Runtime/Persistence/WorldSnapshotEncoder.cs`
- `src/CasualtiesUnknownOnline.Runtime/Persistence/WorldSnapshotDecoder.cs`
- `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldSaveService.cs`
- `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldTransientPolicy.cs`
- `src/CasualtiesUnknownOnline.GameState/GameCheckpoint.cs`

## Non-kernel direct NetMsg families

Not every frame travels through the four envelopes. Session/control, world
mutation/presentation, character presentation, enemy snapshot/attack, trade/chat,
Mod API, player-interaction requests, and item id/starting inventory still use
direct `NetMsg` frames. These are active single-path protocols, not a second
authoritative store for kernel-owned facts. The full classification table is in
`docs/evidence/selfchecks/architecture/phase-e-legacy-inventory-selfcheck.md` (Direct NetMsg
classification).

## Evidence

- Four-envelope wire types: `src/CasualtiesUnknownOnline.Protocol/Wire/`
- Per-event discriminator: `src/CasualtiesUnknownOnline.Protocol/Wire/WireEventKind.cs`
- Range request/recovery: `src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs`
- Kernel protocol transport: `src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs`
- Command execution/rejection: `src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolCommandHandler.cs`
- Transport entry: `src/CasualtiesUnknownOnline.Runtime/Session/Handlers/KernelEnvelopeHandler.cs`
- Save/checkpoint: `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldSaveService.cs`
- Guest→host player stream: `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/PlayerStreamExchange.cs`
- Phase C self-check: `docs/evidence/selfchecks/architecture/phase-c-protocol-core-selfcheck.md` (historical)
