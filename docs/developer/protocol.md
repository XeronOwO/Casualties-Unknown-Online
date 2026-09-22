# Protocol

English | [中文](protocol.zh.md)

Everything authoritative travels as one of four envelopes inside a single transport frame. The frame
carries exactly one envelope, and the envelope kind is explicit, so a receiver can reject an unknown
payload before it decodes the body.

The design goal is narrow: a guest's action has to reach the host, the host's decision has to reach
every guest, and a guest that falls behind has to catch up without replaying the whole session.

## The four envelopes

- **Command** — a guest's intent, and the host's feedback when a command is refused.
- **CommittedBatch** — one atomic accepted change, broadcast from the host to every guest.
- **Checkpoint** — a complete state copy, sent in chunks when someone joins, reconnects or falls too far behind.
- **StateStream** — high-frequency fields such as position and aim; unreliable by design, healed by the next tick or checkpoint.

## Joining and catching up

A joining guest receives a checkpoint, restores it, and then applies the journal tail of the batches
committed after that checkpoint. If it notices a gap it asks for a range; if that range is older than
the host's journal, the host sends a fresh checkpoint instead.

## The version check

Both sides compare a protocol version when a guest joins, and either side ends the attempt when the
numbers differ. That check is the compatibility boundary: a wire change ships together with a version
bump instead of a compatibility layer, because a mismatch is refused at the door.

## What rides outside the envelopes

Session and control, presentation, chat, trade and mod-API traffic still use their own frames. They
are single-path protocols rather than a second authoritative store: a gameplay fact that other
players have to agree on belongs in a committed batch.
