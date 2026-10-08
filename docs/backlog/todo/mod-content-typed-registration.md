# Content registration carries its type: a typed definition instead of an opaque payload

- Status: Todo — **raised 2026-10-08 by the user**, who rejected the current shape in one line: "不能接受，你做
  接口抽象为的是什么？你搞这种鬼玩意，不是反模式吗？". The objection is the type erasure itself, and it holds
  at this position: the mod hands a typed contract OVER and the contract turns it into bytes, so every consumer
  re-derives the type at run time instead of the compiler checking it.
- Priority: High
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

## Non-goals

- Not the mod-packet / runtime-data / status payloads: those cross a boundary and stay opaque.
- Not the effects surface itself — that is `mod-authored-effects.md`, and it is designed on top of this shape.
- No compatibility shim: the old overload does not survive beside the new one.
