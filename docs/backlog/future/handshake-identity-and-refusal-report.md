# Handshake identity and structured refusal reasons

- Status: Future
- Priority: Medium
- Category: Protocol / mod compatibility
- Source: Loomi architecture review (2026-09-20), item 10
- Related: `docs/api/mod-api.md` §5/§7, `todo/mod-api-contract-governance.md`

## What it is

The handshake carries the guest's mod list and `ProtocolVersion.Current`, and a mismatch is refused
by strict equality on both sides. That policy is right and stays: the CUO wire protocol is not made
backward compatible (decisions 137/188 — the compatibility boundary is the handshake check).

What this ticket adds is an ACCURATE REFUSAL. Today one integer and one mod list answer every
question, so a client-only cosmetic mod that differs looks the same as a session-breaking one, and
the player is told "incompatible" without being told which thing was incompatible.

Goal: the handshake distinguishes its dimensions —

- the CUO wire protocol,
- the game build identity,
- the adapter identity/version,
- the kernel/save schema,
- the loaded synchronized mod set,
- each mod's own network/schema version,
- and the negotiated optional capabilities.

The wire protocol stays strictly equal; the other dimensions decide whether a peer is admitted, and
a refusal names the dimension that failed. Any wire change bumps `ProtocolVersion.Current` in the
same change.

## Trigger (why this is future, not now)

Promote it before the first public release, or the first time a network-affecting third-party mod
exists — whichever comes first. Until one of those is true there is nobody to give a better refusal
to: mods today are ours, and the single player-facing case is a version mismatch that says exactly
that.

## Acceptance

- A refusal names the failed dimension, and a client-only mod difference does NOT refuse the join.
- The optional-capability negotiation is additive: an absent capability degrades that dimension, not
  the session (see `todo/adapter-capability-catalog.md` for the Required/Optional rule).
- The sync-coverage matrix and its evidence file carry the new member, and the protocol number moves
  with it.
