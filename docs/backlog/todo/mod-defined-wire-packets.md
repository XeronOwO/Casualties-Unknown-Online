# Mod-defined wire packets and custom handling chains

- Status: Todo
- Priority: High
- Category: Mod platform / protocol extensibility
- Source: the user's 2026-10-07 backlog request — the protocol must be extensible by mods: a mod has to be
  able to define its own behaviour packets and its own handling chain, instead of only riding CUO's
  built-in messages. This promotes the "custom replication domains" entry of
  `docs/backlog/todo/mod-content-ceiling.md` (Part 3, section C) out of the memo.
- Related: `todo/mod-content-ceiling.md` (the entry this promotes and the platform direction it sits in),
  `future/wire-surface-baseline.md` (the wire surface is recorded beside the protocol version),
  `docs/en/reference/protocol-messages.md`, `docs/decisions/active.md` (the protocol-number policy)

## What is asked

A mod ships a behaviour of its own (a machine, a vehicle, a minigame) and needs two things:

1. **A packet of its own** — a message type it defines, sent and received on the session's own transport,
   without CUO having to know the message. The requirement is a mod-owned identity for the message and a
   mod-owned payload, carried by the same reliability, ordering and peer set the built-in messages use.
2. **A handling chain of its own** — the mod decides who may send the message, who applies it, and in
   which order its handlers run relative to CUO's own, instead of a single global callback.

The user's words put the goal beyond "mods may piggyback on one generic blob": the mechanism must leave room
for open-ended custom behaviour, not only for the shapes CUO anticipated.

## What is not known yet

- What `IModNetwork` already offers a mod today, and how far it is from a packet identity: whether a mod
  message is already a first-class wire kind with its own id, or a single tunnel whose inner format CUO
  never reads.
- Whether the existing handshake consistency checks (mod id, version, permissions, `NativeBinding` parity —
  see `review/mod-native-binding-handshake-parity.md`) already cover "same message id, different payload
  version".
- How a mod message would interact with the save/restore cycle and with late joiners: is a mod packet
  replayable state or transient traffic, and who decides.

## Required work

1. Attribute first: read `IModNetwork` and the wire registry and write down what a mod can send today, what
   identity that traffic carries, and where CUO would have to slice a mod-owned id into the frame.
2. Decide the identity and the chain shape (a mod-owned message id, a declared handler chain, a declared
   permission) with the protocol-number policy in hand: this is a wire change, so it takes its own decision
   record and its own version step, not a compatibility shim.
3. Give the mechanism a failure story: an unknown message id from a peer, a payload a mod's handler throws
   on, and a mod that leaves the session mid-chain must each be observable and must not wedge the router.
4. Verify with two real mods, not one: a second consumer is the only proof that the identity and the chain
   are the platform's, not the first mod's private shape.

## Non-goals

- Not a general serialization framework: a mod owns its payload bytes; CUO owns the envelope, the routing
  and the refusal story.
- Not a compatibility promise to KrokMP's wire format — that is
  `future/krokmp-compatibility-adapter.md`, and compatibility is not a design input here.
