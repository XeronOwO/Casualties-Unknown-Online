# Runtime-created BuildingEntity spawns have no backfill or re-report

- Status: In progress (partial landing; round-3 findings open — see the section below)
- Priority: Medium-High
- Category: Network / sync coverage / world entities
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row E3)
- Related: `todo/enemy-snapshot-and-attack-recovery.md` (the enemy/animal half), `todo/sync-cadence-review.md`, `review/guest-block-mutation-re-report.md` (the W1 sibling; this landing generalized its fallback cadence)

## Problem (evidence)

`EntitySpawned` (NetMsg 68) was the only runtime entity-creation channel and it was
one-shot in both directions:

- Report: `src/CasualtiesUnknownOnline.GameAdapter/World/EntitySpawnSync.cs`
  (`_world.SendEntitySpawned(new EntitySpawnedMsg`); host relay in the same file.
- Wire: the host broadcast / guest report in the entity-creation channel.
- No periodic re-report and no creation snapshot: the world-entry fan-out had no
  entity-creation member and the kernel `WorldEntityState` holds only
  consumption / health / opened / trap-state facts.
- The 60 s cycle re-sent only block state, block damage, the kernel checkpoint
  and keypad codes; the keypad/geyser re-sends are position-keyed onto
  already-existing local entities, so they heal a carried value but cannot
  materialize a missing entity.
- A swallowed report therefore left a permanently one-sided entity until the
  next layer regeneration / reconnect.

## Goal

A runtime-created `BuildingEntity` (keypad, crate, mod-spawned structure, crystal,
etc.) materializes exactly once on every peer, survives a swallowed report and a
late join / reconnect, and never duplicates.

## Design direction (decide at implementation)

1. **Creation snapshot in the world-entry group** — the host keeps a table of
   runtime-created entities (id + prefab + position + creation-time payload) and
   sends it absolutely in the ordered world-entry group, plus a periodic
   re-report (piggyback on the existing 60 s cycle).
2. **Re-report on demand** — a peer that is missing an entity asks the host /
   creator for the creation record.
3. **Accepted loss** — rejected: a missing airdrop crate or mod structure is a
   hard gameplay divergence.

Either way the apply must stay idempotent (`EntitySpawnSync.FindExisting`'s ~1 m
dedup already provides the primitive) and must not resurrect an entity the
world has since destroyed.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest creates a runtime entity; its report is dropped | Host creates its copy; third-party guests see it |
| 2 | Host creates a runtime entity; the relay is dropped | Guest converges without reconnect |
| 3 | Late joiner enters the world | All runtime entities present exactly once |
| 4 | Reconnect while in world | Same; no duplicates |
| 5 | Creation-time payload (keypad code / geyser liquid type / crystal tint) | Preserved on every path |
| 6 | Entity destroyed before the re-report | Not resurrected |
| 7 | Layer regeneration | Runtime entities from the previous layer are not re-materialized |
| 8 | Duplicate delivery of the same creation record | Exactly one local entity (existing ~1 m dedup) |

## Non-goals

- Enemy / animal runtime spawns (owned by `todo/enemy-snapshot-and-attack-recovery.md`).
- Item-domain spawns (already covered by the item kernel + keyframe).
- Replacing `EntitySpawned` for the live path; the ticket is about recovery.

## Landed (2026-09-09)

**Mechanism (design option 1, adapted).** The recovery unit is the *creation
record*, keyed by prefab id + the floored cell of its creation position
(`RuntimeEntityKey`), not a deviation table:

- **Host authority table.** `RuntimeEntityRegistry` records every accepted
  NON-ANIMAL creation — the host's own local creations and the guest reports it
  relays — as the exact `EntitySpawnedMsg` the live channel broadcasts
  (creation-time payload included). `RuntimeEntityChannel.SendEntitySpawned`'s
  host branch records BEFORE the broadcast, so the enriched relay (a generated
  keypad code) is what the table stores. The table is bounded (65 536 keys, new
  keys refused at the cap with a once-per-episode warning) and sent absolutely
  as the new `NetMsg.RuntimeEntitySnapshot` (134) in the ordered world-entry
  group (`WorldEntryFanout.Send`) and by the existing 60 s host cycle
  (`WorldEventSync.Update`, alongside the block/checkpoint resends).
