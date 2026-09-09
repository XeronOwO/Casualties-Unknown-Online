# Runtime entity creation can be absorbed by a markerless same-prefab copy

- Status: Todo
- Priority: Low-Medium
- Category: Network / sync coverage / world entities
- Source: round-4 independent re-review of `review/runtime-entity-spawn-backfill.md` (2026-09-09)
- Related: `review/runtime-entity-spawn-backfill.md`, `todo/enemy-snapshot-and-attack-recovery.md` (N1)

## Problem (evidence)

`RuntimeEntityMatch.FindIndex` binds a creation record to its own copy by exact creation
key, then falls back to a MARKERLESS same-prefab copy strictly inside 1 m
(`src/CasualtiesUnknownOnline.Runtime/Session/World/RuntimeEntityMatch.cs:49-73`), and the
adapter stamps that copy with the record's key
(`src/CasualtiesUnknownOnline.GameAdapter/World/EntitySpawnSync.cs:238`).

The fallback exists because two real copies carry no marker:

- the enemy-domain late-join backfill copy (`EnemySyncCoordinator.CreateRuntimeSpawn`,
  materialized from `EnemySnapshot.RuntimeSpawns` at the animal's CURRENT position), and
- a generated entity.

Consequence: when a runtime creation lands within 1 m of an unrelated markerless
same-prefab copy, the record binds that copy and the new entity exists only on the
creating side (one-sided missing entity). The mirror risk — the backfill copy drifts
beyond 1 m, so a surviving live re-report creates a duplicate — is recorded in the E3
ticket's Known limitations.

## Goal

Remove the need for the positional fallback, or bound it with a positive identity:

1. Give the enemy-domain backfill copy the creation key (the host knows both the
   `NetworkEntityId` and the creation record at relay time) so the copy can carry a
   `RuntimeEntityCreation` marker; then delete the positional pass. This is N1's identity
   work in `todo/enemy-snapshot-and-attack-recovery.md`.
2. Prove (or disprove) with evidence that a runtime creation can coincide with a
   generated entity of the same prefab inside 1 m; if it can, decide the tie-break.

## Acceptance

- The positional pass is either deleted (identity covers every real copy) or keeps a
  documented, tested tie-break.
- A regression test covers the absorption case (a markerless same-prefab decoy inside the
  radius must not swallow the record, or must do so by an explicit rule).
- Full build + tests + `dotnet format` green; the E3 matrix row's loss-semantics cell is
  updated with the outcome.
