# Protocol and Data Flow

This is the mechanism layer for the current typed-kernel architecture: the four
envelopes, how commands/batches/checkpoints/state streams flow, and how the
authority boundary is enforced on the wire.

Reading path: [domains.md](domains.md) → [protocol.md](protocol.md) →
[verification.md](../verification.md).

## Four production envelopes

The kernel protocol rides one existing transport frame (`NetMsg.KernelEnvelope`,
id 122) with a `ProtocolFrame` payload. Exactly one envelope is present per frame;
the kind is explicit so receivers can reject unknown envelopes before decoding the
body.

| Envelope | Direction | Meaning | Source |
|---|---|---|---|
| `CommandEnvelope` | Guest → Host | Intent or native observation as a typed command | `src/CasualtiesUnknownOnline.Protocol/Wire/CommandEnvelope.cs` |
| `CommittedBatchEnvelope` | Host → Guests | One atomic committed kernel batch | `src/CasualtiesUnknownOnline.Protocol/Wire/CommittedBatchEnvelope.cs` |
| `CheckpointEnvelope` | Host → Guest | One checkpoint chunk during join/reconnect/gap recovery | `src/CasualtiesUnknownOnline.Protocol/Wire/CheckpointEnvelope.cs` |
| `StateStreamEnvelope` | Host → Guests | Convergent high-frequency field updates | `src/CasualtiesUnknownOnline.Protocol/Wire/StateStreamEnvelope.cs` |

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

- `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolService.cs`
- `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolCommandHandler.cs`
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

If a batch gap exists, the guest requests a revision range. If the range exceeds
the host's journal window, the host resends a checkpoint. See
`KernelProtocolService.SendCheckpoint` and the guest range-request handling in
`KernelProtocolCommandHandler`.

### Host → Guests: state stream

Continuous, high-frequency fields ride `StateStreamEnvelope` (player/enemy
streams, item moves, item snapshots, world-item snapshot streams). The stream is
unreliable by design; the next stream tick or a checkpoint self-heals. A stream
may only update existing convergent fields — it may not create/destroy aggregates
or change ownership.

- Item moves / item snapshots: `KernelProtocolService.SendStateStream*`
- Player/enemy streams: `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/PlayerStreamExchange.cs`
- Stream wire shape: `src/CasualtiesUnknownOnline.Protocol/Wire/WireStateStream.cs`

## Command rejection

A rejected command is returned as a `CommandEnvelope` with
`WirePayloadType.CommandRejected` (and `WireCommandKind.CommandRejected`). This
replaced the legacy dedicated `NetMsg.ItemReject` frame. Block-break drop refusal,
for example, now uses `RejectionReason.BlockAlreadyBroken`.

- `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolCommandHandler.cs`
- `src/CasualtiesUnknownOnline.Runtime/Session/Items/IKernelProtocolControl.cs`
- `docs/tech-decisions.md` #158

## Save / persistence

The save path is a projection of the authoritative checkpoint:

- `KernelSaveFileStore` writes `SaveHeader` + `GameCheckpoint` atomically and
  rejects unknown/corrupt files.
- `KernelSaveFile` is the on-disk shape.
- Named random streams are checkpointed and round-trip through wire/save.

Sources:

- `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelSaveFileStore.cs`
- `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelSaveFile.cs`
- `src/CasualtiesUnknownOnline.GameState/GameCheckpoint.cs`

## Non-kernel direct NetMsg families

Not every frame travels through the four envelopes. Session/control, world
mutation/presentation, character presentation, enemy snapshot/attack, trade/chat,
Mod API, player-interaction requests, and item id/starting inventory still use
direct `NetMsg` frames. These are active single-path protocols, not a second
authoritative store for kernel-owned facts. The full classification table is in
`docs/selfchecks/phase-e-legacy-inventory-selfcheck.md` (Direct NetMsg
classification).

## Evidence

- Four-envelope wire types: `src/CasualtiesUnknownOnline.Protocol/Wire/`
- Kernel protocol transport: `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolService.cs`
- Command execution/rejection: `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolCommandHandler.cs`
- Transport entry: `src/CasualtiesUnknownOnline.Runtime/Session/Handlers/KernelEnvelopeHandler.cs`
- Save/checkpoint: `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelSaveFileStore.cs`
- Phase C self-check: `docs/selfchecks/phase-c-protocol-core-selfcheck.md`
