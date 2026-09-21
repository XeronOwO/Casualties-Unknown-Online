# Native-binding parity in the session handshake

- Status: Review
- Priority: Medium
- Category: Protocol / Mod API
- Depends on: [Native-binding mods declare it instead of hiding](mod-native-binding-declaration.md)
  — the manifest field this carries onto the wire.

## Why this exists

A declaration that never leaves the machine buys nothing. The reason the declared tier exists is
that a host must be able to **see** which peers bind the game's own code, and to require parity when
the session's integrity depends on it. Before this change the handshake carried the mod list
(`HandshakeMsg.Mods`) with id, version and network mode (`docs/api/mod-api.md` §5) and nothing about
native binding, so two peers could run entirely different native modifications and still handshake
clean.

## What landed

- **The declaration is on the wire**: `ModInfoMsg` gained `NativeBinding`, filled from the discovery
  manifest (`ModRegistry.CurrentModInfos`), so every mod entry a guest declares carries the binding
  it published locally. Wire change → `ProtocolVersion.Current` was bumped in the same change
  (decision 188; the constant's own comment is the wire-change log).
- **The host judges it before admission**, per mod id and only for a mod BOTH sides list: a mod one
  side alone lists has no counterpart, and the existing NetworkMode rows still decide who may lack
  what. The binding never joins that contract and adds no rejection cause of its own.
- **One normalization rule, two callers**: `NativeBindingDeclaration.Normalize` ("blank = none",
  trimmed) is what discovery already applied and what the handshake now applies to the received
  value, so a blank declaration and an absent one are the same answer on both sides.
- **A host rule decides, not the declaration**: `HostRules` → `NativeBindingParity`
  (`allow` / `warn` / `require`, default `warn`) — allow is silent, warn admits the member and
  records the mismatch in the host's log, require refuses before the member is created, naming the
  mod and both declarations in the same log shape every other handshake refusal uses. Warn is the
  default because the declaration is new: a host that refused by default would lock out every
  session whose host updated first. The rule is editable on the Online UI's admin page (one row,
  three levels) and through the console's host-rule command (`nativebindingparity`); a hand-edited
  config value that does not parse falls back to warn.
- **The honest boundary is documented**: `docs/api/mod-api.md` §5's matrix gained the parity row and
  a paragraph on what parity proves (the declarations agree) and what it does not (an undeclared
  binding is undetectable, and an equal declaration does not prove equal behaviour).
- Decision 208 records the shape: a host rule rather than a contract, per-mod-id granularity, the
  warn default, and the protocol bump.

## Tests

- `ModHandshakeTests` (new parity rows): equal declarations and no-declaration-on-either-side pass
  even under `require`; a differing declaration is admitted and recorded under the default `warn`
  (the log names the mod, both declarations and the warn policy) and admitted silently under `allow`
  (asserted across every log level); `require` refuses before the member is created and names the mod
  and both declarations; a declaration against none is a difference, rendered as `none` on either
  side; a blank declaration normalizes to "none"; the comparison is exact after trimming and
  case-sensitive; a mod only the guest lists is not judged.
- `ModNativeBindingDeclarationTests.DeclaredBinding_RidesTheHandshakeInfo` covers the discovery → wire
  carrier itself over the real `ModRegistry.CurrentModInfos()` path — the handshake tests replace the
  list provider, so without this case the field could stop being filled and every other test would
  stay green. `Declaration_TravelsOnTheWireShape` replaces the stage-1 assertion that pinned
  `ModInfoMsg` at four properties (the shape stays pinned, now including the field this change added),
  and `Declaration_AddsNoDiscoveryRejectionCause` is the renamed stage-1 case, because a `require`
  host may now refuse over a declared difference.
- `ModHandshakeProtocolTests`: the new field round-trips exactly, and an entry without it decodes to
  null.
- `HostRulesPolicyTests`: the text mapping round-trips the three levels, an unknown value falls back
  to warn, the default is warn, and the service exposes the rule.

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
- An equal declaration does not prove equal behaviour: the same name may cover different patches.
- The Online UI row and the config entry are not covered by automated tests (the Plugin layer has no
  test host), so that surface is verified by the user on the physical machine.
