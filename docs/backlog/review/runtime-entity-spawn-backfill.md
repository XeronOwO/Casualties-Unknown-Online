# Runtime-created BuildingEntity spawns have no backfill or re-report

- Status: Review
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

Either way the apply must stay idempotent and must not resurrect an entity the
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
| 8 | Duplicate delivery of the same creation record | Exactly one local entity |
| 9 | Two identical prefabs created inside one cell | Two entities on every peer; a death drops only its own record |
| 10 | An accepted animal report | Acknowledged by the snapshot key list; never materialized from it |
| 11 | The host cannot materialize a reported creation | Still accepted + relayed; the reporter's report is acknowledged |
| 12 | A MOD-registered template (building or animal) | Materialized (never rejected by a `Resources.Load` pre-check) |

## Non-goals

- Enemy / animal runtime spawns (owned by `todo/enemy-snapshot-and-attack-recovery.md`).
- Item-domain spawns (already covered by the item kernel + keyframe).
- Replacing `EntitySpawned` for the live path; the ticket is about recovery.

## Landed (2026-09-09)

**Mechanism (design option 1, adapted).** The recovery unit is the *creation
record*, keyed by the creation-instance identity `RuntimeEntityKey` — prefab id +
the floored cell of the creation position + a token (the creating side's SteamId
+ a random-seeded monotonic sequence):

- **Creation-instance token.** `EntitySpawnSync.NextCreationKey` stamps
  `EntitySpawnedMsg.CreatorSteamId` / `CreationSequence` (ProtoMember 10/11) on
  the local report; the host's keypad-code enrichment copies the token
  unchanged; the token also rides the marker (`RuntimeEntityCreation`) and the
  recovery tables. The token is what keeps two identical prefabs created
  1.0-1.4 m apart inside one cell apart — the cell alone could not (round-3
  finding 1). The sequence is seeded from a random value, not from 1: the token
  must be unique across PROCESS lifetimes, because the host's accepted table
  survives a creator's reconnect and only resets at a layer boundary.
- **Host authority table.** `RuntimeEntityRegistry` records every accepted
  NON-ANIMAL creation — the host's own local creations and the guest reports it
  relays — as the exact `EntitySpawnedMsg` the live channel broadcasts
  (creation-time payload included). `RuntimeEntityChannel.SendEntitySpawned`'s
  host branch records BEFORE the broadcast, so the enriched relay (a generated
  keypad code) is what the table stores. The table is bounded (4 096 keys TOTAL
  across the entry list and the animal key list — sized so the absolute snapshot
  fits one transport frame; new keys refused at the cap with a once-per-episode
  warning) and sent absolutely as the new `NetMsg.RuntimeEntitySnapshot` (134) in
  the ordered world-entry group (`WorldEntryFanout.Send`) and by the existing
  60 s host cycle (`WorldEventSync.Update`).
