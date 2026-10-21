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
| 3 | The host's capture | `EnemySyncCoordinator.UpdateHost` captures ONLY over a settled world (`WorldIsSettled` = `EnemyRosterAdoption.CanPublishRoster`; the not-settled branch returns before the capture) and `CaptureHostEnemies` walks the bound table (`EnemyBindingTable`), pruning only Unity-destroyed objects — a scan-only state list drops a live enemy's id |
| 4 | The member's judgment | `EnemyRosterAdoption.Plan` (creation key first, then nearest same-prefab inside the reuse radius; unmatched rows materialize; unclaimed copies are surplus; a row without a prefab is unresolved) |
| 5 | The member's Unity apply | `EnemyRosterBinder` (reuse and freeze, materialize at the host's current position with creation key/tint, destroy surplus, live-adopt a stream-announced new id from the kernel fact's prefab AND runtime marker — the stream frame carries neither) |
| 6 | The identity table both roles share | `EnemyBindingTable` (one removal operation over both directions; `PruneDead` is the confirmed death) |
| 7 | The empty and stale gates | `EnemySyncService` sends an empty roster; refuses a roster of another generation (`WorldReportGenerationGate`); the host holds BOTH the snapshot and the 20 Hz stream while its world is generating or absent (`EnemyRosterPublisher.IsReady` read by `EnemySyncService.BroadcastEnemyState`; `MarkRosterReady`/`MarkRosterUnstable`) |
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
| 7 | Adapter wiring | the host states come from the bound table; the binder is the only match; the old names are gone; the empty roster and the stamps are in place; the live pass's identity inputs come from the kernel fact | `SourceShapeGateTests.EnemyRosterAdoption_IsTheOnlyMatch_AndTheRosterGatesArePinned` |
| 8 | The whole pass | three clients, entry and mid-run join, drift, runtime spawn, layer descent | the ticket's `## Acceptance`; batch `20261021-e` judged rows 1/2/3/5/6 pass and row 4 fail |

**The RED** is batch `20261025-d` on the rejected artifact (the ticket's `## Rejection`). The rebuild itself
has no pre-fix tree to fail against — the old entry point is deleted, so a regression case would have to
re-create the scheme; the source-shape gate refuses its names instead.

**Runs:** the focused filter over `EnemyRosterAdoptionTests|EnemySnapshotRecoveryTests|EnemySyncServiceTests|EnemyStateRoundtripTests|EntryRepairConvergenceTests` 70/70 on this tree (§5's fixes included); the full suite and normative gates are recorded at the cycle's final run (the delivery checklist's build row). The independent review's dispositions are the ticket's `## Review (2026-10-21, before the rerun)` section.

## 4. The row-4 fix (2026-10-21, after batch `20261021-e`)

Batch `20261021-e` rejected row 4: no member live-adopted a runtime spawn, the attack was dropped as an
unknown enemy and the row was adopted only by the next snapshot. The fix has two halves, each with its RED
recorded in `.acceptance/red-20261021-f.log`:

- **The live pass's identity inputs.** `EnemyRosterBinder.TryAdoptNewRows` read the prefab from the kernel
  fact but the runtime marker from the 20 Hz buffer row, which is default-false for an id no snapshot has
  introduced; `FindLiveCopy` then refused the creation-channel copy that was the only bindable copy. Fixed:
  `EnemySyncService.TryGetEnemyFacts` returns the prefab and the runtime marker from the SAME kernel fact,
  and the binder passes that marker. RED: the source-shape gate failed on the old binder (the pin is now
  in place and also refuses `state.RuntimeSpawned` there). GREEN:
  `EnemySyncServiceTests.KernelFact_SuppliesBothLiveRowIdentityFields`.
- **The start gate's late-joiner verdict.** The batch's "second gap" (no repair group after 19:29:52) was
  the members being returned to the menu by their 60 s valve: the run had started before the session
  existed, so no gate was ever armed for it and neither answer path released the arriving member. Fixed:
  `WorldStartGate.LateJoinerPasses` passes an in-world-arriving member when the gate was released OR when
  no gate was armed over the run and the host is already in the world; an armed gate still holds its
  members. RED: `WorldStartGateTests.RunAlreadyPlayingWhenTheMemberArrived_PassesWithoutAnArmedGate` and
  `…_SwallowedReleaseIntoAnAlreadyPlayingRun_IsAnsweredOnTheRepeat` failed before the fix;
  `…_AnArmedGate_StillHoldsTheMemberUntilItsOwnRelease` passed before and after.
- **The host's phase in that same run (found by the independent review of this fix).** The same skipped
  world-entry edge and never-armed gate also skip every `MarkPlayingForHost`, so the loading-screen keeper
  pinned the native loading screen over the host's running game for the rest of the session (the host log's
  `[Gate] keeping the loading screen up while waiting for the host.` at 19:28:45.502, with nothing ever
  releasing it). Fixed:
  `StartGateCoordinator.ResumeHostPlayingIfTheSessionRoseAroundTheRun` marks the host playing when a
  session rises around an already-running world (host, active session, in the world, not yet playing, no
  armed gate); pinned by
  `SourceShapeGateTests.RunLifecycle_ResumesAHostWhoseSessionRoseAroundARunningWorld`. The rerun reads the
  line and the keeper's absence from the host log.
- **The gate's per-run flags (same review).** `_released`/`_armed` survived into the next run of one session
  until the host's own entry armed it, so a member whose world came up first could be passed by the previous
  run's verdict. `SetHostRunPending(true)` (the host's click) now clears both; pinned by
  `WorldStartGateTests.ANewRun_ForgetsThePreviousRunsRelease`.
- **The source-shape pin's hand-off (same review).** The pin now also asserts the `TryGetEnemyFacts` call's
  two-column signature and the `FindLiveCopy(..., runtimeSpawned, ...)` argument, so the call cannot stay
  intact while the marker is dropped.

**Runs:** the focused filter over `WorldStartGateTests|EnemySyncServiceTests|EnemyRosterAdoption_IsTheOnlyMatch|RunLifecycle_ResumesAHostWhoseSessionRoseAroundARunningWorld` 21/21; the full suite and normative gates are recorded at the cycle's final run (the delivery checklist's build row). The independent review's dispositions are the ticket's `## Review (2026-10-21, before the rerun)` section.

