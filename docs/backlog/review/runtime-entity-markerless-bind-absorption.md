# Runtime entity creation can be absorbed by a markerless same-prefab copy

- Status: Review
- Priority: Low-Medium
- Category: Network / sync coverage / world entities
- Source: round-4 independent re-review of `review/runtime-entity-spawn-backfill.md` (2026-09-09)
- Related: `review/runtime-entity-spawn-backfill.md`, `todo/enemy-snapshot-and-attack-recovery.md` (N1)

## Problem (evidence)

`RuntimeEntityMatch.FindIndex` bound a creation record to its own copy by exact creation
key, then fell back to a MARKERLESS same-prefab copy strictly inside 1 m
(`src/CasualtiesUnknownOnline.Runtime/Session/World/RuntimeEntityMatch.cs` pre-fix
lines 49-73), and the adapter stamped that copy with the record's key
(`src/CasualtiesUnknownOnline.GameAdapter/World/EntitySpawnSync.cs:238`).

The fallback existed because two real copies carried no marker:

- the enemy-domain late-join backfill copy (`EnemySyncCoordinator.CreateRuntimeSpawn`,
  materialized from `EnemySnapshot.RuntimeSpawns` at the animal's CURRENT position), and
- a trap-layout materialization (`TrapLayoutApplication.Materialize`).

Consequence: when a runtime creation landed within 1 m of an unrelated markerless
same-prefab copy, the record bound that copy and the new entity existed only on the
creating side (one-sided missing entity). The mirror risk — the backfill copy drifting
beyond 1 m, so a surviving live re-report creates a duplicate — was recorded in the E3
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

## Landed (2026-09-09) — the positional pass is DELETED, identity covers every real copy

**Decision (design option 1, completed).** The 1 m markerless fallback is removed
outright; `RuntimeEntityMatch.FindIndex` is now a single exact-creation-key pass
(`RuntimeEntityMatch.cs:46`). The two markerless copy families the pass existed for were
given their identity at the source instead:

- **Enemy-domain backfill (N1 identity half).** The host's own animal copy carries the
  `RuntimeEntityCreation` marker (it was created by the entity-spawn channel), so
  `EnemySyncCoordinator.Capture` publishes that key on the `EnemyEntity`
  (`EnemyEntity.CreationKey`) and `EnemyEntity.ToEnemySpawnEntryMsg` puts it on the wire
  (`EnemySpawnEntryMsg.CreationKey`, ProtoMember 8). `CreateRuntimeSpawn` stamps the
  materialized backfill copy with the SAME key, so a surviving live re-report binds it by
  key instead of duplicating the animal — the drift-beyond-1-m mirror risk is closed too,
  because identity is distance-free.
- **Trap-layout materialization.** `TrapEntityScan` reads the host's own
  `RuntimeEntityCreation` marker while scanning the layout and publishes the key on the
  entry (`TrapLayoutEntryMsg.CreationKey`, ProtoMember 5); `TrapLayoutApplication.Materialize`
  stamps the guest's materialized copy with it, so the runtime-entity snapshot's record for
  that trap binds by key and the death hook reports the same key.
- **Generated entities.** They carry no shared creation key and need none: a world-generation
  entity is deterministic on both sides, and the only record that ever described one is the
  entity's own `Start` report, whose key is stamped on that same copy by the exact-key pass.
  A runtime creation coinciding with a generated same-prefab entity inside 1 m is therefore
  now a real second entity on both sides instead of a one-sided missing entity — the
  tie-break is "identity wins; the markerless copy is never a bind target".
- **Wire.** Two additive ProtoMembers + `ProtocolVersion.Current` 17 → 18 (behavioral wire
  extension; mixed-version sessions are refused by the handshake, per decision #137).

## Acceptance matrix coverage

| # | Scenario | Covered by |
|---|---|---|
| 1 | A markerless same-prefab decoy at the recorded position | `RuntimeEntityMatchTests.FindIndex_MarkerlessSamePrefabDecoyAtTheRecordedPosition_NeverAbsorbsTheRecord` (red→green) |
| 2 | A markerless decoy beside the record's own copy | `FindIndex_MarkerlessDecoyBesideTheOwnCopy_BindsTheOwnCopy` |
| 3 | Markerless copies at the old radius/boundary/prefab variants | `FindIndex_MarkerlessCopy_NeverBinds_WhateverItsPrefabOrPosition` |
| 4 | A drifted copy far outside the old radius | `FindIndex_BindsADistantMarkedCopy_NoRadiusApplies`, `FindIndex_BindsADriftedCopyByKey` |
| 5 | Same-cell siblings never share a copy | `FindIndex_TwoCreationsOfTheSamePrefabInOneCell_NeverShareACopy`, `FindIndex_ThreeSpawnedTurretsCloseTogether_EachBindsItsOwnCopy` |
| 6 | The enemy backfill carries the creation key to the wire | `EnemyStateRoundtripTests.EnemySpawnEntry_CarriesTheCreationKey_SoTheBackfillCopyKeepsTheIdentity`, `NetPacketTests.EnemySpawnEntry_CreationKey_RoundTrips` |
| 7 | An unattributable runtime-spawn fact stays keyless | `EnemyStateRoundtripTests.EnemySpawnEntry_NoCreationKey_StaysNull` |
| 8 | The trap-layout entry carries the key; a generated trap stays null | `NetPacketTests.TrapLayoutEntry_CreationKey_RoundTrips` |
| 9 | The adapter stamps the backfill copies | adapter shell — needs the live Unity world: `EnemySyncCoordinator.RuntimeSpawns.CreateRuntimeSpawn` (marker read at capture, stamp at materialization) and `TrapLayoutApplication.Materialize` are code-reviewed, with the key round-trip pinned by the wire tests. Not dual-client verified. |
| 10 | The live relay still reaches the reporter | unchanged: `RuntimeEntityChannel.SendEntitySpawned`'s host branch broadcasts to every member including the source; no path touched by this change |

## Verification

- **Red (recorded in-session, real runtime failures).** The two absorption tests were run
  against the pre-fix `RuntimeEntityMatch.FindIndex` (positional fallback still present) and
  failed:
  - `FindIndex_MarkerlessSamePrefabDecoyAtTheRecordedPosition_NeverAbsorbsTheRecord` —
    `Assert.Equal() Failure: Values differ Expected: -1 Actual: 0`
  - `FindIndex_MarkerlessBackfillCopyWithoutAKey_IsNotABindTarget` — same
- **Green.** Focused run green across the touched families; full suite 2 682 + 32 normative
  gates green; `dotnet format` clean.
- **Evidence rot.** The deletions shifted 15 evidence anchors (12 in
  `sync-coverage-evidence.json`, 9 inline in the matrix, with overlap) and the new identity
  fields added 3 anchors; every quote was mechanically re-verified against the working tree
  (`SyncCoverageGateTests` 32/32 green) and the matrix/JSON counts were corrected to 793.
- **Deployment (verified, after the second review round).** `tools/deploy.ps1 -GameDir "<game-dir>"`
  deployed the latest build; every deployed CUO assembly hash equals its build output
  (`Runtime` `2CCFF1CF…`, `GameAdapter` `C674D350…`, `GameState` `BAD44B02…`,
  `Protocol` `62AAB03D…`, `Abstractions` `07363D6D…`, `CasualtiesUnknownOnline` `563E6226…`),
  and the deployed Runtime/GameAdapter binaries contain the new symbols
  (`MatchRuntimeSpawnsByIdentity`, `MatchRuntimeSpawnsByCreationKey`, `TryReadOnEntityOf`,
  `TrapLayoutMaterialization`).

## Independent adversarial review (fresh context) — findings and fixes

The first independent review rejected the landing; all findings were fixed and re-verified:

1. **BLOCKER — the trap-layout creation key never reached the wire.** `TrapEntityScan`
   filled `TrapLayoutEntryMsg.CreationKey`, but `TrapLayoutScanner` called
   `IWorldControl.ReportTrapLayout(kind, x, y, prefabName)` (no key) and
   `TrapLayoutRegistry.Report` rebuilt the entry without it, so the field was always null in
   production and the deleted positional fallback left a late joiner's runtime-created trap
   unbound (duplicate entity). **Fix:** the key is threaded through
   `IWorldControl.ReportTrapLayout` → `WorldService` → `WorldChannelRelay` →
   `EntityEventChannel` → `TrapLayoutRegistry.Report` (optional parameter, so the four
   existing call sites and tests keep compiling) and `TrapLayoutScanner` passes it.
   **Coverage:** `TrapLayoutSimulationTests.WorldEntrySnapshot_CarriesTheCreationKey_OfARuntimeCreatedTrap`
   drives the production chain end-to-end (host report → wire → guest receive).
2. **MAJOR — the scan missed child-mounted trap scripts.** `GetComponent<BuildingEntity>()`
   is same-GameObject only; `GeyserScript` hangs on a CHILD object
   (`GeyserScript.cs:13` reads `transform.parent`), so a runtime-created geyser's layout
   entry silently lost its key. **Fix:** `GetComponentInParent<BuildingEntity>()`;
   `TrapLayoutScanner` now logs how many scanned entries carried a key, so the count is
   observable.
3. **MAJOR — the enemy snapshot's key-first pairing was missing.** `MaterializeRuntimeSpawns`
   still paired by position/prefab within 0.5 units; a fact whose animal had already drifted
   (or a snapshot landing before the first 20 Hz batch) would materialize a SECOND copy with
   the SAME creation key. **Fix:** new pure `EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByCreationKey`
   runs FIRST (distance-free, keyed facts bind keyed copies), the keyless remainder keeps the
   historical positional pass, and a keyed fact is never positionally absorbed.
   **Coverage:** four new `EnemyRuntimeSpawnArbitrationTests` cases (key bind regardless of
   distance, keyed fact vs keyless copy, keyless fact left to the positional pass, one keyed
   copy serves one fact).
4. **MAJOR (recorded, not a defect) — `EnemyEntity.CreationKey` is host-only.** A guest's
   `EnemyEntity` is rebuilt from the 20 Hz wire stream / checkpoint restore and does not carry
   the key; that is deliberate and harmless: the guest's creation identity lives on the
   `RuntimeEntityCreation` marker (stamped at creation or backfill) and the guest's
   `RuntimeSpawns` come from the snapshot message itself, which carries the key. Recorded in
   the limitations below and asserted host-side by
   `EnemySyncServiceTests.WorldEntry_RuntimeSpawnFacts_RideTheSnapshot`.
5. **MINOR — one entity, several layout entries.** A turret/mine produces two kinds at the
   same position (`TrapEntityScan`), so `ToSpawn` could materialize the same prefab twice —
   now with the same creation key. **Fix:** the pure `TrapLayoutMaterialization.Deduplicate`
   collapses entries by (prefab, position) with the KEYED entry winning, and `Apply`
   materializes the result and logs the collapsed count.
6. **MINOR — missing null-key wire coverage** for `EnemySpawnEntryMsg`; the domain-level
   `EnemySpawnEntry_NoCreationKey_StaysNull` plus the protobuf null-message precedent cover
   it, and `TrapLayoutEntry_CreationKey_RoundTrips` covers the null side on the trap wire.

### Second independent review round — findings and fixes

The re-review of the fixes rejected the cycle again; all findings were fixed and re-verified:

1. **BLOCKER — candidate-index space in `MatchRuntimeSpawnsByIdentity`.** The orchestration
   filtered the candidate list between the keyed pass and the positional pass, but
   `MatchRuntimeSpawns` reports positions inside the FILTERED list; the caller indexes its own
   list, so a keyed candidate could be bound to a keyless fact, leaving a copy bound to two
   enemy ids and one fact never materialized. **Fix:** the positional result is mapped back
   through `remainingCandidates[filteredIndex].CandidateIndex`. **Coverage:** four new
   `MatchRuntimeSpawnsByIdentity` cases (mixed keyed/keyless with real index assertions, keyed
   fact with no keyed copy, empty).
2. **MAJOR — the deployment claim was false.** The ticket/selfcheck claimed deployed-hash
   verification before any deployment had happened (the entity machine still ran the
   pre-fix build). **Fix:** the deployment is performed in this cycle and the hashes are
   compared afterwards; the claim was removed until then. This is exactly the
   `AGENTS.local.md` rule (a runtime change is not delivered until the entity machine runs
   the new DLLs).
3. **MINOR — `GetComponentInParent<BuildingEntity>()` could cross an entity boundary.** A
   trap component nested under an unrelated markerless child of a runtime-created entity
   would inherit that entity's key. **Fix:** `RuntimeEntityCreation.TryReadOnEntityOf` walks
   the transform chain looking for the marker itself, so a component resolves to the entity
   whose GameObject actually carries the marker.
4. **MINOR — stale evidence prose/counters.** The E3 matrix cell still said the enemy
   late-join copy carries no marker; the audit counts in the README (790/285) and the matrix
   rot-guard sentence (750) were stale. **Fix:** corrected to 793/288 and to the landed
   behaviour.

## Known limitations (recorded, not hidden)

- A runtime-spawn fact the host cannot attribute to a creation record (an animal that never
  rode the entity-creation channel — e.g. a host-mod spawn that bypasses it) travels with a
  null key; its backfill copy is markerless and nothing binds it. That is correct: no record
  exists for it, so there is nothing to bind. The keyless remainder still uses the positional
  pass on the enemy side only, and a keyed candidate is never consumed by a keyless fact.
- `EnemyEntity.CreationKey` is a HOST-side field: the 20 Hz wire stream and checkpoint
  restore rebuild the guest's `EnemyEntity` without it (the key is not a presentation field
  and the guest never publishes an enemy snapshot). The guest's identity is the marker.
- Trap-layout alignment still claims a local entity by kind + 3 m
  (`TrapLayoutAlign`), so a host entry whose creation key belongs to a different local copy
  can be "kept" without being stamped; the 60 s runtime-entity snapshot then materializes
  the record's own copy. That is a duplicate until the next layer/realignment, and it is the
  deliberate trade for deleting the absorption-prone positional bind. (The alternative —
  key-aware alignment — is a separate change in the trap-layout domain.)
- The adapter shell (the marker read at capture, the stamp at materialization, the trap
  scanner's key read, the materialization dedup call site) is not exercisable in the test
  host — it needs the live Unity world. Those branches rest on code review plus the unified
  dual-client pass; the wire identity, the pure match judgment, the key-first arbitration
  and the dedup judgment are covered by tests.
