# No opaque payloads in the mod API: a typed data model instead of a byte envelope

- Status: Review — **raised 2026-10-08 by the user**: "什么运走存下来，我觉得就不应该存在字节形式的啊 /
  完全不应该存在！！！". It is the second half of the ruling that produced
  `mod-content-typed-registration.md`, and it covers the surfaces where the payload really does cross a
  boundary — the half the first ticket deliberately left alone. Both stages landed 2026-10-09; the
  mod-visible contract now carries no `byte[]` outside the model's own binary leaf.
- Priority: Critical
- Category: Mod platform / protocol / architecture
- Related: `docs/backlog/review/mod-content-typed-registration.md` (step 1 of the same ruling),
  `docs/backlog/todo/mod-authored-effects.md` (follows this one, and would otherwise be built twice),
  `docs/backlog/review/mod-defined-wire-packets.md` (the packet surface),
  `docs/backlog/review/mod-payload-null-collection-tolerance.md` (the rule that exists because of the payload
  step), `docs/en/reference/mod-api.md`
- Source: the user's 2026-10-08 ruling, quoted above.

## The principle

The question that decides the shape is not "does it cross a boundary" but **who defines the shape**.

- Where **CUO defines it**, the contract must be typed — the content kinds, the status projections. An opaque
  payload there erases a type CUO itself owns.
- Where the **mod defines it**, the framework still must be able to see the structure. An opaque blob means the
  framework cannot validate it, bound it structurally, log it meaningfully, show it in the console, or help a
  mod migrate it — and every mod reinvents malformed-payload handling on its own.

So the shape to build is a CUO-owned **typed data model**: numbers, text, booleans, lists, maps, and exactly one
explicit binary leaf for data that really is bytes. A mod that wants its own compact encoding puts it in that
leaf, on purpose — the escape hatch is a value inside the model, never the API's envelope.

## The precedent (checked 2026-10-08, against the platforms a mod author knows)

Registration APIs are typed wherever the library owns the semantics of what is registered: Forge and Fabric
register typed objects into a typed registry (`DeferredRegister`, `Registry.register`), Unity registers typed
assets, ASP.NET's container registers typed services, Kubernetes describes a custom resource with a schema, and
a schema-first RPC describes it in a `.proto` — a `bytes` field there is an explicit value type, never the
envelope.

Byte-shaped APIs do exist, and they are transport or storage primitives whose contract IS "opaque payload, the
library does not know what is inside": a queue, a cache (`IDistributedCache`), a key-value store, a file. The
closest mod-platform example is Bukkit's plugin message channel, whose listener receives `byte[]` — and that is
the part of the platform everybody hand-rolls serialization for; the same channel later gained a typed buffer
(Netty's `ByteBuf`, with read/write primitives) on Paper, and Forge/Fabric never offered bytes there at all:
their network payloads are a typed buffer the mod writes fields into.

So the split is not "local versus networked": it is "the library knows the shape, or it does not". CUO knows it
for content (its own DTOs) and for status (its own projections), and does not know it for a mod's own packets
and data — which is exactly where a typed data model replaces the blob.

## The census (the whole public surface, taken 2026-10-08 from the API baseline)

Every `byte[]` on the mod-visible contract, with its stability level. The census is the baseline file
(`docs/contracts/abstractions-api-baseline.txt`), so it is exactly what a mod can see — and it is complete.

| Surface | Level | What it carries | Verdict |
|---|---|---|---|
| `IModContent.TryRegister` (both overloads), `ModContentDefinition` (ctor + `Data`), and the nine content DTOs' `ToPayload` / `FromPayload` | Stable | a definition's serialized form | a typed definition — step 1 |
| `IModContext.Network` → `IModNetwork.SendToHost` / `SendToPeer` / `Broadcast` / `MessageReceived` | Stable | a mod's own message bytes | the data model (the raw channel `IModContext.Packets` already supersedes for new work) |
| `IModPackets.SendToHost` / `SendToPeer` / `Broadcast`, `IModPacketContext.Payload` | Experimental | a declared packet's payload | the data model |
| `IModData.TryGet` / `TrySet` / `TryApplyShared`, `IModState.TryGet` / `TrySet` | Stable | a mod's own keyed value | the data model |
| `IModStatusRuntime` (six methods), `IModStatusTransport` (three), `ModStatusUpdate.Value` / `ForBody` / `ForLimb` / `ToPayload` / `FromPayload`, `ModStatusMoodleRequest.Payload` | Stable | a mod's status value, and CUO's own update envelope | the data model for the mod's value; a typed wire contract for CUO's envelope |
| `ModBodyFormulaProjection`, `ModLimbProjection` (`ToPayload` / `FromPayload`) | Stable | CUO's own projection of body/limb state | a typed wire contract |
| `IModNativeApi`'s admitted value surface (it lists `byte[]` among the "framework-safe values") | Advanced | a native operation's arguments and result | the data model replaces `byte[]` there too; the registry's own shape is `mod-api-typed-seams.md` |
| `SaveArchiveEntry.Content` (save files) | internal | a file's bytes | kept — a binary leaf of a FILE, not an API envelope |
| The data model's own binary leaf | — | data that really is bytes | the one place a mod says "this value is binary", on purpose |

## What the change has to decide

1. **The value model**: which value kinds exist, how deeply they nest, what the per-value and per-message
   budgets are, and how a mod builds one from its own C# type (a typed builder; a convenience serializer that
   maps a plain object graph onto the model is a candidate, not a requirement).