## 5. The batch-`20261021-f` fixes (2026-10-21)

Batch `20261021-f` rejected row 1 on a 956-animal world (the host's early capture put 83 rows in the
kernel, the world-entry reset dropped them and cleared both members' tracked candidates, and the entry
adoption built 946 copies; `aligned=False` until the repair) and found the runtime-spawn classification
wrong at that scale (291 → 873 runtime spawns for a roster of generation output). The three fixes:

- **The capture waits for a settled world.** `EnemySyncCoordinator.UpdateHost` asks `WorldIsSettled`
  first and returns after `MarkRosterUnstable`; the capture is unreachable before that return. A run's
  first layer then has no kernel rows, and `LayerScopedTableReset.Reset` gates each table on its OWN
  count, so the empty enemy table commits no `EnemiesResetEvent` and the members are not reset at entry.
  The 20 Hz stream is held while the roster is not ready, because the buffer still names the layer being
  left.
- **The candidates are the tracked set ∪ a live scene scan.** `EnemyRosterBinder.AdoptRoster` reads
  `EnemyAnimalScan.Union(_localAnimals)`, the same union the host's capture and the guest's freeze pass
  use, so a reset that clears the tracking loses nothing while the copies stand in the scene.
- **The runtime-spawn rule is the generation's finished edge.**
  `EnemyRosterAdoption.IsRuntimeSpawn(sessionActive, generationEdgeObserved, existedAtGenerationEdge)`
  over the set both roles record once per layer in `MarkAnimalsCreatedByGeneration`; a deferred `Start`
  of a generation animal no longer reads as a runtime spawn.

RED, all in `.acceptance/red-20261021-g-*.log`:
`SourceShapeGateTests.EnemyRosterCapture_WaitsForASettledWorld_AndTheGenerationEdgeOwnsTheRuntimeClassification`
failed on the old tree (*"the host's frame pump (UpdateHost) was not found"*),
`EnemySyncServiceTests.RosterUnstable_HoldsThePeriodicStream_NotJustTheSnapshots` failed with
`Expected: 0, Actual: 1`, and `LayerBoundaryKernelResetTests.LayerReset_AnEmptyEnemyTable_CommitsNoEnemyReset`
failed with `Expected: 0, Actual: 1` (the review's MAJOR 1: the entry reset still committed an
`EnemiesResetEvent` over an empty enemy table when the fluid table was not empty — `Reset` now gates each
table on its own count). GREEN: the same three, plus the classification matrix
`EnemyRosterAdoptionTests.IsRuntimeSpawn_OnlyAfterTheGenerationEdgeInASession` and the untouched
adoption/snapshot suites.

The independent review of this cycle (frozen tree, FULL tier: 0 blockers, 3 majors, 7 minors) is
dispositioned in the ticket's `## Fix cycle after batch 20261021-f` → `### Independent review`. Two
review fixes are structural and pinned rather than judged by a batch: `EnemyRosterBinder.DestroyCopy`
deactivates a copy before destroying it (a Unity destruction lands at the end of the frame, and the
adoption's scene scan would otherwise adopt a copy the same pass had just unbound — MAJOR 2), and
`EntitySpawnSync.OnEntityInstantiated` takes the enemy domain's generation fact so a generation animal's
deferred `Start` is not reported as a runtime creation (MAJOR 3; the non-animal half is the ticket's
limit).

**Runs:** the focused filter over
`EnemyRosterAdoptionTests|EnemySnapshotRecoveryTests|EnemySyncServiceTests|EnemyStateRoundtripTests|EntryRepairConvergenceTests`
70/70 on this tree; `LayerBoundaryKernelResetTests` 5/5 (the mixed-table case and the tombstone case now
carrying a live row); `WorldStartGateTests|EnemySyncServiceTests|EnemyRosterAdoption_IsTheOnlyMatch|RunLifecycle_ResumesAHostWhoseSessionRoseAroundARunningWorld`
21/21; the full suite and normative gates at the cycle's final run (the delivery checklist's build row).

## 6. Limits

- The adapter is Unity wiring (`ExcludeAssets="compile"`): the pure judgment is tested, the apply is judged
  by the batch and pinned by source shape.
- The host-resume pump and the gate's per-run reset are Unity/session wiring too: the pump's proof is the
  batch's host log, the reset's is the session test above.
- A materialized generated enemy is created through the runtime-creation path; presentation, damage and drop
  ownership are batch rows, not unit tests.
- The match hint is a reuse heuristic; a copy outside the radius is replaced, never bound far away. The live
  pass may provisionally bind a keyless same-prefab copy for a runtime row whose key it has not seen yet; the
  snapshot then destroys the creation copy and materializes the generated row's replacement (a same-prefab
  swap with a one-frame pop, identity stays by row).
- A row without a prefab is retried; the surplus destruction is deferred while any row is unresolved.
- The batch cannot force a generation divergence; the drift row depends on the host's world running before
  the session.
