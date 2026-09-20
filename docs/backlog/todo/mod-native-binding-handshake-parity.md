# Native-binding parity in the session handshake

- Status: Todo
- Priority: Medium
- Category: Protocol / Mod API
- Depends on: [Native-binding mods declare it instead of hiding](mod-native-binding-declaration.md)
  — the manifest field this carries onto the wire.

## Why this exists

A declaration that never leaves the machine buys nothing. The reason the declared tier exists is
that a host must be able to **see** which peers bind the game's own code, and to require parity when
the session's integrity depends on it. Today the handshake carries the mod list
(`HandshakeMsg.Mods`) with id, version and network mode (`docs/api/mod-api.md` §5) and nothing about
native binding, so two peers can run entirely different native modifications and still handshake
clean.

## What this ticket carries

- Each mod's `NativeBinding` declaration travels with `HandshakeMsg.Mods`.
- The host sees the guest's declared bindings **before** the session is admitted and applies its
  policy: allow, warn, or require an exact match.
- A refusal names the mod and the declaration that differed, in the same shape every other
  handshake refusal uses (`docs/api/mod-api.md` §5's matrix).
- It is a wire change, so `ProtocolVersion.Current` is bumped in this change
  (`advanced-modification-policy.md` §7). The handshake check is the compatibility boundary, so
  nothing here is held back for compatibility's sake (decision 188).

## Design questions to settle here

- **Where the policy lives**: a host rule beside the existing ones, with a conservative default.
  Recommended default: **warn, not refuse** — the declaration is new, and a default that refuses
  would break every session whose host updated first.
- **Granularity**: parity of the set of bound mods, or of the declaration per mod id? The
  invitation flow already compares mod sets; the binding is a property of a mod entry, so the cheap
  shape folds into the existing matrix rows ("same mod id, same declaration").
- **What parity can honestly promise**: identical declarations do not prove identical behaviour
  (an undeclared binding is invisible). The document must say so rather than implying a guarantee.

## Acceptance

- A guest whose declared bindings differ is reported or refused according to the host policy, and
  the reason names the mod.
- The handshake matrix in `docs/api/mod-api.md` §5 gains the declaration column, and the handshake
  section states what parity does and does not prove.
- Tests: the mod handshake and handshake-protocol suites cover the new rows
  (`ModHandshakeTests` / `ModHandshakeProtocolTests`), and the wire round-trip covers the new field.

## Limits

- No detection of undeclared bindings — that limit belongs to
  [the declaration ticket](mod-native-binding-declaration.md) and is stated there.
- A host that allows a mismatch carries the risk knowingly; its log line is the record.
- **Why not higher priority**: nothing today depends on it, and the declaration stage has to land
  first; the wire change is small once the field exists.