2. **Validation and diagnostics**: a malformed value is a named refusal with the path inside the model, in the
   shape the content providers already use.
3. **Version handling**: the model is self-describing, so a schema version travels with it as it does today —
   and the framework can finally say WHICH field changed instead of only "the decode failed".
4. **The wire**: packets and status ride messages whose payload becomes the model; the protocol number follows
   decision 241 (the pre-release baseline is frozen, so the change ships with its commit and its ticket).
5. **What stays byte-shaped**: file bytes in the save archive, and the model's own binary leaf.

## Frozen shape (2026-10-09)

The ruling fixes *what*: a CUO-owned typed data model, one explicit binary leaf, no opaque envelope left on
the mod-visible contract. This section fixes *how* so the implementation follows a written shape. It is
staged, each stage its own cycle with its own gates and deployment:

- **Stage A — the mod's own data over the wire (LANDED 2026-10-09)**: the model, its codec,
  `IModNetwork`, `IModPackets` and `IModPacketContext`, and — forced by the first three — CUO's own status
  frame: `ModStatusUpdate`, `IModStatusRuntime`, `IModStatusTransport` and the two projections. The codec
  was the last user of `ModPayloadCodec`, so that file and the `DataContractSerializer` decode seam beside
  it are deleted in stage A rather than in B.
- **Stage B — the stores and the remaining surfaces (LANDED 2026-10-09)**: `IModData`, `IModState` (and its
  file), the runtime moodle request's remaining call sites and `IModNativeApi`'s admitted value list, plus
  the last of the `byte[]` on the mod-visible contract. Stage B is what retires the last of decision 244's
  paper trail; its decode half already went with stage A.

The census rows are split the same way, and a surface never carries both spellings: a stage moves its rows
whole.

### 1. The value model (`ModValue`, `ModValueKind`)

- Kinds: `Boolean`, `Integer` (`long`), `Number` (`double`), `Text` (`string`), `Binary`, `List`, `Map`.
  Two numeric kinds because one `double` would lose a Steam id (2^53) silently, and the model's whole point
  is that a value means what it says. **No Null kind**: absence is structural — a `false` from a `Try`, a
  map field that is not there — so nothing has to decide what a nil element meant.
- `ModValue` is a sealed, immutable class; every factory copies what it is handed, so a value can be shared,
  cached and read from two threads without a defensive copy (which deletes the clone-on-read/write
  ceremony every one of these surfaces does today).