- **Guest pending re-report.** `RuntimeEntityChannel.SendEntitySpawned`'s guest
  branch records the report in `PendingEntityReportTable` BEFORE the send (for
  animals too — a swallowed guest → host animal report has no other in-session
  recovery). The 60 s fallback (`PendingReportFallback` +
  `WorldReportFallbackPump` — the W1 cadence class generalized to take the
  pending count and the resend action) re-reports every unacknowledged creation
  as a plain `EntitySpawned` message until the host answers. The answer is
  either the host's relay echo (the broadcast includes the reporter) or the
  absolute snapshot; both drop the entry. An unanswered entry is never dropped
  for age — a stalled creation logs once after 10 fallback windows and keeps
  retrying.
- **Apply is additive and idempotent.** The snapshot handler replays each entry
  through the live creation path: missing entities materialize, existing ones
  bind by their stamped creation key first and by the 1 m same-prefab match
  otherwise, so a repeated record never duplicates and a local creation the host
  has not answered yet is never destroyed by a snapshot that predates it (unlike
  the trap layout's destructive alignment).
- **Creation identity survives drift.** Every runtime-created entity carries a
  `RuntimeEntityCreation` marker (prefab id + creation cell) stamped on the
  local report path and on every remotely created copy. A BuildingEntity's
  Rigidbody2D becomes Dynamic while its chunk is visible
  (BuildingEntity.cs:54), so a copy can fall or be pushed across cells: the
  death funnel reads the marker and reports the CREATION key, and the apply path
  prefers a candidate with the same marker before the radius match. A record
  therefore binds to its own copy and is dropped when that copy dies, wherever
  it drifted.
- **Animals are split by direction.** The live creation message is unchanged,
  and `EntitySpawnedMsg.IsAnimal` (ProtoMember 9) tells the recovery which
  direction owns it: the guest pending table records animals (in-session
  guest → host recovery), the host accepted-creation table does not (the enemy
  domain's `EnemySnapshot.RuntimeSpawns` owns the late-join/backfill copy and
  materializes at the animal's CURRENT position — a host record would make a
  late joiner create a second copy at the creation position).
- **One bad record cannot kill a batch.** `Utils.Create` THROWS on a missing
  prefab (the enemy materializer already documented the same failure); the
  remote-create path contains it per entry, so a single un-creatable record
  (mod prefab mismatch) no longer discards the rest of an absolute snapshot
  batch on every cycle. Mod-registered custom templates still materialize
  normally (`UtilsCreateCustomPrefabPatch` handles those ids; a
  `Resources.Load` pre-check would have rejected them, which the round-2 review
  caught before delivery).
- **Not resurrected.** Every `BuildingEntity` death funnels through
  `BuildingEntityUpdatePatch`; it notifies the world domain, which drops the
  host's accepted record or the guest's pending report for that creation
  (`ReportRuntimeEntityDestroyed`). The layer/session boundaries clear both
  tables (`WorldService.ResetDamagedBlocks` → host table;
  `WorldParamsService.Apply` → guest pending table;
  `WorldService.ResetSessionState` → both). The world-entry completion marker
  deliberately does NOT clear the guest table — a reconnect-while-in-world keeps
  its local creations, exactly like W1.

**Structure.** The creation channel outgrew `EntityEventChannel` (567 lines, two
lines of headroom before the 600-line gate), so the runtime-creation half became
its own `RuntimeEntityChannel` and `EntityEventChannel` returned to the
entity-EVENT channel (311 lines). `WorldService` also crossed the gate (610
lines) while adding the new forwards, so the host start-gate lifecycle moved to
`WorldStartGate` with its own state (the `IWorldControl` surface is unchanged).
The 1 m dedup judgment was extracted from the adapter's Unity scan into the pure
`RuntimeEntityMatch`, so the radius/exclusion contract is unit-testable.

**Independent adversarial review (fresh context, two rounds).**

- Round 1 found: (BLOCKER) the evidence anchors were shifted by the preceding
  `dotnet format` run — re-anchored and re-verified (770 entries); (MAJOR) the
  animal family was recorded in the recovery tables although the death hook
  reported the current position, so a moved animal's record leaked and the 60 s
  re-broadcast resurrected it, and a late joiner materialized a duplicate next
  to the enemy snapshot's copy; (MAJOR) an un-creatable prefab threw inside the
  snapshot apply loop and discarded every later entry.
- Round 2 (re-review of the fixes) found: (MAJOR) the first fix used a
  `Resources.Load` pre-check, which rejects every MOD-registered custom building
  by construction (custom templates live in the building-content provider and
  are materialized by `UtilsCreateCustomPrefabPatch`, not in Resources) — the
  guard was replaced with a per-entry `try/catch` around `Utils.Create`;
  (MAJOR) the death hook still keyed by the current position — the stamped
  `RuntimeEntityCreation` marker now supplies the creation key, and the apply
  path prefers the same-marker candidate; (MAJOR) the animal exclusion starved
  the guest → host direction — the guest pending table now records animals
  while the host table stays excluded; (MINOR) the geyser deferred report lost
  the flag and the new wire field had no round-trip test — both fixed.
- Both rounds were run by fresh-context reviewers with their own build/test
  runs; every finding is fixed and re-verified (round 3 is the same gate suite
  plus the new regression tests).

**Known limitations (recorded, not hidden).**

- A destroyed entity whose death signal is itself lost can be re-materialized
  until the host's own copy dies: the destruction paths are the E1
  damage/open/support-loss relays, whose own re-report gap is a separate ticket.
- The adapter's materialization shell (the Unity create + `FindExisting` scan,
  the death-hook wiring in `BuildingEntityUpdatePatch`, the `WorldParamsService`
  apply call site, the `RuntimeEntityCreation` stamping) is not exercisable in
  the test host — it needs the live Unity world. Those branches rest on code
  review plus the unified dual-client acceptance pass; the pure match judgment
  and every Runtime table/cadence path are covered by tests.