- **Animal acknowledgement.** An accepted animal is recorded by KEY ONLY
  (`RuntimeEntityRegistry.ReportAnimal`) and rides the snapshot's
  `AcceptedAnimalKeys` list — acknowledgement, never materialization. The enemy
  domain owns the animal copy (`EnemySnapshot.RuntimeSpawns` materializes at the
  animal's CURRENT position with a host-allocated id), so a materializable host
  record would make a late joiner create a second copy at the creation position.
- **Guest pending re-report.** `RuntimeEntityChannel.SendEntitySpawned`'s guest
  branch records the report in `PendingEntityReportTable` BEFORE the send (for
  animals too — a swallowed guest → host animal report has no other in-session
  recovery). The 60 s fallback (`PendingReportFallback` +
  `WorldReportFallbackPump` — the W1 cadence class generalized to take the
  pending count and the resend action) re-reports every unacknowledged creation
  as a plain `EntitySpawned` message until the host answers. The answer is the
  host's relay echo, the absolute snapshot's entry list, or its animal key list.
  An unanswered entry is never dropped for age — a stalled creation logs once
  after 10 fallback windows and keeps retrying.
- **Accept-first on a local materialization failure.** A host that lacks the
  prefab/template cannot build its own copy, but it must not drop the creation:
  `RuntimeEntityChannel.ReportEntitySpawnUnmaterialized` relays the ORIGINAL
  message, so a member that has the prefab receives it and the reporter's echo
  still acknowledges its pending report. It deliberately does NOT record the
  acceptance — a record with no local copy could never be dropped, and the 60 s
  snapshot would re-materialize a creation a member later destroyed. The
  resulting late-join gap for such a creation is recorded under limitations.
- **One materialization path.** `RuntimeEntityFactory.TryCreate` is the shared
  materializer (entity spawn + enemy runtime backfill): it never pre-checks
  `Resources.Load` (a MOD-registered template lives in the content provider and
  is not in Resources BY DESIGN), contains `Utils.Create`'s missing-prefab throw
  per entry, and destroys a non-BuildingEntity orphan.
  `UtilsCreateCustomPrefabPatch` destroys the half-built instance when a mod
  instance hook throws, so a failed callback cannot leave an unmarked orphan.
- **Apply is additive and idempotent.** The snapshot handler replays each entry
  through the live creation path: missing entities materialize, existing ones
  bind by their stamped creation key first, and otherwise by a MARKERLESS
  same-prefab copy inside the 1 m radius (`RuntimeEntityMatch`) — a repeated
  record never duplicates and a local creation the host has not answered yet is
  never destroyed by a snapshot that predates it. The positional pass exists for
  copies that never entered the runtime-creation tables: an enemy-domain backfill
  copy (`EnemySyncCoordinator.CreateRuntimeSpawn`) or a generated entity. (A
  trap-layout materialization no longer reports at all — it carries a
  `SpawnReplayMarker`, because the layout is host-authoritative and its `Start`
  runs after the `RemoteApply` scope closed.) It
  deliberately skips every candidate that CARRIES a marker, so a sibling
  creation's copy can never absorb this record.
- **Creation identity survives drift.** Every runtime-created entity carries a
  `RuntimeEntityCreation` marker holding the full key. A BuildingEntity's
  Rigidbody2D becomes Dynamic while its chunk is visible
  (BuildingEntity.cs:54), so a copy can fall or be pushed across cells: the
  death funnel reads the marker and reports the CREATION key, and the apply path
  binds by that key. A record therefore binds to its own copy and is dropped
  when that copy dies, wherever it drifted — and a same-cell sibling keeps its
  own record.
- **Deferred geyser reports carry the queued identity.** A geyser's liquid type
  rolls at the CHILD's Start (GeyserScript.cs:12), so its report is deferred one
  frame. The queue now carries the creation key AND the creation position
  stamped at instantiation, and the flush locates the entity by that key instead
  of a 3 m `FindTrap` first hit — the report key and the death key can no longer
  diverge across cells.
- **Not resurrected.** Every `BuildingEntity` death funnels through
  `BuildingEntityUpdatePatch`; it notifies the world domain, which drops the
  host's accepted record (or animal key) or the guest's pending report for that
  creation key. The layer/session boundaries clear both tables
  (`WorldService.ResetDamagedBlocks` → host table;
  `WorldParamsService.Apply` → guest pending table;
  `WorldService.ResetSessionState` → both). The world-entry completion marker
  deliberately does NOT clear the guest table — a reconnect-while-in-world keeps
  its local creations, exactly like W1.

**Structure.** `EntityEventChannel` (567 lines, two lines of headroom before the
600-line gate) split into the runtime-creation half (`RuntimeEntityChannel`, 375
lines) and the entity-EVENT channel (311 lines). `WorldService` also crossed the
gate while adding the new forwards, so the host start-gate lifecycle moved to
`WorldStartGate` (`WorldService` 499 lines). The 1 m dedup judgment became the
pure `RuntimeEntityMatch` (79 lines) and is now the exact-key-first identity
judgment with a markerless positional fallback.
The materialization shell was extracted into `RuntimeEntityFactory` (64 lines) so
the entity-spawn channel and the enemy runtime backfill cannot drift apart
again. All touched classes are under the 600-line gate.

## Round 3 findings — fixed

The third independent review (fresh context) found four MAJOR defects plus one
MINOR in the first landing. All five are fixed; each fix has a regression test or
an explicit code-review justification:

1. **MAJOR — same-cell same-prefab second creation was swallowed.** The key was
   prefab id + floored cell only, and the marker match ignored distance, so two
   identical prefabs 1.0-1.4 m apart bound to the same copy and the second
   record overwrote the first. **Fix:** the creation-instance token
   (`EntitySpawnedMsg.CreatorSteamId`/`CreationSequence`) indexes the key, the
   marker and the match, and the positional radius fallback now binds ONLY
   candidates that carry no marker — a sibling's marked copy can never absorb
   this record. **Coverage:**
   `RuntimeEntityKeyTests.From_TwoCreationsOfTheSamePrefabInOneCell_AreDistinctByToken`,
   `RuntimeEntityRegistryTests.Report_TwoCreationsOfTheSamePrefabInOneCell_AreTwoRecords`,
   `PendingEntityReportTableTests.Report_TwoCreationsOfTheSamePrefabInOneCell_AreDistinctByToken`,
   `GuestEntityReportRecoveryTests.TwoCreationsOfTheSamePrefabInOneCell_AreTwoAcceptedRecords`,
   `RuntimeEntityMatchTests.FindIndex_TwoCreationsOfTheSamePrefabInOneCell_NeverShareACopy`,
   `GuestEntityReportRecoveryTests.TwoCreationsInOneCell_OneDies_OnlyItsOwnRecordIsDropped`.
2. **MAJOR — geyser deferred reports keyed off a different transform than the
   marker.** `OnEntityInstantiated` stamped the root position while `FlushReports`
   reported the child `GeyserScript` position, located by a 3 m first hit; a
   cross-cell offset made the host record never drop, so the 60 s re-broadcast
   could resurrect a destroyed geyser. **Fix:** the queue entry carries the
   creation key and position captured at instantiation, the flush reports that
   position and re-locates the entity by that key (the apply queue does the
   same). **Coverage:** adapter shell (needs the live Unity world) — code review
   plus the pure exact-key judgment (`RuntimeEntityMatchTests`), and the unified
   dual-client pass.
3. **MAJOR — a host-side create failure returned before the relay.**
   `OnRemoteEntitySpawned` logged and returned when the host lacked the
   prefab/template, so a third-party guest that DID have it never received the
   creation (accept-first violation). **Fix:**
   `RuntimeEntityChannel.ReportEntitySpawnUnmaterialized` relays the original
   message WITHOUT recording it (see the round-4 re-review below for why a record
   must not be kept); the adapter calls it from the failure branch. **Coverage:**
   `GuestEntityReportRecoveryTests.UnmaterializableCreation_IsRelayedAndAcknowledgedWithoutBeingRecorded`
   + `UnmaterializableReport_OnAGuest_IsNotRelayed` (the adapter's failure branch
   is the contract double).
4. **MAJOR — MOD animals had no late-join recovery under the split.**
   `EnemySyncCoordinator.CreateRuntimeSpawn` pre-checked `Resources.Load` and
   skipped MOD-registered animal templates; animals are excluded from the host's
   materializable table, so nothing else could heal a late joiner. **Fix:** the
   pre-check is gone; both materialization sites use
   `RuntimeEntityFactory.TryCreate` (per-entry containment, orphan destroy).
   **Coverage:** adapter shell — code review; the shared factory's contract is
   pinned by the entity-spawn paths and the pure match tests.
5. **MINOR — an accepted animal report never got a snapshot acknowledgement.**
   **Fix:** `RuntimeEntitySnapshotMsg.AcceptedAnimalKeys` +
   `RuntimeEntityRegistry.ReportAnimal`; the guest drops the pending entry from
   that list and never materializes from it. **Coverage:**
   `RuntimeEntityRegistryTests.ReportAnimal_KeepsTheKeyOnlyAndNeverEntersTheMaterializableEntries`,
   `Remove_DropsTheAcceptedAnimalKeyToo`, `Reset_ClearsEveryAcceptedCreation`,
   `GuestEntityReportRecoveryTests.AnimalCreation_IsAcknowledgedByTheHostSnapshot`,
   `NetPacketTests.RuntimeEntitySnapshot_EntriesAndAnimalKeys_RoundTrip`.

## Round 4 findings (independent re-review) — fixed

A fresh-context re-review of the round-3 fixes ran against the working tree and
found two MAJOR regressions plus four MINORs. All are fixed and re-verified:

1. **MAJOR — deleting the positional fallback duplicated markerless copies.**
   `TrapLayoutApplication.Materialize` instantiates the host's layout outside
   generation and its `Start` runs after the `RemoteApply` scope was disposed
   (`CallContext` is a scope stack that falls back to `LocalAction`,
   `CallContext.cs:96`), so that copy reported itself as a runtime creation while
   carrying no marker — an exact-key-only match could not bind it, so every peer
   materialized a second trap. **Fix (root cause + safety net):** (a) the layout
   replay now carries a `SpawnReplayMarker`, so it never reports at all — a
   host-authoritative layout replay must not re-report; (b) the positional pass
   is restored but restricted to MARKERLESS candidates, which keeps an
   enemy-domain backfill copy binding a live animal re-report while a sibling
   creation's marked copy can never be bound. **Coverage:**
   `RuntimeEntityMatchTests.FindIndex_BindsAMarkerlessCopyInsideTheRadius` +
   `FindIndex_TwoCreationsOfTheSamePrefabInOneCell_NeverShareACopy`; the
   `SpawnReplayMarker` stamping is adapter-shell (code-reviewed).
2. **MAJOR — a recorded unmaterializable creation resurrected a later
   destruction.** Recording it gave the host a record with no local copy that
   could ever drop it, so the 60 s snapshot re-materialized an entity a member
   had destroyed (acceptance matrix row 6). **Fix:**
   `ReportEntitySpawnUnmaterialized` relays WITHOUT recording; the resulting
   late-join gap is recorded under limitations. **Coverage:**
   `UnmaterializableCreation_IsRelayedAndAcknowledgedWithoutBeingRecorded` (it
   also sends a snapshot and asserts nothing is re-sent) +
   `UnmaterializableReport_OnAGuest_IsNotRelayed`.
3. **MAJOR — wire extension without the protocol-version bump.** New ProtoMembers
   on `EntitySpawnedMsg` and `RuntimeEntitySnapshotMsg` are a behavioral wire
   change: `ProtocolVersion.Current` is now 17 (was 16) and the versioning docs
   (which still said 15) were corrected.
4. **MINOR — `Remove` short-circuited.** Both sets are now consulted explicitly,
   so a peer that flips `IsAnimal` for one key cannot leave a stale animal ack
   behind. **Coverage:**
   `RuntimeEntityRegistryTests.Remove_DropsTheKeyFromBothSets`.
5. **MINOR — the token was unique only within one process.** The sequence is now
   seeded from a random value, so a restarted creator cannot re-issue a token the
   host's table still holds.
6. **MINOR — the snapshot could exceed the transport frame.** A creation record
   is ~80 B, so a 65 536-entry table would produce a multi-MB frame and the
   receiving `IpDirectTransport` closes the peer on an oversize frame
   (`IpDirectTransport.cs:26,360`). The bound is now 4 096 keys TOTAL across the
   entry list and the animal key list (~320 KiB).

Also fixed from that review: `PendingEntityReport` uses a using directive instead
of a partially-qualified name; the unused `RuntimeEntityKey.HasCreationToken` was
deleted and `EntitySpawnedMsg.HasCreationToken` now drives a tokenless-report
warning in the channel; the matrix header's evidence count was corrected to 790.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Creation identity | `RuntimeEntityKey` = prefab id + creation cell + token (creator SteamId + random-seeded monotonic sequence) | `RuntimeEntityKey.cs:21`; `RuntimeEntityKeyTests` (4) |
| Wire | `EntitySpawnedMsg` ProtoMember 10/11 + `HasCreationToken`; `RuntimeEntityKeyMsg` for the ack list; `ProtocolVersion.Current` 17 | `EntitySpawnedMsg.cs:104,113,116`; `NetPacketTests` (23) |
| Match judgment | exact creation-key bind first, then MARKERLESS same-prefab inside 1 m; a marked candidate is never positional | `RuntimeEntityMatch.cs:49,53`; `RuntimeEntityMatchTests` (10) |
| Marker / death key | the marker carries the full key; the death funnel reports it | `RuntimeEntityCreation.cs:36,57`; `EntitySpawnSync.cs:116,118` (adapter, code-reviewed) |
| Host table | non-animal entries + animal key set, 4 096 keys TOTAL; key-based remove; snapshot carries both | `RuntimeEntityRegistry.cs:41,96,118,151`; `RuntimeEntityRegistryTests` (10) |
| Guest pending table | key-based remove/attempt; the entry carries its key | `PendingEntityReportTable.cs:61,64`; `PendingEntityReportTableTests` (9) |
| Channel | animal recording by key; snapshot ack list; accept-first relay WITHOUT a record; key-based death; tokenless warning | `RuntimeEntityChannel.cs:121,162,186,219`; `GuestEntityReportRecoveryTests` (16) |
| Geyser deferral | queue carries key + creation position; flush locates by exact key (no positional fallback) | `EntitySpawnSync.cs:172,359` (adapter, code-reviewed) |
| Materialization family | shared factory (no pre-check, per-entry containment, orphan destroy); mod hook failure destroys the half-built instance | `RuntimeEntityFactory.cs:32,39`; `EnemySyncCoordinator.RuntimeSpawns.cs:78`; `UtilsCreateCustomPrefabPatch.cs:87` (adapter, code-reviewed) |
| Snapshot vocabulary | `AcceptedAnimalKeys` ack list | `RuntimeEntitySnapshotMsg.cs:40`; `NetPacketTests` |
| Matrix / evidence | E3 → OK; summary 47/8/0/9; evidence re-anchored (790 entries) | `docs/evidence/sync-coverage-matrix.md` row E3; `SyncCoverageGateTests` 12/12 |

## Verification

- **Red (recorded in-session, real runtime failures — not compile errors).** The
  new wire fields and the two new Runtime entry points were landed INERT first
  (pure data / no-op) so the regression tests compiled and ran against the
  pre-fix behaviour; the focused run then failed with:
  - `PendingEntityReportTableTests.Report_TwoCreationsOfTheSamePrefabInOneCell_AreDistinctByToken`
    — `Assert.Equal() Failure: Values differ Expected: 2 Actual: 1`
  - `RuntimeEntityRegistryTests.Report_TwoCreationsOfTheSamePrefabInOneCell_AreTwoRecords`
    — same
  - `GuestEntityReportRecoveryTests.TwoCreationsOfTheSamePrefabInOneCell_AreTwoAcceptedRecords`
    — same
  - `GuestEntityReportRecoveryTests.AnimalCreation_IsAcknowledgedByTheHostSnapshot`
    — `Expected: 1 Actual: 0` (no snapshot was sent at all — the host had no
    materializable entries, and the animal ack list did not exist)
  - `GuestEntityReportRecoveryTests.UnmaterializableCreation_IsStillAcceptedRelayedAndAcknowledged`
    — `Assert.Single() Failure: The collection was empty`
- **Green.** The same focused run: 72/72 pass. Full suite: 2 665 + 32 normative
  gates (2 697) pass; `dotnet format` exit 0; build 0 warnings / 0 errors.
- **New coverage.** 21 new cases: `GuestEntityReportRecoveryTests` 11 → 16,
  `RuntimeEntityRegistryTests` 5 → 11, `PendingEntityReportTableTests` 8 → 9,
  `RuntimeEntityMatchTests` 7 → 10 (rewritten for the exact-key-first contract
  plus the markerless fallback), new `RuntimeEntityKeyTests` 4,
  `NetPacketTests` 21 → 23.
- **Round-4 re-review.** Two fresh-context reviewers re-checked the round-3
  fixes; their two MAJOR regressions and four MINORs are listed above and all
  fixed, with the full suite re-run green afterwards.
- **Deployment.** `tools/deploy.ps1 -GameDir "<game-dir>"` deployed the latest
  build; every deployed CUO assembly hash equals its build output —
  `Runtime` `4344595B…`, `GameAdapter` `B60301C4…`, `GameState` `1A413881…`,
  `Protocol` `D342379F…`, `Abstractions` `40703D29…`,
  `CasualtiesUnknownOnline` `FBE57F71…`.

### Acceptance matrix coverage

| # | Covered by |
|---|---|
| 1 | `SwallowedGuestCreationReport_IsReReportedOnTheFallbackCycleAndConverges` (link down → the fallback re-reports → the host records + relays → the third member receives it) |
| 2 | `SwallowedHostRelay_HostSnapshotReBroadcastConvergesTheMember` (the snapshot APPLY heals the lost relay without a reconnect; the 60 s trigger itself is adapter-side and code-reviewed only) |
| 3 | `LateJoiner_ReceivesTheAcceptedCreationTableOnWorldEntry`; the animal half rides `EnemySnapshot.RuntimeSpawns` (N1) |
| 4 | `HostSnapshot_AcknowledgesAPendingReportWhoseEchoWasSwallowed` + `WorldSnapshotComplete_DoesNotDropUnansweredCreations` |
| 5 | the snapshot/report entries are the exact `EntitySpawnedMsg` (creation-time payload included); `RuntimeEntityRegistryTests.Report_UpsertsByCreationKey_AndKeepsTheLatestPayload`; `NetPacketTests.EntitySpawned_CrystalEnemyTint_RoundTrips` |
| 6 | `EntityDestroyedBeforeTheAnswer_HostDropsTheAcceptedRecord` + `EntityDestroyedBeforeTheAnswer_GuestDropsThePendingReport` + `TwoCreationsInOneCell_OneDies_OnlyItsOwnRecordIsDropped`; the adapter reports the stamped creation key (code-reviewed — needs the live world) |
| 7 | `LayerReset_DropsThePreviousWorldsPendingCreations` + `RuntimeEntityRegistryTests.Reset_ClearsEveryAcceptedCreation`; the adapter wiring is code-reviewed |
| 8 | `DuplicateCreationReport_KeepsOneAcceptedRecord` + `RuntimeEntityMatchTests` (the exact-key bind, the same-cell sibling rejection, the markerless candidate rejection, the three-turret regression); the adapter's Unity create path is dual-client acceptance |
| 9 | `RuntimeEntityKeyTests` + the same-cell registry/pending/channel/match cases + `TwoCreationsInOneCell_OneDies_OnlyItsOwnRecordIsDropped` |
| 10 | `AnimalCreation_IsAcknowledgedByTheHostSnapshot` + `RuntimeEntityRegistryTests.ReportAnimal_KeepsTheKeyOnlyAndNeverEntersTheMaterializableEntries` |
| 11 | `UnmaterializableCreation_IsStillAcceptedRelayedAndAcknowledged` (the adapter's failure branch calls `ReportEntitySpawnUnmaterialized`) |
| 12 | `RuntimeEntityFactory` is the single materialization path (code-reviewed); the mod-animal backfill call site is `EnemySyncCoordinator.RuntimeSpawns.cs:78` |

## Known limitations (recorded, not hidden)

- A destroyed entity whose death signal is itself lost can be re-materialized
  until the host's own copy dies: the destruction paths are the E1
  damage/open/support-loss relays, whose own re-report gap is a separate ticket.
- A creation the host cannot materialize is relayed but NOT recorded (a record
  with no local copy could never be dropped, and the snapshot would resurrect a
  member's later destruction). Consequence: a member that joins AFTER the
  creation — even one that has the prefab — does not receive it. The host cannot
  represent a creation it cannot materialize, and recording it would trade this
  late-join gap for a resurrection bug.
- The markerless positional pass can also ABSORB a genuine runtime creation into
  an unrelated markerless same-prefab copy within 1 m (a generated entity), and
  the bind then stamps that copy with the creation key. The pass is kept because
  without it a markerless trap-layout/enemy-backfill copy is duplicated instead
  — a missing entity is the lesser divergence, and the stamped key makes the
  absorption self-consistent afterwards. Owned by
  `todo/runtime-entity-markerless-bind-absorption.md`.
- The enemy domain's own late-join copy (`EnemySyncCoordinator.CreateRuntimeSpawn`)
  carries no creation marker; it binds a live re-report only through the
  markerless 1 m positional pass, so a late joiner's animal that has already
  drifted can still be duplicated by a surviving live re-report. Unifying the
  animal live relay with the enemy backfill identity stays with
  `todo/enemy-snapshot-and-attack-recovery.md` (N1).
- The source-excluding `BroadcastEntitySpawned` relay is dead API (no caller):
  `todo/runtime-entity-dead-api-cleanup.md`.
- The adapter shell (the Unity create + `FindExisting` scan, the death-hook
  wiring, the geyser queue flush, the `WorldParamsService` apply call site, the
  `RuntimeEntityCreation` stamping, the mod-hook cleanup) is not exercisable in
  the test host — it needs the live Unity world. Those branches rest on code
  review plus the unified dual-client acceptance pass; the pure match judgment
  and every Runtime table/cadence path are covered by tests.
  `future/adapter-shell-verification-harness.md` records the verification-gap
  option, deferred by decision.
