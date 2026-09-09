# Runtime entity creation — markerless bind absorption + dead relay cleanup (2026-09-09)

Cycle: close the two round-4 residuals of the E3 runtime-entity landing
(`review/runtime-entity-spawn-backfill.md`). ProtocolVersion 17 → 18 (two additive
ProtoMembers). Red→green recorded; independent adversarial review and deployed-hash
verification are part of this cycle's completion gate.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Markerless positional bind | `RuntimeEntityMatch.FindIndex` second pass (pre-fix `RuntimeEntityMatch.cs:61-77`): any markerless same-prefab candidate strictly inside 1 m bound the record and `EntitySpawnSync` stamped it (`EntitySpawnSync.cs:238`). |
| Enemy late-join backfill | `EnemySyncCoordinator.CreateRuntimeSpawn` (`RuntimeSpawns.cs:62-91`) materialized from `EnemySnapshot.RuntimeSpawns` at the animal's CURRENT position with a `SpawnReplayMarker` only — no creation identity. |
| Trap-layout materialization | `TrapLayoutApplication.Materialize` (`TrapLayoutApplication.cs:85-104`) instantiates the host's layout and adds a `SpawnReplayMarker` only. |
| Creation identity | `RuntimeEntityCreation` marker (full `RuntimeEntityKey`), stamped at creation (`EntitySpawnSync.cs:163,238`) and read by the death funnel (`EntitySpawnSync.cs:116`). |
| Dead relay | `IWorldControl.BroadcastEntitySpawned` + `WorldService`/`WorldChannelRelay`/`RuntimeEntityChannel` forwards; no call site (`rg` evidence). |
| Live relay | `RuntimeEntityChannel.SendEntitySpawned` host branch broadcasts to every member including the source (the echo is the acknowledgement and carries the enriched keypad code). |

## 2. Whole-family audit

- The positional pass had exactly three candidate families: enemy backfill, trap-layout
  materialization, generated entity. All three were classified: the first two were given
  the creation key (wire + stamp), the third needs none (deterministic on both sides and
  carries its own key when it reports itself).
- The same identity gap existed in both directions of the animal path (host's own copy →
  snapshot → late joiner; guest's re-report → host bind) and in the trap path (host scan →
  layout wire → guest materialize). Both were aligned in the same cycle.
- The dead-API family was audited: `ISessionControl.BroadcastExcept` has 30+ live callers
  and stays; only the `NetMsg.EntitySpawned` forward was dead and is removed.
- Every evidence anchor whose line number shifted was re-verified mechanically (see §4);
  the matrix/JSON counts were corrected.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Match judgment | markerless 1 m pass deleted; single exact-key pass | `RuntimeEntityMatch.cs:46,50`; `RuntimeEntityMatchTests` (11) |
| Enemy backfill identity | `EnemyEntity.CreationKey` ← `RuntimeEntityCreation` read at capture (`EnemySyncCoordinator.cs:232`); `EnemySpawnEntryMsg.CreationKey` (ProtoMember 8, `EnemySpawnEntryMsg.cs:64`); key-first snapshot pairing (`EnemyRuntimeSpawnArbitration.MatchRuntimeSpawnsByIdentity`); `CreateRuntimeSpawn` stamps it (`EnemySyncCoordinator.RuntimeSpawns.cs:107`) | `EnemyEntity.cs:46,108`; `EnemyRuntimeSpawnArbitrationTests` (8 new cases); adapter stamp is code-reviewed |
| Trap-layout identity | `TrapEntityScan` reads the host's marker up the transform chain (`TrapEntityScan.cs:99`) → `ReportTrapLayout(..., creationKey)` (`IWorldControl.cs:296`) → `TrapLayoutRegistry` → `TrapLayoutEntryMsg.CreationKey` (ProtoMember 5, `TrapLayoutEntryMsg.cs:36`); `Materialize` stamps the copy (`TrapLayoutApplication.cs:125`); one copy per (prefab, position) via the pure `TrapLayoutMaterialization` | `TrapLayoutSimulationTests.WorldEntrySnapshot_CarriesTheCreationKey_OfARuntimeCreatedTrap`; `TrapLayoutMaterializationTests` (4) |
| Null-key handling | an unattributable runtime-spawn fact carries no key; `RuntimeEntityCreation.TryRead` tolerates a null entity | `EnemyStateRoundtripTests.EnemySpawnEntry_NoCreationKey_StaysNull`; `RuntimeEntityCreation.cs:60` |
| Dead API | `BroadcastEntitySpawned` removed from 4 files | `rg 'BroadcastEntitySpawned' src tests` → zero hits |
| Wire/protocol | two additive ProtoMembers; `ProtocolVersion.Current` 18 | `ProtocolVersion.cs:8`; `NetPacketTests` (2 new) |
| Evidence rot | 15 shifted anchors fixed, 3 new anchors added, counts 790 → 793 | `SyncCoverageGateTests` 32/32; matrix §S2 |

## 4. Verification results

| Evidence | Result |
|---|---|
| Red (pre-fix code, real run) | `FindIndex_MarkerlessSamePrefabDecoyAtTheRecordedPosition_NeverAbsorbsTheRecord` Expected -1 Actual 0; `FindIndex_MarkerlessBackfillCopyWithoutAKey_IsNotABindTarget` same — 2 failed / 10 passed |
| Focused green | 90+ across the touched families (match, arbitration, trap layout + materialization, enemy sync, roundtrip, packets) |
| Full suite + normative gates | 2 682 + 32 green |
| `dotnet format` | clean |
| Evidence quotes | 793/793 verified against the working tree (script + `SyncCoverageGateTests`) |
| Independent adversarial review | TWO fresh-context rounds: the first REJECTED with 1 blocker + 3 majors; the second REJECTED with 1 new blocker (candidate-index space) + deployment/evidence gaps. All findings fixed and re-verified (see the ticket's review section). |
| Deployment | `tools/deploy.ps1 -GameDir "<game-dir>"`; all six deployed CUO assemblies hash-equal the build output (Runtime `2CCFF1CF…`, GameAdapter `C674D350…`), and the deployed binaries contain the new symbols |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + normative gates + `dotnet format`.
- The pure match judgment and both wire identities are covered by tests (absorption,
  decoy, drift, sibling, round-trip, null-key).
- The adapter shell (marker read at capture, stamp at materialization, trap scanner key
  read) needs the live Unity world and is code-reviewed only — **not** dual-client
  verified by this cycle; the unified user acceptance pass is where the real world runs.

## 6. Plan approval

The user instructed this session to do these two follow-up tickets after the E3 landing
and to defer the save-system ticket pending six design answers, so this cycle's plan is
approved without a separate interactive approval step.

## 7. Structure review

- No new top-level types; two additive ProtoMembers and one nullable property per entity.
- `RuntimeEntityMatch` shrank to a single pass (fewer responsibilities, one identity rule).
- `RuntimeEntityChannel` and `WorldService` shrank by the deleted dead API.
- All touched classes stay under the 600-line gate; no new expression-state bools; no
  state-ownership change (the creation key is read from the existing marker and carried as
  data).
