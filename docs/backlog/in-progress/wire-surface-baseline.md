# The wire surface is recorded beside the protocol version

- Status: In progress
- Priority: Medium
- Category: Protocol / gates (the wire surface and the version number)
- Source: the user's design question of 2026-10-07 — could a gate hash the network protocol and fail when it
  changed while the version did not — recorded as this ticket in the same conversation.
- Related: `src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs` (decision 241 is the freeze and
  supplies this ticket's arming switch), `docs/decisions/active.md` (decisions 137, 188, 241),
  `docs/contracts/abstractions-api-baseline.txt` and `ApiSurfaceGateTests` (the shape this ticket copies: a
  derived surface, a recorded baseline, a gate that reports the drift), `SyncCoverageGateTests` (which already
  owns the discriminator registries), `docs/en/reference/protocol-messages.md`

## Run sheet

- [x] 1. Design recorded — this section and §Shape: the line format, the scan roots, the arming switch.
- [x] 2. Entry map — every declaration file the scan must read (Protocol/** and the one external registry), each
  named with what it contributes.
- [x] 3. Family — the neighbouring gates (ApiSurfaceGateTests' shape, SyncCoverageGateTests' registries), the
  floors, and the matcher self-tests the new gate needs.
- [x] 4. Red — `WireSurfaceGateTests` written first and failing: no baseline file, no derivation (5 failed, 0 passed,
  each on its own `NotImplementedException`).
- [x] 5. Implement — `WireSurfaceGate.cs` (Roslyn derivation, canonical render, comparison, emit-for-review,
  arming check) and the recorded `docs/contracts/wire-surface-baseline.txt`.
- [x] 6. Verify — focused gate, full gates, full suite, format; the arming case on both sides of the switch.
- [ ] 7. Commit — test + docs only; no deploy (the wire is unchanged by this ticket).
- [ ] 8. Close — status with the readings, README row, census, delivery checklist; report to the player.

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

## Shape as recorded (2026-11-07)

- Scan roots: `src/CasualtiesUnknownOnline.Runtime/Protocol/**/*.cs` (recursive, including `Messages/`), plus
  `src/CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync/AdaptiveStreamId.cs` — the one discriminator
  registry that lives outside `Protocol/` (the ticket's `WireCommandKind` / `WireEventKind` no longer exist in
  the tree; a tree-wide grep on 2026-11-07 is the record).
- Entries, sorted, '|'-separated: `id|<NetMsg name>=<number>`; `msg|<type>`;
  `member|<type>|<tag>|<wire type>|<cardinality>[|<dataformat>]`; `enum|<type>|<value>=<number>`.
- Wire type is normalized to what protobuf encodes: `int32`, `int64`, `uint32`…, `bool`, `float` (fixed32),
  `double` (fixed64), `string` (length-delimited), `bytes`, a user type's simple name, or `repeated <T>` with
  cardinality `repeated`. `Nullable<T>` records `optional`. An explicit `DataFormat = …` is recorded as a
  field; the default is left out.
- Enum numbers are the EFFECTIVE values (explicit numbers kept; implicit recorded as the running ordinal).
- Arming: the gate parses `ProtocolVersion.cs` — while `Current`'s initializer is exactly the identifier
  `UnreleasedBaseline`, the drift comparison is inert (it still derives and emits a candidate for the release
  day); any other initializer arms it, and a drift then fails naming the changed lines.
- Baseline `docs/contracts/wire-surface-baseline.txt`; candidate
  `docs/contracts/wire-surface-baseline.candidate.txt`, emitted by the gate when the surface differs, never
  compared. A removal deletes its line and adds `*REMOVED* <key> — <reason>`.
- Floors (measured 2026-11-07: 142 message types, 712 member lines, 108 ids, 144 enum lines): at least 130
  message types, 650 member lines, 100 id lines and 40 enum lines, so a scan that finds nothing fails instead of
  passing; every declared scan path is existence-checked, and the matcher has positive and negative samples.

## Entry map (2026-11-07)

| Declaration file | Contributes | What a miss would hide |
|---|---|---|
| `Protocol/NetMsg.cs` | the message ids (`id|` lines) | an id added, removed or renumbered |
| `Protocol/Messages/*.cs` (~160 files) | the `[ProtoContract]` types, their `[ProtoMember]` members, and the enum kinds they carry | a member added, removed or retagged; an enum value moved |
| `Protocol/NetPacket.cs`, `NetColorRgba.cs`, `NetVector2.cs` | the envelope and the nested carrier types | a member change in a type every message embeds |
| `Protocol/WorldTimeSpeed.cs`, `EnemyAttackKind.cs`, `EnemyEffectKind.cs`, `EntityEventKind.cs`, `TraderActionKind.cs` | the discriminator kinds that ride inside messages | an enum id moved without a type member moving |
| `Protocol/ProtocolVersion.cs` | the arming switch (`Current`'s initializer) and `UnreleasedBaseline` | the gate armed or inert on the wrong side of the release |
| `Session/AdaptiveSync/AdaptiveStreamId.cs` | the stream-registry ids | a stream id added or moved |
| `docs/contracts/wire-surface-baseline.txt` | the recorded surface (the compared side) | nothing at run time; the gate cannot record without it |

Every path above is existence-checked by the gate; a missing file fails with the path named, never silently shrinks
the scan.

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

## Self-check table (2026-11-07)

| Mechanism | Change | Evidence |
|---|---|---|
| Derivation (`ScanTree`, `DeriveFromSource`) | reads `[ProtoContract]` / `[ProtoMember]` at attribute level from 176 declaration files | `TheDerivation_ReadsTheWireAttributesAndNothingElse` — skips a class without the contract attribute and a member without the tag, records implicit enum ordinals |
| Recorded surface (`docs/contracts/wire-surface-baseline.txt`) | 1,106 entries (142 messages, 108 ids, 712 member lines, 144 enum lines) | the focused gate run 5/5; the census floor reads the same counts |
| Comparison (`Compare`) | key-level added / removed / changed with the line named | `TheComparison_NamesWhatChangedByKey` — a retag, an added member and a moved enum value are each named |
| Arming (`IsArmedFor`) | parses `ProtocolVersion.Current`'s initializer | `TheArmingSwitch_IsInertWhileCurrentNamesTheBaselineAndArmedOnceItDoesNot` — inert on `= UnreleasedBaseline`, armed on `= 2` |
| Emission (`EmitForReview`) | writes the candidate under `artifacts/`, never compares it | the first green run emitted it; the recorded copy was verified byte-equal by SHA-256 |

## Readings (2026-11-07)

- The derived volume: `WireSurfaceGate.ScanTree()` read 176 declaration files and produced 142 message types,
  108 message ids, 712 member lines and 144 enum-value lines; the recorded
  `docs/contracts/wire-surface-baseline.txt` carries all 1,106 entries plus the header (1,116 lines).
- `WireSurfaceGateTests` — 5/5. The two cases that pin the arming switch: `Current = UnreleasedBaseline` → inert
  (asserted false on `IsArmedFor`), `Current = 2` → armed (asserted true). The comparison case reports a retagged
  member, an added member and a moved enum value by key; the derivation case reads `[ProtoMember]` and skips a
  class without `[ProtoContract]`, a member without the attribute, and an enum's implicit ordinal (`First = 4`,
  `Second = 5`).
- The red run before the implementation: `失败: 5，通过: 0` — every case failed on its own
  `NotImplementedException`, the expected shape for a gate that does not exist yet.
- Full gates `失败: 0，通过: 822`; full suite `失败: 0，通过: 6598` (net48); `dotnet format` exit 0.
- The comparison is inert today: `ProtocolVersion.Current` is declared as `UnreleasedBaseline`, so the recorded
  surface is a release-day record rather than a live guard. The switch arms the day that initializer becomes a
  literal (decision 241).
