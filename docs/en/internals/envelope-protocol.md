# The four envelopes

[Documentation](../README.md) > [Internals](README.md) > The four envelopes

---

**After this page** you can say what travels in which envelope, why the high-frequency stream is
allowed to lose data while a committed batch is not, and how a guest catches up in the middle of a
session. Read [The shape of CUO](architecture-overview.md) first: this page assumes the kernel and the
committed batch.

## One frame, one envelope

Everything authoritative rides one transport frame that carries exactly one envelope. The kind is
explicit, so a receiver refuses what it does not understand before it decodes the body — quoted from
`src/CasualtiesUnknownOnline.Protocol/Wire/EnvelopeKind.cs`:

```text
A frame carries exactly one envelope; the kind is explicit so receivers can reject
unknown/unsupported envelopes before touching the payload.
```

The frame is checked structurally before anything acts on it.
`src/CasualtiesUnknownOnline.Protocol/Wire/ProtocolFrameValidator.cs` refuses a frame that contains no
envelope or more than one, a header that disagrees with the envelope kind, a header whose sender is not
the transport sender, a payload discriminator that does not belong to that envelope family, and an
unknown **critical** payload. Presentation payloads are deliberately exempt: "Unknown presentation
payloads are intentionally non-fatal so future optional effects can ride the protocol without requiring
a new critical version bump."

## The four envelopes

| Envelope | Direction | What it means |
|---|---|---|
| `CommandEnvelope` | guest → host, and host → guest for a rejection | one intent, or one native observation, plus the refusal that answers it |
| `CommittedBatchEnvelope` | host → guests | one atomic committed batch — the only confirmation that authoritative state changed |
| `CheckpointEnvelope` | host → guest | one chunk of a complete state copy, on join, reconnect or gap recovery |
| `StateStreamEnvelope` | host → guests, and guest → host for player reports | convergent high-frequency fields: position, aim, fluid volume |

Every envelope carries the same header: protocol version, run epoch, sender, message id, operation id
where it applies, the base revision it was built on, and the payload type. The run
[epoch](../reference/glossary.md) is what keeps a previous run's traffic from polluting a new one — a
frame from an old run is dropped rather than applied.

## The path of one action

1. A guest turns a gameplay intent into a `CommandEnvelope` and sends it.
2. The host validates the frame, then routes the decoded command through the admission seam in the
   Application layer.
3. The kernel judges it. An accepted command produces one committed batch; a refused one produces a
   typed rejection with a reason (`RejectionReason` in
   `src/CasualtiesUnknownOnline.GameState/RejectionReason.cs`).
4. An accepted batch is broadcast to every guest as a `CommittedBatchEnvelope`, and the host projects it
   into its own world tables and clones.
5. Every guest applies the batch to its own kernel and projects the result. `Apply` is idempotent by
   operation id, so a duplicate batch is simply ignored.

A refusal comes back as a `CommandEnvelope` carrying `WirePayloadType.CommandRejected`, not as its own
frame type. If you are working on a family that used to have a dedicated rejection message, that
message is gone: the answer now travels in the envelope that carried the question.

## Joining and catching up

A guest never replays the session from the beginning. It gets a checkpoint at revision N, restores it,
then applies the batches committed after it — the journal tail:

```text
Host: checkpoint at revision N     ->  chunks
Host: batches N+1..M               ->  the journal tail
Guest: restore checkpoint, apply tail, answer Ready(M)
Host: normal batches and streams from M on
```

Two recovery paths exist, and which one runs is decided by how far behind the guest is. If the guest
notices it missed a batch it asks for a range; if that range has already fallen out of the host's
bounded journal window, the host sends a fresh checkpoint instead of a range it can no longer serve.
Either way the guest ends up at a revision the host named, and normal traffic resumes from there.

## Why a stream may lose data and a batch may not

The state stream is unreliable by design. It carries values that converge: the next tick overwrites
them, so a lost frame costs freshness, not correctness. In exchange, a stream is not allowed to do
anything that only a fact can do — it may only update existing convergent fields. It may not create or
destroy an aggregate, change ownership or a container relation, or advance a key state machine.
Terminal states that later logic depends on have to become domain events and ride a batch, because a
value that can be dropped must never decide who owns something.

## The version check is the compatibility boundary

Both sides compare the protocol version during the join [handshake](../reference/glossary.md), and
either side ends the attempt when the numbers differ. That check is the whole compatibility story:
`src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs` states the policy — "each behavioral
wire extension bumps this and mixed-version sessions are rejected by the handshake". So a wire change
ships together with a version bump in the same change, and the code never has to keep an old shape
alive.

## What rides outside the envelopes

Not every frame is one of the four. Session and control traffic, world presentation, character
presentation, enemy snapshots and attacks, trade and chat, the mod API and player-interaction requests
still use their own frames. They are single-path protocols, not a second place where authoritative
state lives: a gameplay fact other players must agree on belongs in a committed batch, and anything
that only one receiver acts on can stay direct.

## Related reading

- [The shape of CUO](architecture-overview.md) — the kernel a batch comes from
- [State streams and snapshots](state-and-snapshots.md) — how a stream becomes something you can read
- [Who decides what happens to a player](judgment-ownership.md) — which side decides before anything is sent
- [Send a message to the other players](../how-to/send-a-network-message.md) — the mod-facing network
- [Glossary](../reference/glossary.md) — batch, checkpoint, state stream, epoch, handshake, protocol version

---

[Documentation](../README.md) > [Internals](README.md) > The four envelopes
