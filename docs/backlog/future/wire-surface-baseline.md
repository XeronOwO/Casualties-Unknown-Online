# The wire surface is recorded beside the protocol version

- Status: Future
- Priority: Medium
- Category: Protocol / gates (the wire surface and the version number)
- Source: the user's design question of 2026-10-07 — could a gate hash the network protocol and fail when it
  changed while the version did not — recorded as this ticket in the same conversation.
- Related: `src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs` (decision 241 is the freeze and
  supplies this ticket's arming switch), `docs/decisions/active.md` (decisions 137, 188, 241),
  `docs/contracts/abstractions-api-baseline.txt` and `ApiSurfaceGateTests` (the shape this ticket copies: a
  derived surface, a recorded baseline, a gate that reports the drift), `SyncCoverageGateTests` (which already
  owns the discriminator registries), `docs/en/reference/protocol-messages.md`

## What it is

The protocol version is one DECLARED integer and nothing checks that it still describes the wire: a change to
a protobuf member tag, a field type, an enum value or a message id moves the shape without touching the
number, and the only thing that notices is a human reading the diff. This ticket adds the missing half — a
DERIVED wire surface, recorded beside the version and re-derived by a gate, so "the wire changed" becomes a
machine fact instead of a reviewer's memory.

The rule the gate enforces once it is armed (that is, after the first official release — decision 241): the
recorded surface and the recorded version move TOGETHER. A surface change with an unchanged version fails
with the list of what changed, and the fix is either to revert it or to bump the number in the same change.

## Shape (decided here so the release day does not have to design it again)

- The baseline is a CANONICAL TEXT LIST, not an opaque hash: one line per wire member — message type, member
  tag, normalized wire type, cardinality, enum values with their numbers, and the discriminator registries'
  ids. A hash may ride along as the cheap trigger, but the failure message has to NAME what changed, and a
  text baseline makes that a diff rather than a guess. `docs/contracts/abstractions-api-baseline.txt` is the
  precedent, including its tombstone convention for a deliberate removal (`*REMOVED* <key> — <reason>`).
- Exclusions, so the baseline does not churn for what the wire cannot see: assembly identity and build stamps,
  declaration order, private members, doc and log text, and FIELD NAMES — protobuf is tag-based, so a rename
  is not a wire change; a name belongs in the report, not in the compared surface.
- Derivation: the gate host is net8 while the wire assemblies are net48, and only the Game Adapter may
  reference the game assemblies — so either `MetadataLoadContext` over the built wire assemblies (metadata
  only, no execution, no game assemblies) or the Roslyn source scan the other gates already use. What protobuf
  reads is the ATTRIBUTE, so the projection is attribute-level, not just type shape.
- Location: `docs/contracts/`, beside the API baseline. The version it pairs with is read from the constant,
  never copied into the file.
- The house rules for any gate apply: a census floor so a projection that finds nothing fails rather than
  passes, matcher self-tests with positive and negative samples, and every declared scan path
  existence-checked.

## The arming seam

The check must be INERT until the first official release: pre-release the version is frozen (decision 241), so
a surface change without a bump is the normal case and an armed gate would fire on every legitimate wire
change. The switch already exists — `ProtocolVersion.Current` IS `UnreleasedBaseline` before that release and
becomes a literal after it. The baseline file may be created early with the check reading that switch, or
created on the release day.

## What it can and cannot see

It sees the SHAPE: members added, removed or renumbered, changed wire types and cardinality, messages and
discriminator ids added or removed, enum values moved.

It cannot see the SEMANTICS, and this repository carries the receipts: decision 236 changed the ordering
contract between a drop report and the snapshot that follows it, and says in its own words that no wire member
is added; the same field can change meaning (absolute to delta, units, ownership) or change WHEN it is
populated. A peer on the other side of such a change interprets identical bytes differently and no hash of the
surface notices. Those changes stay the reviewer's call, recorded in the constant's own per-number entry —
what a peer without it would do. The gate automates "you touched the schema", never "this is a behavioural
wire change".

A third class is serializer and dependency movement: a protobuf-net version change moves the bytes without
moving a single attribute. It is partly covered already by the centrally pinned package versions, and this
ticket does not claim to close it.

## What already covers the neighbours

- Message DIRECTION is pinned by the main suite (the guest-to-host, host-to-guest and bidirectional direction
  classes over the receiver's own validity check), so a new id with the wrong direction already fails.
- Discriminator REGISTRY COMPLETENESS is pinned by `SyncCoverageGateTests`: every `NetMsg`,
  `WireCommandKind`, `WireEventKind` and `AdaptiveStreamId` member owns a matrix row.
- Nothing reads `ProtoMember`, `ProtoContract`, `EnvelopeHeader` or `WirePayloadType` today, so the field and
  tag level is the gap this ticket fills. It belongs in the same family as the three above: one version, one
  surface, one direction table, one registry.

## Acceptance

- A wire-surface change with the version unchanged fails, and the failure names what changed (member added,
  removed or renumbered, id added or removed, enum value moved).
- The same change with the version moved in the same change passes; a change that touches no wire surface
  passes whatever the version does.
- A projection that finds nothing fails on the census floor instead of passing.
- The check is inert while `ProtocolVersion.Current` IS `UnreleasedBaseline` and armed the moment it is not,
  proven by a case on each side of that switch.