- Factories: `ModValue.Boolean`, `.Integer`, `.Number`, `.Text`, `.Binary`, `.List(params ModValue[])`,
  `.Map(params (string Key, ModValue Value)[])`. A null argument is an `ArgumentNullException` (a
  programming error in the mod's own code) except a null `params` array, which is an empty container —
  the same "null means none" rule the collection members already carry.
- Accessors: `Kind`, `TryGetBoolean`, `TryGetInteger`, `TryGetNumber` (accepts `Integer` as a widening),
  `TryGetText`, `TryGetBinary`, `Items`/`Fields` (**null** unless the kind is `List`/`Map`, so a wrong kind
  cannot read as "empty"), `TryGetField(string name, out ModValue value)`.
- `Equals`/`GetHashCode`/`==`/`!=` are structural (`IEquatable<ModValue>`), and `ToString()` renders a
  bounded, log-safe form — the ticket's own reason for typing this at all is that the framework must be
  able to log it.
- The binary leaf is an explicit value, not an envelope: one kind whose payload the framework never
  interprets. Spelled `ReadOnlyMemory<byte>` rather than `byte[]` on purpose — rule 15's letter admits a
  binary leaf and bans the erased spelling, and no TYPED path mutates a value. (The letter of "immutable" is
  about the model's own API: `TryGetBinary` hands out the value's own memory, so a caller willing to call
  `MemoryMarshal.TryGetArray` can reach the backing array. That is the stage B review's N2, recorded rather
  than claimed away — it needs a deliberate call, the mod is in-process trusted code, and nothing a peer
  sends reaches it.)

### 2. The budgets and the refusal (one validator)

Nothing validates a value except the encoder: accepting a value IS being able to encode it inside the
budgets, so there is no second rule to drift.

| Budget | Value | Why that number |
|---|---|---|
| Depth | 8 | the deepest contract CUO itself carries is 2; a mod's own structure has room and a recursive builder still refuses |
| Values per graph | 4096 | bounds the walk, not the shape |
| Entries per list/map | 1024 | the same order as the stores' own per-mod caps |
| Text | 16 KiB encoded | a payload rail, not a string limit |
| Binary leaf | 32 KiB encoded | the frame's own 64 KiB cap is the outer bound |
| Encoded total | the surface's own cap (`ModChannel.MaxPayloadBytes` 64 KiB on the wire) | one place, already documented |

A refusal is a `bool` plus a reason string that names the PATH inside the model
(`$.targets[3].hp: a number must be finite`) and the budget it broke, in the same shape the content
providers already refuse in. Decode enforces the same budgets WHILE it walks — the depth is checked before
recursing, and a length is checked against the bytes actually left — so a malformed or hostile payload is
refused rather than recursed into. A non-finite number and text that is not valid UTF-16 are refused at
encode: the model round-trips exactly or it does not travel.

### 3. The wire

The value model gets a canonical binary encoding owned by CUO (`ModValueCodec`, `internal` in Runtime — the
mod-visible contract never sees bytes). Tag byte per value, fixed-width little-endian lengths, containers
length-prefixed. `ModMessageMsg.Payload` keeps carrying `byte[]`: the frame's field is CUO's own transport
detail and both of its ends are CUO's, so the protocol message does not change shape — only what the bytes
mean, and the receive side decodes before any mod callback runs (a decode failure is dropped with the
refusal, exactly like the over-cap drop beside it). Protocol number: pre-release, so decision 241 freezes
it; a wire change ships with its commit and its ticket.

The 64 KiB rail is measured on the ENCODED size, at both ends, where it is measured today.

### 4. What does NOT change

- `IModNetwork`'s `void` (no-op plus a log) and `IModPackets`' `bool` refusal shapes: this ticket retypes
  the payload, it does not redesign the refusal story of a surface that has one.
- The permission, role, session, rate-limit and fan-out rules of either surface.
- `SaveArchiveEntry.Content` (a file's bytes), the save archive, and `ModStateFile`'s own byte fields: a
  file's content is a binary leaf of a FILE, which the census already keeps.
- No compatibility layer of any kind (already a non-goal above).

### 5. What the implementation owes the reader

A `docs/en/reference/mod-api.md` section per block for the model (kinds, factories, accessors, budgets, the
refusal shape), the two how-to pages that show `Encoding.UTF8.GetBytes` rewritten, the baseline lines, the
alignment pairs recomputed, the cycle's self-check, and a decision entry recording the shape.

## What landed (2026-10-09, stage A)

The shape above, implemented and verified: `ModValue`/`ModValueKind`, `ModValueCodec` (the one validator),
`IModNetwork`, `IModPackets`, `IModPacketContext`, `ModStatusUpdate` (a map whose field names are its wire
contract), `IModStatusRuntime`, `IModStatusTransport` (`TryHandleStatusUpdate`), the two projections
(`ToValue`/`TryFromValue`), the retirement of `ModPayloadCodec` and of `[DataContract]`, the example mods,
the API baseline (25 tombstones, plus two entries whose type changed in place), the terminology registry, both document blocks and the alignment record.
`docs/evidence/selfchecks/mod-api/mod-value-model-selfcheck.md` is the record; the census rows this cycle
moved are the network, packet, status-frame, status-value and projection rows, whole.

Two facts worth carrying forward:

- **The per-delivery byte copy is DELETED, not moved.** The old contract promised a handler its own copy of
  the payload precisely because a handler could rewrite it; an immutable value makes the copy and the
  promise unnecessary, and the case that pinned the copy is replaced by one that pins what a value
  guarantees (`OneImmutableValue_ServesTheWholeDelivery`).
- **The null-collection census shrank by a member for a real reason.** The runtime moodle request's
  `Payload` is a `ModValue` now, so it is not a collection member; the census is
  `ModNullCollectionRuleTests` with 26 rows, and the model's read-only `Items`/`Fields` views are a named
  group rather than rows. `review/mod-payload-null-collection-tolerance.md` records the retirement.

## What landed (2026-10-09, stage B)

The stores and the last byte-shaped surfaces, whole:

- **`IModData`** takes and returns a `ModValue` (`TryGet` / `TrySet` / `TryApplyShared`); the slot keeps the
  value itself, so the clone on read and on write is deleted rather than moved, and both stores accept a
  value exactly when the one encoder can encode it inside their own 64 KiB cap — the policies call it and
  carry its refusal into their log line instead of owning a second value rule.
- **`IModState`** takes and returns a `ModValue`; each entry is persisted as the value's canonical encoding
  in the file's own byte field (a FILE's binary leaf, which the census keeps).
- **The mod-state file's version becomes 2.** A version-1 file held whatever bytes the mod chose, and the
  same string can now decode as a different value — a lone `0x00` reads as `false` — so no migration can
  tell the two apart: the file is refused whole, which is the degradation its own contract already promises
  for a version this build does not read. A single entry that is not one value is dropped by name with its
  reason while the rest of the table loads.
- **`IModNativeApi`'s admitted value surface** trades its `byte[]` arm for `ModValue`, and its array shapes
  are decided by rank and element type rather than by twelve `T[]` type patterns. Reason, measured on the
  built assembly: on this stack a signed and an unsigned array of the same width are NOT distinguishable by
  pattern (`value is sbyte[]` is true for a `byte[]` and the other way round; likewise `short`/`ushort`,
  `int`/`uint`, `long`/`ulong`, while `bool[]`, `float[]`, `decimal[]`, `string[]` and `char[]` are exact),
  so a pattern list cannot state the one rule this change exists to state — that a raw byte array is no
  longer admitted. The element-type comparison states it and keeps the signed twin admitted.
- **The runtime moodle request needed no code**: stage A had already moved its `Payload` to `Value`, and
  this cycle verified its two call sites (`ModStatusMoodleProjection` building it from the store's value,
  the resolver registry carrying it).

The census rows this cycle moved are the two store rows and the native-API row, whole; the file's own leaf
and the model's binary leaf are the only two rows left, and both are meant to stay. `IModData`, `IModState`
and the native value list are recorded as five tombstones plus five new baseline lines.

Two facts worth carrying forward:

- **A save file's byte semantics are part of its version.** The mod-state file's shape did not change (the
  same field, the same keys) and its version still had to move, because what a stored string MEANS changed
  and one old value (a lone `0x00`) would otherwise have come back as a valid new one.
- **Two blocks' byte-era samples were still standing after stage A** — the Mod UI snippet's
  `Encoding.UTF8.GetBytes("ping")` in both languages and the save-data how-to's encoded JSON — and this
  cycle rewrote them onto the model. A doc example that cannot compile against the contract is a defect,
  not a leftover.

Limits: no game process and no two-client session were available this cycle, so a live mod's runtime data
and its cross-session state are the acceptance batch's rows; the platform behaviour behind the native
value list (the signed/unsigned array equivalence) was measured on the built assemblies rather than
reasoned about.

## Non-goals

- Not a compatibility layer: the old payload overloads do not survive beside the model.
- Not raw game or Unity types in the API (decision of 2026-10-08 on the effects surface: engine types only
  where a handle is explicitly an escape hatch).
- Not a general-purpose serialization framework for mods: the model carries mod data across CUO surfaces, and a
  mod that wants its own format inside the binary leaf may have it.

## Order

After `mod-content-typed-registration.md` (the same ruling, smaller, and the contract the effects surface sits
on) and before `mod-authored-effects.md`: an effect that reports what it did to peers is exactly the kind of
surface that would otherwise be built on the byte envelope and then rebuilt.
