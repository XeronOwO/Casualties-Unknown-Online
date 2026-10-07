# A member joining a run in progress never binds the generated enemies

- Status: Review (landed 2026-10-07 — the pairing key is the fact's bind-time anchor, owned by the
  arbitration; the runtime rows await the next batch)
- Priority: High
- Category: Enemy sync / generation baseline / late join
- Source: batch `20261006-g`'s third client (2026-10-06) — a member that joined the lobby and entered the
  world AFTER the run had started logs the pairing failure once per 60 s cycle for the whole session while
  the member that was present at world entry reads the mapping as established in the same cycles. Found by
  the acceptance run while reading that batch's inventory rows, which are unaffected.
- Related: `done/enemy-runtime-spawn-classification.md` (the fix that made the generation baseline read
  `mapping=True` for a member present at world entry; this ticket is the case its acceptance never drove),
  `done/enemy-snapshot-binding-recovery.md` (the repair carrier), `done/layer-change-member-dropout.md`
  (the other member-lifecycle defect), `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemySyncCoordinator.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemySpawnArbitration.cs`,
  `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md`

## Symptom (read from batch `20261006-g`, three clients, one lobby)

The run started with two members in the world; the third joined the lobby and entered the world about two
minutes later. From its own entry onward it logs, every 60 s:

```text
[WRN] [Enemy] generation spawn pairing failed (69 host vs 69 guest generated enemies) — generated copies stay local (generation divergence); runtime spawns are still bound.
[INF] [Enemy] snapshot applied: 0 generated bound, 0 runtime spawns, mapping=False.
```

Its entry sequence shows the freeze itself working — `[Enemy] guest froze 69 enemy copies at generation
finish (before they move).` one second before the first failure — so **the first pairing already fails**;
this is not a case of copies drifting after a successful bind. The member that was in the world from the
start reads the same cycles as established: `[Enemy] snapshot applied: 69 generated bound, 0 runtime
spawns, mapping=True.`, and the host sends both peers identical snapshots (`[Enemy] snapshot sent to
<steamid>: 69 enemies, 0 runtime spawns.`).

The timing and the cycle counts in this section come from the batch's raw client logs, which live OUTSIDE
this tree; what the tree carries is the quoted lines themselves, in the batch record's rows (*Limits*).

The counts agree on both sides, so nothing is missing on the wire: the two ordered position lists do not
line up index by index.

## What the readings already rule out

- **Not a count mismatch**: the warning itself reports `69 host vs 69 guest`, and the host's own snapshot
  line says 69.
- **Not the runtime-spawn half**: both clients and the host report `0 runtime spawns`, and the warning says
  runtime spawns stay bound.
- **Not a late drift of the guest's copies**: the copies are frozen at generation finish, one second before
  the failing pass, so whatever is wrong is already true at freeze time.
- **Not a missing repair**: the failure repeats every cycle for the whole session (five consecutive cycles
  read), so the 60 s repair pass runs and keeps failing rather than never running.

## What the traces point at, and what a fix has to decide

The pairing is all-or-nothing: it sorts both sides by `(x, y)` ascending and fails the WHOLE set when any
index-pair exceeds `PairTolerance = 0.5f` world units (pre-fix that was `EnemySpawnArbitration.TryPair`,
which this cycle replaced with `TryPairGeneratedCopies`; the verdict itself is unchanged). One side of that
comparison is the host's own anchor set (`EnemyStateCapture`'s spawn position per bound id), which is the
same input for every member; the other is the joining member's own copies. So the open question is what the
late joiner's copies are and where they stand:

1. **Are the copies at the deterministic generation positions at all?** A member present at world entry
   generates its own animals from the same seed as the host; a member entering an ALREADY RUNNING world
   resets its generation stream to the host's captured baseline (`[WorldParamsService] Generation stream
   reset to captured baseline (…)`) and then generates. Whether that reset makes its
   `WorldGeneration.DistributeEntities` output identical to the host's generation-time output is the first
   thing to read — the fix needs the two ordered lists side by side and the first index whose distance
   exceeds the tolerance, not the aggregate count.
2. **Is the host's anchor still the generation position by then?** The capture comment states the anchor is
   the host's FIRST-BIND position, which equals the generation position only while generation has just
   finished. A late join is by definition a bind attempt long after the host's own generation — if any
   anchor was written at a first bind that happened after the animal moved, the late joiner can never match
   it, while the member present at entry still can.
3. **The decision the fix takes**: either the joining member is handed the host's authoritative generated
   positions (a deterministic key that survives a late entry) instead of being asked to reproduce them, or
   the pairing gains an identity that does not depend on where a copy stood at bind time. Either way the
   all-or-nothing verdict stays — what must not survive is a member whose whole generated set stays local
   for the session.
4. **The blast radius is the same family**: the pairing key is also what the runtime-spawn bind and the
   per-cycle repair use, so the fix has to say what happens to a member whose generated set never pairs
   (today: it keeps logging every 60 s and its animals never follow the host).

## Landed (2026-10-07)

Fact sheet: `docs/evidence/selfchecks/enemies/enemy-generation-pairing-late-join-selfcheck.md`.

The readings resolve to ONE field. The pre-fix call site ordered the host's facts by their bind-time
anchors (`OrderBy(s => s.SpawnPosition, …)`) and then compared `EnemyEntity.Position` — the host's LIVE
pose, refreshed by the 20 Hz stream — against the member's copies, which are frozen at their generation
positions. Pairing on the live pose holds only in the instant after generation, so a member whose attempt
runs later can never pair, and the all-or-nothing verdict then fails the whole set; the member present at
entry passed because its first pairing is its world-entry snapshot, taken right after its own generation —
not because anything about that member differs. The type's own contract already said
which field is the key (`EnemyEntity.SpawnPosition`: "The guest pairs its frozen copies on THIS, never on
`Position`") — the caller chose otherwise, and no pure test could see it (self-check §1 row 8).

- **The key moved out of the caller.** `EnemySpawnArbitration.TryPairGeneratedCopies` (Runtime) takes the
  host's generated FACTS, orders them by the anchor each fact carries, orders the member's copies by their
  own (frozen) positions, and pairs index-by-index inside `PairTolerance`, returning the caller's own copy
  indexes. The call site no longer supplies a position list at all, so the field cannot be substituted AT
  THAT CALL. What that does not enforce: the fact's own anchor field is still settable, so an adapter line
  that stamps `SpawnPosition` from `Position` would restore the pre-fix key with no test red — the test
  assembly cannot compile against the adapter (*Limits*).
- **The verdict stays all-or-nothing** (item 3's constraint): a divergence is reported, never guessed
  around, and a member whose set does not pair keeps its copies local exactly as before.
- **A refusal says WHERE it diverged.** `PairingDivergence` carries both counts plus the first index whose
  key distance exceeded the tolerance and that distance; the failure line prints them, and the two failure
  shapes (count mismatch / first key mismatch) are separate lines. Batch `20261006-g` could read only the
  counts, which cannot separate item 1's "the member's stream did not reproduce the host's set" from a wrong
  key — the new reading can.
- **A success reports what the verdict hides.** The paired pass logs the WORST key distance of the set
  beside `mapping=True`, so the next batch's row also shows how far inside the tolerance the two generation
  outputs sit.
- **Item 3's other option was not taken, deliberately.** Handing the joiner the host's generated positions
  would replace the deterministic-baseline architecture every other generation domain (blocks, ores,
  structures, ground items, buildings) already pairs on. The identity was never missing — the member
  regenerates the host's world from the captured `Random.state` baseline — only the field read.
- **Item 2's limit is unchanged and stays recorded**: the anchor is the host's FIRST-BIND position, equal
  to the generation position only while generation has just finished. An animal the host first bound after
  it moved remains unpairable, and `EnemyStateCapture` still reports a missing anchor loudly.
- **The family was audited, not just the reported case.** The runtime-spawn channel
  (`EnemyRuntimeSpawnArbitration.TryPairByPosition` and the snapshot's positional pass) pairs the host's
  LIVE pose against the member's copy **deliberately**: that channel's fact is refreshed by the 20 Hz
  stream, and the stream carries no anchor at all (`EnemyStreamWireMapper.ApplyTo` writes none, and
  `EnemySyncService` preserves one only for an id a snapshot already buffered), so a runtime enemy the
  member first learned from the stream has no anchor to key on — while its own copy was created where the
  creation report placed that animal and is frozen there. That difference is now written into that class's
  doc, so the next reader cannot "align" the two channels by analogy.
- **No wire member, no save shape and no protocol number changed**: the anchor already travels in
  `EnemyStateMsg.SpawnPosition`; only the field a comparison reads changed.

## Limits

- The placing is Unity wiring, and the test assembly references the GameAdapter with
  `ExcludeAssets="compile"`: no in-process test can reach `OnEnemySnapshotReceived`. The pure layer is
  pinned by tests and the wiring is judged by the rows below on the deployed artifact — the same division
  `done/enemy-snapshot-binding-recovery.md` recorded.
- Nothing inspects the ADAPTER's key choice: the arbitration derives the key from the facts, but
  `EnemyEntity.SpawnPosition` is settable, so an adapter line that stamps the anchor from the live pose
  would restore the pre-fix behaviour with no test red. The new regression case fails only for a key read
  inside `EnemySpawnArbitration` itself. Judging the adapter's line is the batch's job, as for every other
  Unity-wiring claim here.
- The RED for this defect is the batch reading that filed it (5/5 cycles on the deployed artifact) plus the
  mutation on the new entry point that restores the pre-fix key and turns the new cases red (self-check §3).
  A test that fails on the PRE-fix tree cannot exist: the key choice lived in a line no test can reach.
  The cycle count and the entry timings above are read from that batch's raw client logs, which are NOT in
  this tree; the in-tree record quotes the lines themselves (`docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md`).
- The two refusal readings (count mismatch, first key mismatch) are diagnostics for a divergence the rows
  expect NOT to happen, so no acceptance row drives them: they are pinned by the pure cases, and a row that
  drove them would have to break a member's generation parity on purpose.
- Item 1 — whether a late joiner's regeneration reproduces the host's generation positions exactly — is
  what row 2 measures, now with the worst-distance reading beside it. If that parity is broken, the same
  row reads the first divergent index instead, and the second fix (handing the member the host's positions)
  is a separate cycle.
- Not read on the machine in this cycle: rows 1-5 need three clients and a mid-run join.
- The anchor remains a position, so a member whose generation output differs from the host's by more than
  `PairTolerance` at any single index still cannot bind; that verdict is unchanged by design.

## Acceptance

Read on the deployed artifact, three clients, one lobby, and judged on the joining member's own window:

1. Join the lobby and enter the world BEFORE the host starts the run (the reference): the member reads
   `[Enemy] snapshot applied: N generated bound, 0 runtime spawns, mapping=True.` from its entry on.
2. Join the lobby and enter the world AFTER the run has started (the case this ticket is filed from): the
   member reads the same `mapping=True` line with the same `N generated bound` within one repair cycle of
   its entry, and **zero** `generation spawn pairing failed` lines for the rest of the session.
3. In that session, move a host-driven enemy and read its position on the late joiner: it follows the host
   like any bound copy, and the third client's view agrees.
4. A member that joins mid-run while the host holds runtime spawns reads them bound on the same pass
   (`R runtime spawns` matching the host's count).
5. Regression: the member present at world entry keeps its green line and the host's log carries no new
   warning.
