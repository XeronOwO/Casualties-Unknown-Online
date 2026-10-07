# The Late-Join Enemy Generation Pairing — Self-Check (2026-10-07)

Delivery fact sheet for `docs/backlog/review/enemy-generation-pairing-late-join.md`, filed by acceptance
batch `20261006-g`: a member that entered the world about two minutes into a run logged
`generation spawn pairing failed (69 host vs 69 guest generated enemies)` and
`mapping=False` once per 60 s cycle for the whole session. The cycle fixes the pairing key and moves it out
of the adapter's call site; the runtime rows (three clients, one mid-run join) are the next batch's
(§4).

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The batch's reading — the red this cycle works from: the third client's entry sequence logs `[Enemy] guest froze 69 enemy copies at generation finish (before they move).` one second before its FIRST pairing, then the failure and `0 generated bound, 0 runtime spawns, mapping=False` on five consecutive cycles; the member present at world entry reads `69 generated bound, 0 runtime spawns, mapping=True` in the same cycles, and the host sends both peers identical `69 enemies, 0 runtime spawns` snapshots. The failure/success/host LINES are quoted in the in-tree record; the cycle count and the two timings are NOT — they were read from that batch's raw client logs, which live outside this tree, and the ticket says so (*Limits*). | `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md` (the quoted lines), the ticket's *Symptom* and *Limits* (the counts' provenance) |
| 2 | The pairing CALL SITE (pre-fix) built the host side twice with two different fields: `generatedHost` was ordered by `s.SpawnPosition` and then `EnemySyncCoordinator` passed `generatedHost.Select(e => e.Position)` into `EnemySpawnArbitration.TryPair`. `Position` is the live pose the 20 Hz stream refreshes, so the comparison held only in the instant after generation — which is why the member present at entry passed and a member whose first attempt ran two minutes later failed. | the pre-fix lines (commit `c80e2c40`, candidate filter added by `9dd120fe`), `EnemySyncService`'s `entity.SpawnPosition = existing.SpawnPosition;` (the stream refreshes `Position` and preserves the anchor) |
| 3 | The CONTRACT that already named the key: `EnemyEntity.SpawnPosition` — "where this enemy stood when the HOST first bound it … The guest pairs its frozen copies on THIS, never on `Position`: the live position keeps moving with the host's simulation, so pairing on it only holds in the instant after generation". The adapter contradicted it; nothing checked the two against each other. | `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemyEntity.cs` |
| 4 | Where the anchor comes from: `EnemyStateCapture.RecordAnchor` is called from `Bind`, so it is written at the host's FIRST bind of that entity, and the host's own id allocation (`EnsureMapping`) sorts those same first-capture positions. The anchor is therefore the generation position for a run the host started itself, and only then (the ticket's item 2). | `EnemySyncCoordinator.Bind` / `EnsureMapping`, `EnemyStateCapture.RecordAnchor` |
| 5 | The member's side of the pair: its generated copies are frozen the moment generation finishes (`FreezeOnGenerationComplete` → `Freeze`, rigidbody switched to `Static`), and the candidates are only the copies with no host id yet (`IsRepairCandidate`). A copy that already carries an id is never re-paired — that filter is the 20261002-f fix and stays. | the batch's `guest froze 69 enemy copies …` line, `EnemySyncCoordinator.FreezeOnGenerationComplete`, `EnemySpawnArbitration.IsRepairCandidate` |
| 6 | The cadence that separates the two members: the enemy table rides the world-entry snapshot AND the 60 s in-session repair, so the member present at entry pairs on its entry snapshot — the first one after its own generation — while a late joiner's first attempt lands minutes later. | `EnemySyncService`: `Replace(msg.Enemies, EnemySnapshotReceived)`, the batch's entry sequence |
| 7 | The baseline the whole key rests on: both sides generate the same animals from the host's captured `Random.state` (`ResetGenStreamToBaseline`), which is why the member's own copies — and not a set of positions shipped to it — are the pairing's other side. Its late-join parity is what ticket item 1 asks for and what row 2 measures. | `WorldParamsService.ResetGenStreamToBaseline`, the batch's `Generation stream reset to captured baseline (…)` line |
| 8 | Why no pure test caught this, and why the existing premise test was GREEN while the product was broken: `PairingPremise_TheLivePositionStopsMatching_WhileTheSpawnAnchorStillPairs` handed `TryPair` a hand-picked anchor list, so it proved the PRIMITIVE could pair at any time — the caller's choice of field was outside its reach — and the test assembly references the GameAdapter with `ExcludeAssets="compile"`, so the adapter's wiring is judged by a batch. | `tests/CasualtiesUnknownOnline.Tests/Session/EnemySnapshotRecoveryTests.cs` (pre-fix), `tests/CasualtiesUnknownOnline.Tests/CasualtiesUnknownOnline.Tests.csproj` |
| 9 | The FAMILY: the runtime-spawn channel pairs the host's LIVE pose against the member's copy on purpose — its fact is refreshed by the 20 Hz stream, the stream carries NO anchor (`EnemyStreamWireMapper.ApplyTo` writes none; `EnemySyncService` preserves one only for an id a snapshot already buffered), so a runtime enemy first learned from the stream has no anchor to key on, while the member's copy was created where the creation report placed that animal and frozen there; the creation-key pass outranks the positional one where a key exists. | `EnemyRuntimeSpawnArbitration.TryPairByPosition`, `MatchRuntimeSpawnsByCreationKey`, `EnemyStreamWireMapper.ApplyTo`, `EnemySyncService`'s stream-apply loop |

## 2. The change

- `EnemySpawnArbitration` (Runtime) gains `TryPairGeneratedCopies(hostGeneratedFacts, copyPositions, out
  pairs, out divergence)`: it orders the host's facts by the anchor each fact carries, orders the member's
  copies by their own (frozen) positions (a STABLE order, matching the host side's, so two copies at one
  position keep the rank the host side gives them), pairs index-by-index inside `PairTolerance`, and returns
  each pair with the CALLER's own copy index plus that pair's key distance. The key is now derived where the
  pairing is decided, and the call site cannot substitute the moving field because it no longer supplies a
  position list at all. What that does NOT enforce: `EnemyEntity.SpawnPosition` is still settable, so an
  adapter line stamping the anchor from `Position` would restore the pre-fix key with no test red — nothing
  in the test tree inspects the adapter's choice (§4).
- `EnemySpawnArbitration.PairingDivergence` (new nested value type) carries both counts, an explicit
  `GeneratedPairingOutcome` (`Paired` / `CountMismatch` / `KeyMismatch`), and on a key mismatch the first
  index whose distance exceeded the tolerance with that distance. The outcome is a FIELD rather than
  something inferred from the index sentinel, so `IsCountMismatch` is false for a successful pair — the
  reviewer's finding on the first draft, where the -1 sentinel meant both "no index compared" and "success".
- `EnemySpawnArbitration.TryPair` and `Order` are DELETED, not kept beside the new entry point: the
  coordinator was `TryPair`'s only production caller and `Order` had none once the ordering moved in. The
  class keeps `Compare`/`Distance`/`PairTolerance` (shared with `EnemyRuntimeSpawnArbitration`) and the
  three predicates (`ShouldRepairGenerationBaseline`, `IsRepairCandidate`, `AssertedBoundCopies`).
- `EnemySyncCoordinator.OnEnemySnapshotReceived` builds the fact list and the candidate list, hands both to
  the arbitration, binds the paired copies by the returned index, and reports three readings: a pass logs
  the WORST key distance of the set beside the existing `mapping=True` line, a count mismatch and a
  first-key mismatch log their own warning shapes (both keeping the `generation spawn pairing failed` prefix
  the batches grep for). The empty-candidate path still preserves the established baseline, and
  `ShouldRepairGenerationBaseline` is called exactly as before.
- `EnemyRuntimeSpawnArbitration.TryPairByPosition`'s doc no longer claims both lists hold "the same
  spawn-command positions": it now states that its key is the live pose deliberately, and states the
  VERIFIABLE reason — the stream is that channel's carrier and carries no anchor, so a runtime enemy the
  member first learned from the stream has none — rather than the first draft's "the copy never stands at
  the host's anchor", which the review showed is not what that path's copies do.
- `docs/evidence/sync-coverage-evidence.json`: the N1 anchor that quoted the coordinator's
  `.OrderBy(s => s.SpawnPosition, comparer)` is re-pointed to the arbitration's
  `OrderBy(fact => fact.SpawnPosition, comparer)`, the line that now carries the claim. Entry count
  unchanged (1031).
- No wire member, no save shape, no protocol number: the anchor already travels in
  `EnemyStateMsg.SpawnPosition`.

## 3. Verification

Mechanism × change × evidence, every cell filled:

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | The host-side pairing key (the defect) | derived from the fact's anchor inside the arbitration | `TryPairGeneratedCopies_LateJoin_PairsOnTheAnchorTheLivePoseHasLeft` (asserts first that the live pose is beyond tolerance, so the case fails if the key reads `Position` again); mutation A (§ below) turns it red |
| 2 | The ordering of both sides | moved into the arbitration; the caller's list order is reported back as an index | `TryPairGeneratedCopies_OrdersBothSidesByTheAnchor_AndReportsTheCallersCopyIndexes` (rank order + the caller's indexes + input-order independence); mutation B turns it red |
| 3 | The refusal's reading | counts + an explicit outcome + first mismatch index + its distance, in two distinct warning shapes | `…_CountMismatch_ReportsBothCounts` (outcome `CountMismatch`), `…_OutOfTolerance_ReportsTheFirstIndexAndItsDistance` (outcome `KeyMismatch`) |
| 4 | The success's reading | the worst key distance of the paired set, logged beside `mapping=True`; the outcome distinguishes a pass from a refusal | `…_WithinTolerance_PairsAndReportsTheKeyDistance` (the pair carries its distance, the outcome is `Paired` and `IsCountMismatch` is FALSE — the reviewer's finding on the first draft, where the sentinel made it true); the runtime half is ticket row 2 |
| 5 | The candidate filter and the baseline latch (unchanged) | not touched | the three predicate theories still pass, plus `RepairPairing_AlreadyBoundCopies_DropOutOfTheCandidateSet` and `RepairPairing_UnboundCopies_StillPairOnTheSpawnAnchor` |
| 6 | The wiring itself (Unity) | the call site hands facts, not positions | not testable in-process (item 8 above); ticket rows 1-5 on the deployed artifact |
| 7 | The N1 evidence anchor | re-pointed to the line that now carries the claim | `SyncCoverageGateTests.SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine` (it failed on the moved line before the re-point — the gate caught exactly this) |