**Evidence.** `docs/evidence/sync-coverage-matrix.md` row E3 → `OK`
(47 OK / 8 event-only / 0 fallback-only / 9 transient), the new
`RuntimeEntitySnapshot` vocabulary entry indexed to E3, inline anchors
re-anchored and extended (778 entries: 505 collector + 273 inline), the audit
ticket and backlog index updated, and the gap list records E3 as closed.

**Verification.**

- Red (recorded in-session): `GuestEntityReportRecoveryTests.SwallowedGuestCreationReport_IsReReportedOnTheFallbackCycleAndConverges`
  failed on the unfixed code with
  `Assert.Single() Failure: The collection was empty` — the 60 s fallback did
  not re-report the swallowed creation. To re-observe: comment out the pending
  recording (`RecordPendingEntityReport(msg);`) or the fallback pump call in
  `RuntimeEntityChannel.SendEntitySpawned`.
- Green: `GuestEntityReportRecoveryTests` (11) + `RuntimeEntityRegistryTests`
  (5) + `PendingEntityReportTableTests` (8) + `RuntimeEntityMatchTests` (7) +
  the direction-classification row (1) = 32 new cases; full suite 2 644 + 32
  gates (2 676) pass; `dotnet format` exit 0 (source-only clean; the
  `--verify-no-changes` variant additionally reports the build-generated
  `MyPluginInfo.cs` under `obj/`, which is not source); build 0 warnings /
  0 errors.
- Deployment: `tools/deploy.ps1 -GameDir "<game-dir>"` deployed the latest
  build and every deployed CUO assembly hash equals its build output —
  `Runtime` `5E88AC11…`, `GameAdapter` `654DC7F5…`, `GameState` `A3D620EC…`,
  `Protocol` `941DC9AB…`, `Abstractions` `31B509AC…`, `CasualtiesUnknownOnline`
  `992774FE…`.

### Acceptance matrix coverage

