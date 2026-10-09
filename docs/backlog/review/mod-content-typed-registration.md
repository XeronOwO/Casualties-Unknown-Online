# Content registration carries its type: a typed definition instead of an opaque payload

- Status: Review — implementation landed 2026-10-08 (see *What landed*) and was independently reviewed the same day; the acceptance batch is pending. It was **raised 2026-10-08 by the user**, who rejected the current shape in one line: "不能接受，你做
  接口抽象为的是什么？你搞这种鬼玩意，不是反模式吗？". The objection is the type erasure itself, and it holds
  at this position: the mod hands a typed contract OVER and the contract turns it into bytes, so every consumer
  re-derives the type at run time instead of the compiler checking it.
- Priority: Critical
- Category: Mod platform / mod API
- Related: `docs/backlog/todo/mod-api-no-opaque-envelopes.md` (step 2 of the same ruling: the surfaces where a
  payload does cross a boundary), `docs/backlog/todo/mod-authored-effects.md` (the code-registration surface
  this one must precede — it is an API over the same contract),
  `docs/backlog/review/mod-declared-behaviour-with-no-function.md`,
  `docs/backlog/review/mod-payload-null-collection-tolerance.md` (the rule that exists because of the payload
  step), `docs/backlog/review/mod-content-kind-with-no-provider.md`,
  `docs/en/reference/modification-policy.md` (the `Abstractions` baseline is a reviewed change)
- Source: the user's 2026-10-08 ruling, quoted above.

## Why this ticket exists

`IModContent.TryRegister(id, kind, byte[] data, schemaVersion)` takes the payload as opaque bytes, and every
content DTO ships `ToPayload()` / `FromPayload()`. Its own doc states the intent — the framework "never
interprets the mod's payload" — and the price is paid at the one place the abstraction exists for: two
independent strings (`kind` and the payload) must agree at run time, and every provider has to remember the same
three steps (compare the kind, decode, refuse when the decode returns null). A wrong pairing is a runtime
refusal today, not a compile error.

## What the bytes actually do (measured 2026-10-08)

Nothing that needs them:

- **They are not sent.** `src/CasualtiesUnknownOnline.Protocol/` carries no content-definition message at all;
  cross-player consistency comes from both peers having the mod and the handshake comparing mod id / version /
  permissions, so each client materialises its own copy of the content.
- **They are not stored.** The save keeps content **ids** and a `SaveManifest.ContentFingerprint` string; an id
  the current content set no longer has is salvaged per entry (`DamageReport.EntryReason.ContentMissing`). The
  fingerprint field is currently written as `string.Empty` everywhere it is produced (`WorldCutWriter`), so not
  even that reads the payload.
- **They are not compared.** Nothing hashes or diffs a definition's bytes.

The opaque payload is the right shape where data really crosses a boundary, and the repository already uses it
there: mod-defined packets (`IModPackets`), mod runtime data/state, and the status transport (`ModStatusTransport`
broadcasts `update.ToPayload()`). This ticket is about the case where it was applied to contracts whose shape
CUO itself defines.

## The shape to build

1. **A non-generic base interface for a registered definition** — the id, the kind and the schema version, and
   nothing else. Each content DTO implements it; the DTO keeps its own typed members.
2. **`TryRegister` takes the definition**, so the call site is type-checked: the kind travels with the object
   instead of beside it, and a definition cannot be filed under another kind by hand.
3. **Providers claim by kind and cast** (`definition as ModItemDefinition`) — a failed cast is the same named
   refusal a failed decode is today, but the producer side can no longer build the mismatch at all.
4. **The registry, the catalog, the ownership query, the console resource-id completion and the binder keep
   working on the base interface**, so the property the payload step was buying — a store that knows no content
   schema, and a mod that can register a kind of its own — is kept in full.
5. **Serialization stays where it crosses a boundary**: packets, runtime data, status updates. The content DTOs
   stop being serialization contracts, and `ModPayloadCodec` (decode + the null-collection normalisation + the
   depth bound), the content payload's 64 KiB cap and the null-collection census shrink to the surfaces that
   still travel.

## Scope of the change

`Abstractions` (the base interface, nine content DTOs, `IModContent`), the runtime registry/envelope/binder, the
adapter providers, `ModContentDisplayName` (a property read instead of a decode), the public-surface baseline
(`docs/contracts/abstractions-api-baseline.txt` — a reviewed change), `docs/en/reference/mod-api.md` with its
Chinese pair and the alignment record, and the suites that drive `ToPayload()` today. Pre-release, so
compatibility is not a design input.

## Process lesson this ticket must leave behind

The shape being fixed was a *recorded* decision — the mod-API page says why the bytes are there ("the framework
still stores bytes opaquely") — so the miss was not carelessness. It was optimisation on one axis (one registry
that serves every kind) with the bill paid on another (the type, at the contract), and the question "who reads
this form" was never asked because the answer was assumed to be "somebody across a boundary".

The generalizable rule lands in `AGENTS.md`'s *Engineering Discipline* with this change: **a mechanism needs a
named consumer — say which caller reads what it produces; a form nothing transports, compares or interprets is
a guess, not a design.** The repository already applies that judgement to content kinds and to quality
references; this is the same judgement aimed at its own internals.

## What landed (the frozen shape)