**The red.** Batch `20261006-g` read the failure on the deployed artifact, 5/5 cycles, with the member
present at entry green in the same cycles (§1 row 1). A test that fails on the PRE-fix tree cannot exist:
the wrong key lived in a line the test assembly cannot compile against (§1 row 8). The pre-fix key is
therefore reproduced INSIDE the new entry point as mutation A, on the fixed tree.

**Mutations** (each applied to `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemySpawnArbitration.cs`,
run against the focused pair of test classes, then restored; the file was byte-identical before and after
every mutation — worktree bytes SHA-256
`367B75C10B4DF4658F108FFB3EA8FEBFB63864864259FAD5BBD21CBBFB86BACC`, and the LF-normalized content the
`.gitattributes` filter (`* text=auto eol=crlf`) stores as the blob hashes to
`CC21471D43E3F1D88A90A09685A7D0D2CC8DF12E56E6AA411608EB18467536FC`):

| # | Mutation | Result |
|---|---|---|
| A | the compared key reads `hostOrdered[i].Position` — the exact pre-fix behaviour | 4 of 25 failed: the late-join case, `RepairPairing_AlreadyBoundCopies_DropOutOfTheCandidateSet`, `RepairPairing_UnboundCopies_StillPairOnTheSpawnAnchor`, `PairingPremise_TheLivePositionStopsMatching_WhileTheSpawnAnchorStillPairs` |
| B | the host ordering reads `fact.Position` instead of `fact.SpawnPosition` | 2 of 25 failed: the late-join case, `RepairPairing_UnboundCopies_StillPairOnTheSpawnAnchor` |