| # | Covered by |
|---|---|
| 1 | `SwallowedGuestCreationReport_IsReReportedOnTheFallbackCycleAndConverges` (link down → the fallback re-reports → the host records + relays → the third member receives it) |
| 2 | `SwallowedHostRelay_HostSnapshotReBroadcastConvergesTheMember` (the snapshot APPLY heals the lost relay without a reconnect; the 60 s trigger itself is adapter-side and code-reviewed only) |
| 3 | `LateJoiner_ReceivesTheAcceptedCreationTableOnWorldEntry` (the world-entry fan-out carries both accepted creations); the animal half rides `EnemySnapshot.RuntimeSpawns` (N1) |
| 4 | `HostSnapshot_AcknowledgesAPendingReportWhoseEchoWasSwallowed` + `WorldSnapshotComplete_DoesNotDropUnansweredCreations` (the reconnect marker is not a boundary; the snapshot is the answer; a session teardown does clear) |
| 5 | the snapshot/report entries are the exact `EntitySpawnedMsg` (creation-time payload included); `RuntimeEntityRegistryTests.Report_UpsertsByCreationKey_AndKeepsTheLatestPayload` pins the payload replacement; `NetPacketTests.EntitySpawned_CrystalEnemyTint_RoundTrips` pins the new flag's wire round-trip |
| 6 | `EntityDestroyedBeforeTheAnswer_HostDropsTheAcceptedRecord` + `EntityDestroyedBeforeTheAnswer_GuestDropsThePendingReport` (no re-report, no re-broadcast); the adapter reports the stamped creation key, so a drifted entity still drops its record (code-reviewed — needs the live world) |
| 7 | `LayerReset_DropsThePreviousWorldsPendingCreations` + `RuntimeEntityRegistryTests.Reset_ClearsEveryAcceptedCreation`; the adapter wiring (`WorldParamsService.Apply`, `ResetDamagedBlocks`) is code-reviewed only |
| 8 | `DuplicateCreationReport_KeepsOneAcceptedRecord` (the host table upserts to one record) + `RuntimeEntityMatchTests` (the 1 m same-prefab bind, the same-marker preference after drift, the tutorial-prop exclusion and the three-turret regression); the adapter's Unity create path is dual-client acceptance |

## Round 3 findings (OPEN — fix next session)

The third independent review (fresh context) accepted the round-2 fixes but found four
MAJOR defects plus one MINOR in the new mechanism. They are real and unfixed; the ticket
therefore stays OPEN and the matrix row E3 keeps its `Event-only gap` verdict.

1. **MAJOR — same-cell same-prefab second creation is swallowed.** The marker match in
   `RuntimeEntityMatch.FindIndex` ignores distance, and the recovery key is only
   `prefab id + floored cell`: two identical prefabs 1.0-1.4 m apart (same cell) bind the
   second report to the first copy, so the second entity exists nowhere and the key
   upserts overwrite each other. Fix direction: a creation-instance token on the wire
   (creator SteamId + monotonic sequence) indexed by both `RuntimeEntityKey` and the
   `RuntimeEntityCreation` marker; the marker match then needs no radius.
2. **MAJOR — geyser deferred reports key off a different transform than the marker.**
   `OnEntityInstantiated` stamps the root `entity.transform.position`, while
   `FlushReports` reports `geyser.transform.position` (a CHILD) and locates it by the 3 m
   `FindTrap` first hit. A cross-cell offset makes the record key differ from the death
   key, so the host record is never dropped and the 60 s re-broadcast can resurrect a
   destroyed geyser. Fix direction: report the queued creation position (and locate the
   entity by the queued id/position, not the 3 m first hit).
3. **MAJOR — a host-side create failure returns before the relay.** When the host lacks
   the prefab/template, `OnRemoteEntitySpawned` logs and returns before
   `SendEntitySpawned(relay)` and before the accepted-creation record, so a third-party
   guest that DOES have the prefab never receives the creation (accept-first violation).
   Fix direction: relay the original message and record the acceptance even when the
   host cannot materialize locally; contain the custom-template hook failure inside
   `UtilsCreateCustomPrefabPatch` (destroy the half-built instance, rethrow) so a
   callback failure cannot leave an unmarked orphan.
4. **MAJOR — MOD animals have no late-join recovery under the new split.**
   `EnemySyncCoordinator.CreateRuntimeSpawn` still pre-checks
   `Resources.Load(spawn.PrefabId) == null` and skips MOD-registered custom animal
   templates (they are not in Resources; `UtilsCreateCustomPrefabPatch` materializes
   them). With animals now excluded from the host accepted-creation table, a mod animal
   is permanently missing on a late joiner. Fix direction: replace that pre-check with
   the same per-entry `try/catch(Utils.Create)` used by `EntitySpawnSync` (the N1 ticket
   owns the rest of the enemy path).
5. **MINOR — the guest animal pending entry never gets a snapshot acknowledgement.** The
   host snapshot excludes animals, so an entry whose echo was lost is cleared only by the
   entity's death; it re-reports every 60 s and logs the stall warning meanwhile. Fix
   direction: carry an accepted-animal key list in `RuntimeEntitySnapshotMsg` for
   acknowledgement only (never materialize from it).

Until these are fixed and re-reviewed, the mechanism must not be presented as closing E3.
