# The Enemy Roster Adoption — Self-Check (2026-10-21)

Delivery fact sheet for `docs/backlog/todo/enemy-generation-pairing-late-join.md`: the rejected
position-as-identity pairing scheme is replaced by the host's roster, and a member adopts its local enemy
copies row by row. The RED is batch `20261025-d` on the rejected scheme (85 host vs 85 guest, first key
mismatch at index 52, 17.021 units apart, both members present at world entry, every 60 s cycle);
`## Rejection` in the ticket carries the quoted lines and the reasoning.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The host's durable enemy facts: id, prefab, runtime flag, health | `EnemyState` / `EnemyKernelProjection` (the host commits upserts/removals; the member's kernel is the replay and the checkpoint carries the table) |
| 2 | The absolute state projection the member adopts | `EnemySnapshotMsg` (one generation-stamped row per enemy: id, prefab, match hint, current state; the runtime rows' creation key/tint ride the runtime-spawn detail list; sent at entry and on the 60 s repair) |
| 3 | The host's capture | `EnemySyncCoordinator.CaptureHostEnemies` walks the bound table (`EnemyBindingTable`) and prunes only Unity-destroyed objects — a scan-only list drops a live enemy's id |
| 4 | The member's judgment | `EnemyRosterAdoption.Plan` (creation key first, then nearest same-prefab inside the reuse radius; unmatched rows materialize; unclaimed copies are surplus; a row without a prefab is unresolved) |
| 5 | The member's Unity apply | `EnemyRosterBinder` (reuse and freeze, materialize at the host's current position with creation key/tint, destroy surplus, live-adopt a stream-announced new id) |
| 6 | The identity table both roles share | `EnemyBindingTable` (one removal operation over both directions; `PruneDead` is the confirmed death) |
| 7 | The empty and stale gates | `EnemySyncService` sends an empty roster; refuses a roster of another generation (`WorldReportGenerationGate`); the host holds snapshots while its world is generating or absent (`MarkRosterReady`/`MarkRosterUnstable`) |
| 8 | The layer lifecycle | `GameAdapterBridge.OnWorldGenerate` → `EnemySyncCoordinator.OnWorldGenerationStarted`; `EnemiesResetEvent` clears the member's buffer and bindings |
| 9 | The family this replaced | `EnemySpawnArbitration` (all-or-nothing generated pairing), `EnemyRuntimeSpawnArbitration`'s positional passes, the empty-snapshot no-op, the scan-only state list — all gone, pinned absent by the source-shape gate |

## 2. The change

The user chose all four rebuild options (2026-10-21): the host's roster is the identity; an unmatched copy
converges per copy; generated and runtime-spawned enemies share one host-id identity; divergence is
reported per row and is recoverable. The implementation is the eight mechanisms above; the pure adoption
matrix replaces the two deleted arbitration test suites, and the source-shape gate pins the adapter wiring
(the roster adoption is the only match; the old matching names are absent; the empty roster is sent; the
snapshot is stamped; the generation boundary resets the roster).

## 3. Verification

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | Reuse inside the radius / replace outside it | nearest same-prefab copy is reused; a row outside the radius materializes and the drifted copy destroys | `EnemyRosterAdoptionTests.Plan_ReusesTheNearestSamePrefabCopy_AndDestroysTheFarOne`, `…_RowWithNoCopyInsideTheRadius_IsMaterialized_AndTheDriftedCopyDestroyed` |
| 2 | Creation-key discipline | a keyed row binds the keyed copy even when a keyless copy is nearer; a keyed row never absorbs a keyless copy | `…_CreationKey_BeatsPosition`, `…_KeyedRowWithNoKeyedCopy_IsMaterialized_NotPositionallyAbsorbed`, `…_KeylessRow_NeverConsumesAKeyedCopy` |
| 3 | Empty and complete rosters | an empty roster destroys every local copy; an unresolved row defers the surplus destruction (the caller's rule, pinned by the source-shape gate) | `…_EmptyRoster_DestroysEveryLocalCopy`, `…_MissingPrefabRow_IsUnresolved_NotMaterialized`, `…_OneUnresolvedRow_DoesNotBlockTheOtherRows` |
| 4 | Determinism | same-prefab ties are resolved by position, not by the caller's candidate order | `…_EquidistantCopies_PickTheSameCopyRegardlessOfInputOrder`, `…_OneCopyServesOneRow` |
| 5 | Snapshot semantics | the empty roster travels and fires; the match hint survives the stream; a stale generation is refused; the reset clears the buffer | `EnemySnapshotRecoveryTests.EmptyEnemyTable_TheRepairSendsTheAuthoritativeEmptyRoster`, `…_StreamFrames_DoNotForgetTheMatchHint`, `…_SnapshotOfAnotherGeneration_IsRefused`, `…_RosterReset_ClearsTheGuestBufferAndRaisesTheAdapterEvent` |
| 6 | The service's absolute table | entry and repair deliver the rows; a repeat does not duplicate; the empty table is authoritative | `EnemySyncServiceTests.WorldEntry_ReceivesFullEnemySnapshot`, `…_WorldEntry_NoEnemies_SendsTheAuthoritativeEmptyRoster`, `…_EnemySnapshot_Overwrites_PreviousBuffer`, `EnemySnapshotRecoveryTests.EnemiesPublishedAfterTheEntryEdge_StillReachAStayingMember` |
| 7 | Adapter wiring | the host states come from the bound table; the binder is the only match; the old names are gone; the empty roster and the stamps are in place | `SourceShapeGateTests.EnemyRosterAdoption_IsTheOnlyMatch_AndTheEmptyRosterIsAuthoritative` |
| 8 | The whole pass | three clients, entry and mid-run join, drift, runtime spawn, layer descent | the ticket's `## Acceptance`; NOT RUN in this cycle (the batch is the next step) |

**The RED** is batch `20261025-d` on the rejected artifact (the ticket's `## Rejection`). The rebuild itself
has no pre-fix tree to fail against — the old entry point is deleted, so a regression case would have to
re-create the scheme; the source-shape gate refuses its names instead.

**Runs:** the focused filter over `EnemyRosterAdoptionTests|EnemySnapshotRecoveryTests|EnemySyncServiceTests|EnemyStateRoundtripTests|EntryRepairConvergenceTests` 66/66; the full suite and normative gates are recorded at the cycle's final run (the delivery checklist's build row). The independent review's dispositions are the ticket's `## Review (2026-10-21)` section.

## 4. Limits

- The adapter is Unity wiring (`ExcludeAssets="compile"`): the pure judgment is tested, the apply is judged
  by the batch and pinned by source shape.
- A materialized generated enemy is created through the runtime-creation path; presentation, damage and drop
  ownership are batch rows, not unit tests.
- The match hint is a reuse heuristic; a copy outside the radius is replaced (one-frame pop), never bound
  far away.
- A row without a prefab is retried; the surplus destruction is deferred while any row is unresolved.
- The batch cannot force a generation divergence; the drift row depends on the host's world running before
  the session.
