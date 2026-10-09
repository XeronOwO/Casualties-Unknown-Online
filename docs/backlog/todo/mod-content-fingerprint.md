# Two peers that materialize different content are never compared

- Status: Todo — split out of `docs/backlog/review/mod-content-attribute-declarations.md` when that ticket's
  stage B landed (2026-10-09), carrying its acceptance row verbatim. Not invented here: making a definition
  able to COMPUTE its values is what made the field load-bearing.
- Priority: Medium
- Category: Mod platform / save + handshake
- Related: `docs/backlog/review/mod-content-typed-registration.md` (the typed contract; it already records
  that the field is written as `string.Empty`), `docs/architecture/save-archive-format.md` (the manifest the
  field lives in), `docs/backlog/done/world-determinism-world-fingerprint.md` (the OTHER fingerprint — world
  block state, a local diagnostic; this ticket is about content).

## What is wrong today

`SaveManifest.ContentFingerprint` is produced as `string.Empty` (`src/CasualtiesUnknownOnline.Runtime/Persistence/WorldCutWriter.cs`),
and nothing reads it back as a comparison: two peers, or two saves, can materialize different content under
the same id and the same mod version and nothing notices. The mod handshake is the only consistency
boundary, and it compares mod id / SemVer / permissions / network mode — none of which says what a
declaration actually produced.

## What this ticket owes

- The acceptance row moved here verbatim from the parent ticket: **two clients that materialize different
  content under the same id and the same mod version are reported as a named mismatch rather than silently
  accepted.**
- A decision on WHERE the comparison happens and what it hashes. The candidates are a digest over the
  materialized content written into `SaveManifest.ContentFingerprint` (durable, compared on load), a
  handshake-side content digest (compared between peers), or the save digest plus a peer comparison. It
  has to be decidable what a peer can honestly assert about another peer's content: ids, kinds and schema
  versions are stable, while a computed member's value is not hashable in general — the ticket starts by
  naming the subset both sides can agree on, and refuses (with a named mismatch) the rest rather than
  hashing something unstable.

## Non-goals

- No world-state fingerprint: that is `docs/backlog/done/world-determinism-world-fingerprint.md`.
- No serialization of a definition: definitions stay code and every peer re-declares them.
