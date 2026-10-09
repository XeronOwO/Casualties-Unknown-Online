# Self-check — the typed value model (stage A: the mod's own data over the wire)

Cycle: 2026-10-09. Ticket: `docs/backlog/todo/mod-api-no-opaque-envelopes.md` (step 1 of the user's
2026-10-08 ruling landed earlier in `review/mod-content-typed-registration.md`; this cycle is the second
half). Scope landed: **stage A — the mod's own data over the wire** (the model, its encoder, `IModNetwork`,
`IModPackets`, `IModPacketContext`, the status frame and the status value, the two projections). Stage B —
the stores (`IModData`, `IModState`), the moodle request's remaining surfaces and the native value surface
— is NOT part of this cycle and stays in the ticket.

## 1. What landed, mechanism by mechanism

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | The value model (`ModValue`, `ModValueKind`, Abstractions) | a sealed immutable value: `Boolean`, `Integer` (`long`), `Number` (`double`), `Text`, `Binary`, `List`, `Map`; no null kind; factories copy what they are handed; structural equality and a bounded `ToString()` | `ModValueTests` (8 cases), the baseline's new lines |
| 2 | The encoder (`ModValueCodec`, Runtime internal) | the ONE validator: accepting a value IS being able to encode it. Canonical tag-per-value encoding, budgets (depth 8, 4096 values, 1024 entries, 16 KiB text, 256-byte names, 32 KiB binary leaf) enforced while writing AND while reading, refusals that name the path (`$.targets[3].hp: a number must be finite to travel`) | `ModValueCodecTests` (19 cases) |
| 3 | The message tunnel | `IModNetwork` carries `ModValue`; the adapter encodes (one log line on a refusal), the lifecycle decodes before any mod callback runs and drops a payload that is not exactly one value | `ModMessageTests`, `ModValueCodecTests.AnEmptyOrTruncatedPayload_IsRefused` |
| 4 | Declared packets | `IModPackets`/`IModPacketContext` carry `ModValue`; no per-delivery byte copy is needed any more, so the copy is gone rather than moved | `ModPacketsTests.OneImmutableValue_ServesTheWholeDelivery`, `ModPackets*Tests` |
| 5 | The status frame | `ModStatusUpdate` is a MAP whose field names are the wire contract (`id`, `scope`, `player`, `limb`, `schema`, `remove`, `value`), read with `TryFromValue`; an unknown field is ignored, a missing one refuses the frame | `ModStatusUpdateTests` (5 cases), `ModStatusWireTests` |
| 6 | The status value | `IModStatusRuntime`/`IModStatusTransport` take and return `ModValue`; `TryHandleStatusPayload(byte[])` became `TryHandleStatusUpdate(ModValue)`; the store keeps the value it was handed (no defensive copy — a value is immutable) | `ModStatusRuntimeTests`, `ModStatusWireTests`, `ModStatusProjectionStoreTests` |
| 7 | The two projections | `ToPayload`/`FromPayload` became `ToValue`/`TryFromValue` with the field names pinned as public constants; an absent field is the default, a present wrong-kind field refuses the value | `ModBodyFormulaProjectionTests`, `ModLimbProjectionTests`, `ModStatusProjectionStoreTests` |
| 8 | The retired payload path | `ModPayloadCodec` and its `DataContractSerializer` decode seam are DELETED: no contract in Abstractions is a serialization contract any more; `[DataContract]`/`[DataMember]` are gone from the three travelling types | the file is gone; grep finds no `ModPayloadCodec`, no `[DataContract]` under `src/` |
| 9 | The null-collection census | `ModPayloadNullCollectionTests` (411 lines, built on payload round trips) became `ModNullCollectionRuleTests`: the member half only, 26 rows — the moodle request's `Payload` is a `ModValue` now, so it is not a collection member any more, and the model's read-only `Items`/`Fields` views are a NAMED group rather than rows | `ModNullCollectionRuleTests`, `ModNullCollectionRuleTests.Census_...` |
| 10 | The registry, docs and gates | the API baseline re-recorded with 25 tombstones (27 keys left the surface; two of them name a member that still exists and changed type, so those are edits rather than removals), the terminology registry gained `value` and lost the "opaque bytes" payload definition, both human blocks updated (a `## Values` reference section, the network/packet/status sections, the two how-to examples), the alignment pairs re-recorded | `git hash-object` per pair; the gate suite |

## 2. The family audit (no piecemeal fix)

The census row is the family, and every member moved in the same change: the two ways a mod's own bytes
crossed a boundary (the anonymous tunnel and a declared packet), the third that CUO itself builds on top of
the first (the status frame), the value every one of them carries, the projections that shape it, the two
stores that hold it, the display side that reads it back, the example mods that demonstrate it, the
baseline, the terminology registry and both document blocks. Nothing was left with two spellings: grep
finds no `byte[]` on these surfaces, and the only remaining `byte[]` in `Abstractions` is the save
archive's file content, which the census keeps on purpose.

