# Two peers that materialize different content are never compared

- Status: Review — **landed 2026-10-10** (see *What landed*): nothing is left to develop here, and the
  acceptance batch is pending. Split out of `docs/backlog/review/mod-content-attribute-declarations.md`
  when that ticket's stage B landed (2026-10-09), carrying its acceptance row verbatim. Not invented
  here: making a definition able to COMPUTE its values is what made the field load-bearing.
- Priority: Medium
- Category: Mod platform / save + handshake
- Related: `docs/backlog/review/mod-content-typed-registration.md` (the typed contract; it already records
  that the field is written as `string.Empty`), `docs/architecture/save-archive-format.md` (the manifest the
  field lives in), `docs/backlog/done/world-determinism-world-fingerprint.md` (the OTHER fingerprint — world
  block state, a local diagnostic; this ticket is about content).

## What was wrong

`SaveManifest.ContentFingerprint` was produced as `string.Empty`
(`src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldCutWriter.cs`), and nothing read it back as a
comparison: two peers, or two saves, could materialize different content under the same id and the same mod
version and nothing noticed. The mod handshake was the only consistency boundary, and it compared mod id /
SemVer / permissions / network mode — none of which says what a declaration actually produced.

## What this ticket owed

- The acceptance row below, judged at the acceptance batch.
- A decision on WHERE the comparison happens and what it hashes — taken; see *What landed* and decision 254.
  The ticket's own rule stands as the scope of that decision: what a peer can honestly assert is the subset
  both sides agree on — ids, kinds and schema versions are stable, while a computed member's value is not
  hashable in general — so a difference inside that subset is named, and the rest is deliberately not hashed
  rather than hashed into a comparison that means nothing.

## What landed (2026-10-10)

One canonical form, both boundaries, derived from the registry at each read. `ModContentFingerprint` renders
one canonical line per registered content entry — the owning mod id, the address (the canonical
`namespace:path` when the mod declared a namespace, the mod-scoped id otherwise), the kind and the schema
version — sorts them and hashes the joined text. That
address set IS the subset both peers can honestly assert: a definition's other members may COMPUTE their
values, so a value is not hashable in general and a hash of one would report differences that are not
differences. Every variable field is length-prefixed, and that is a rule rather than decoration: the
registration rails only require a kind to be non-whitespace and a mod id to be non-blank, so an unescaped
rendering lets one mod's id stand in for another pair of entries — two different content sets with one text.

**Between peers**: `ModInfoMsg.ContentFingerprint` (null = "this mod registered no content" on both sides,
never "unknown") is filled by the session's handshake list and compared by the host per mod id, only for a mod
both sides list: a difference is logged with the mod, both fingerprints and the mode, and the member is
admitted. **Never a refusal**, by user ruling (2026-10-10: report it, do not block play — the comparison covers
the address of every entry and never a computed value, so it cannot be complete enough to gate entry). The line
is the record a divergence is diagnosed from, and a mod author whose values change under an equal address set
has to declare that in the mod version or the entry's schema version, which is the only place it can be said.

**Durably**: a cut records the whole-set fingerprint in `SaveManifest.ContentFingerprint` — the field §3.2
reserved and every producer wrote as `string.Empty` — and a load compares it with the live set and REPORTS a
difference (`DamageReport.EntryReason.ContentMismatch`, repair mode, §6.1's rule for a build difference).
What that adds over the per-entry salvage is the drift salvage cannot see: a content set that changed while
every stored content id still resolves. Empty on either side is "unknown" and is never compared.

**Structure**: the content registry became a leaf (`ModContentStore`, with `ModContentAdapter` as one mod's
write guard over its own slice), because both readers must not resolve `ModService` — it reads the session,
a cycle. The session's `IModListProvider` is therefore its own leaf composing the discovery registry with the
store, and the mod-visible read view `IModContentControl` is unchanged.
`WorldSnapshotPayload.ContentFingerprint` was DELETED in the same round: a payload member nothing read.

Evidence: `docs/evidence/selfchecks/mod-api/mod-content-fingerprint-selfcheck.md`; decision 254.

## Acceptance (moved here verbatim from the parent ticket, judged at the acceptance batch)

- **Two clients that materialize different content under the same id and the same mod version are reported
  as a named mismatch rather than silently accepted.**

## Non-goals

- No world-state fingerprint: that is `docs/backlog/done/world-determinism-world-fingerprint.md`.
- No serialization of a definition: definitions stay code and every peer re-declares them.