1. **`IModContentDefinition`** (Abstractions): `Id`, `Kind`, `SchemaVersion` and nothing else. The nine
   content DTOs implement it; each keeps its own typed members.
2. **`TryRegister(IModContentDefinition definition)`** replaces both byte[] overloads: the call site is
   type-checked, the kind travels with the object (`Kind` is a constant of the DTO's type), and a
   definition cannot be filed under another kind by hand.
3. **Providers claim by kind and cast** (`registration.Definition is ModItemDefinition definition`); the
   binder keeps routing by `Kind`, and a mod-authored definition under a claimed kind is the same named
   refusal a failed decode used to be.
4. **Registry, catalog, ownership query, console resource-id completion and binder work on the base
   interface**, so the property the payload step bought is kept: a store that knows no content schema, and
   a mod that can register a kind of its own by implementing the interface.
5. **Serialization stays where it crosses a boundary.** `ModPayloadCodec` now serves `ModStatusUpdate`
   and the two status projections only; the content DTOs and their member objects lost `ToPayload`,
   `FromPayload`, `[DataContract]` and `[DataMember]`, and `ModContentPolicy` lost `MaxDefinitionBytes`
   and `IsValidData`. `ModContentDefinition` is deleted (registry keeps the instance the mod built).
6. **Decision 244's member half is kept**: every collection member still coalesces a null write, because a
   mod fills those objects in code — the decode half is what shrank. `ModPayloadNullCollectionTests` now
   discovers the two groups separately (travelling contracts: three payload shapes; mod-built
   declarations: a null write for each member).

## Mechanism inventory (every touched mechanism, with its source)

| Mechanism | Where | Evidence |
|---|---|---|
| Registration contract | `Abstractions/IModContent.cs`, new `IModContentDefinition.cs` | source + `ModContentTests` |
| Nine content DTOs | `Abstractions/Mod{Item,Recipe,Liquid,LiquidTile,Tile,Building,Structure,Status,Moodle}Definition.cs` | source + the nine definition suites (`Kind` constant, defaults, null members) |
| Per-mod registry | `Runtime/Session/Mods/ModContentAdapter.cs` | source + `ModContentTests` |
| Registration record / policy | `ModContentRegistration.cs`, `ModContentPolicy.cs` | source + `ModContentTests.PolicyCaps_*` |
| Display name seam | `Runtime/Session/Content/ModContentDisplayName.cs` | source + `ModContentResourceLocationSourceTests` |
| Binder | `Runtime/Session/Mods/ModContentBinder.cs` | source + `ModContentBinderTests` |
| Catalog / owner query / console resource ids | `ModContentCatalog.cs`, `ModContentOwnerQueryAdapter.cs`, `ModContentResourceLocationSource.cs` | source + their suites |
| Nine adapter providers | `GameAdapter/Content/GameAdapter*ContentProvider.cs` | source + the nine `Patching/*ProviderTests` suites |
| Travelling payloads | `ModPayloadCodec.cs`, `ModStatusUpdate.cs`, the two projections | source + `ModPayloadNullCollectionTests` (1 payload member, 3 shapes) |
| Public surface | `docs/contracts/abstractions-api-baseline.txt` | `ApiSurfaceGateTests` (removals carry tombstones) |
| Human docs | `docs/{en,zh}/reference/mod-api.md`, `how-to/register-content.md`, `start/your-first-mod.md`, `reference/glossary.md` | `docs/standard/alignment.txt` re-recorded for all four pairs |
| Example mod / acceptance recipe | `src/CasualtiesUnknownOnline.ModExample/ExampleMod.cs`, `tools/acceptance/recipes/building-template-inject.cs` | source; the recipe is the live-provider proof of the typed seam |

## Self-check table

| Mechanism × change | What proves it |
|---|---|
| A definition carries its own identity | the nine suites assert each DTO's `Kind` constant, `Id` and `SchemaVersion`; `ModContentTests.BindRegistersContent_ContextExposesIt` reads them off the real stack |
| A wrong pairing is unreachable at the call site | `TryRegister` takes only the definition; the provider's cast refusal is driven family-wide by `ModContentNullCollectionBindingTests.EveryProvider_RefusesADefinitionOfAnotherTypeFiledUnderItsKind` (nine real providers, one `StubContentDefinition` per claimed kind) |
| The registry keeps the mod's instance | `ModContentTests.RegisteredDefinition_IsStoredAsTheInstanceTheModHandedOver` (`Assert.Same`) |
| Rails still refuse | `ModContentTests` — null definition, empty/short/long kind, invalid id, non-positive schema version, duplicate, count cap |
| Providers read typed members | the nine `Patching/*` provider suites, `ModContentNullCollectionBindingTests` through the real binder |
| The serialized surface shrank to what travels | `ModPayloadNullCollectionTests`: one payload contract member driven through all three shapes, 27 mod-built members driven with a null write |
| Nothing serializes a content definition | no `ToPayload`/`FromPayload` under the content DTOs (grep), the removals tombstoned in the baseline |
| Docs match the code | `alignment.txt` re-recorded for the four edited pairs, self-check manifest rows marked historical |

## Non-goals

- Not the mod-packet / runtime-data / status payloads: those cross a boundary and stay opaque.
- Not the effects surface itself — that is `mod-authored-effects.md`, and it is designed on top of this shape.
- No compatibility shim: the old overload does not survive beside the new one.