## 3. Tests

| Suite | Result |
|---|---|
| `ModValueTests` + `ModValueCodecTests` (new, 27 cases) | pass |
| `FullyQualifiedName`…`CasualtiesUnknownOnline.Tests.Mods` (the whole mod family, 460 cases) | 460/460 |
| `CasualtiesUnknownOnline.NormativeGates.Tests` | see §4 |

One real defect was found BY the new tests and fixed in the same round: the encoder wrote a map's field
name as a tagged value while the decoder read it as a bare length-prefixed string, so every map failed to
round-trip. `ModValueCodecTests.Containers_RoundTripIncludingNestingAndEmptyOnes` was red on the first run
and is the case that pins it.

## 4. Verification (how the runtime proves it)

- The encode/decode path is the production one in every message case: `ModMessageTests` sends through
  `IModNetwork` and asserts the value the receiving copy reads, including a value whose encoding is
  EXACTLY the 64 KiB rail (`ModMessageTests.AValueThatFillsTheRailExactly_Passes`) and a full-size frame
  that is not a value (`AFullyPaddedFrame_IsAcceptedByTheRailAndDroppedAsAValue`).
- The refusal path is observable: a value over the rail, over a budget, non-finite, invalid UTF-16, a
  truncated payload, a lying length, a duplicate field and an unknown tag each produce a refusal that
  starts at `$` and names the segment.
- The gates: the baseline gate (the surface is 865 entries, the reviewed file matches, 25 removals carry a
  reason), the documentation tree gate, the backlog integrity gate and the delivery checklist.

## 5. The independent review (2026-10-09)

`mod-value-model-review.md` is the report, run in a fresh context against the frozen tree. No blocker; two
major, three minor and four nits, and every one of them is dispositioned in this same commit:

| # | Finding | Fix |
|---|---|---|
| major | the decoder let a payload carry an EMPTY map field name, and the model refuses one by throwing — so ten bytes from a peer turned a refusal into an exception through a `Try` method (demonstrated on the frozen build) | `TryReadMap` refuses the name with `$[i]: a map field name must not be empty` before the model is asked; `ModValueCodecTests.AnEmptyFieldNameInAPayload_IsRefusedRatherThanThrown` pins it, and `ANegativeOrOversizedEntryCount_IsRefused` pins the count neighbours |
| major | the Chinese how-to page was half-migrated: its call table still said `SendToHost(payload)` and its snippet still read `payload` after the value variable was introduced, while the alignment record already called the pair confirmed | both how-to pages are fully migrated (tables and snippets), and the pair re-recorded |
| minor | `Items`/`Fields` handed out the array and the dictionary themselves, so a cast could write through a value the model calls immutable | both are read-only wrappers created once at construction; `ModValueTests.TheContainersAValueHandsOut_CannotBeWrittenThrough` pins it |
| minor | a stale `<see cref="TryHandleStatusPayload"/>` on the renamed interface | fixed |
| minor | four numbers in this record and the checklist did not reproduce against the frozen tree | corrected from a direct measurement (the codec is 591 lines, `ModValue` 413, 25 tombstones, 8 + 19 cases) |
| nits | the status store encodes a value to validate it and discards the bytes; two case names promise more than they assert; the ticket listed a `Count` accessor the model does not have; a receive-side drop log echoes the peer's `ModId` (pre-existing) | the accessor line is gone from the ticket; encode-to-validate is accepted deliberately (ONE validator beats a second rule that can drift) and recorded here; the two case names and the pre-existing log echo are named rather than silently dropped |

## 6. Limits (recorded, not hidden)

- **No game process was run this cycle.** Nothing here needs one: the surfaces are L0-testable end to end
  through the real composition root and the transport fake, and the acceptance batch is where a live
  session re-confirms that a mod's message and a shared status still arrive.
- **Stage B is not landed**: `IModData`, `IModState` (and its file), `ModStatusMoodleRequest`'s remaining
  call sites and `IModNativeApi`'s admitted value list still speak `byte[]`, so the contract carries both
  spellings for now. That is a staging decision recorded in the ticket, not a compatibility layer: stage B
  moves those rows whole and deletes the last of them.
- **The wire format is CUO's own**, so a pre-release peer of a different build cannot talk to this one; the
  handshake's protocol check is the boundary, and decision 241 keeps the number frozen before release.
- **The 64 KiB rail is measured on the encoded value**, so the largest legitimate value is somewhat smaller
  than 64 KiB of raw payload would suggest; the budgets above are the reason and they are documented on the
  mod API page.
