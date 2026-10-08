# No opaque payloads in the mod API: a typed data model instead of a byte envelope

- Status: Todo — **raised 2026-10-08 by the user**: "什么运走存下来，我觉得就不应该存在字节形式的啊 /
  完全不应该存在！！！". It is the second half of the ruling that produced
  `mod-content-typed-registration.md`, and it covers the surfaces where the payload really does cross a
  boundary — the half the first ticket deliberately left alone.
- Priority: Critical
- Category: Mod platform / protocol / architecture
- Related: `docs/backlog/todo/mod-content-typed-registration.md` (step 1 of the same ruling),
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