Both mutations were re-run on the FINAL revision of the file (after the review's fixes), so the two digests
above and the two red sets are reproducible from this tree. The reviewer independently hand-executed the
same two mutated versions against the same 25 cases without touching the frozen tree and reproduced the
same red sets and case names.

**Runs.** Focused: `EnemySpawnArbitrationTests` + `EnemySnapshotRecoveryTests` 25/25 pass (17 + 8). Normative
gates 450/450 pass (the N1 anchor re-point was required by them, §3 row 7). Full suite: recorded at the
cycle's final run (§4 notes what it does not prove).

## 4. Limits

- The placing is Unity wiring and the test assembly cannot compile against the GameAdapter
  (`ExcludeAssets="compile"`), so the pure layer is pinned by tests while the wiring is judged by the
  batch's rows — the same division `done/enemy-snapshot-binding-recovery.md` recorded.
- Nothing checks the ADAPTER's key choice. The arbitration derives the key from the facts, but
  `EnemyEntity.SpawnPosition` is settable, so an adapter line stamping the anchor from the live pose re-creates
  the pre-fix behaviour with no case red; the regression case fails only for a key read inside
  `EnemySpawnArbitration` itself (the reviewer's finding on the first draft's stronger wording).
- Ticket item 1 (does a late joiner's own regeneration reproduce the host's generation positions?) is not
  proven here: row 2 measures it, now with the worst-distance reading beside it, and a broken parity would
  show as a first-divergent-index line instead of a silent failure.
- The cycle count and the two entry timings this sheet repeats from batch `20261006-g` are NOT reproducible
  from this tree: they were read from that batch's raw client logs. The quoted failure/success/host lines are
  in the in-tree record, and the ticket labels the difference.
- The two refusal readings are diagnostics for a divergence the acceptance rows expect NOT to happen, so no
  row drives them; they are pinned by the pure cases only.
- The anchor is the host's FIRST-BIND position, so an animal the host first bound after it moved remains
  unpairable (ticket item 2, unchanged and still recorded in `EnemyStateCapture`).
- A generation divergence wider than `PairTolerance` at any one index still refuses the whole set, by
  design: the member's copies stay local and the failure line now says where. No fallback pairing and no
  identity that survives a divergence were introduced.
- Not read on the machine in this cycle: rows 1-5 need three clients and a mid-run join.
