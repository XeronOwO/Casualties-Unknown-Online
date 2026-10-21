# Enemy generation sync: the host's roster is the identity (rebuilt from the rejected pairing scheme)

- Status: Todo — Rejected (batch `20261021-e`: row 4 — no member live-adopts a runtime spawn; the row is
  adopted only by a later snapshot, and the second member never received it at all; rows 1, 2, 3, 5 and 6
  pass. Record: `docs/evidence/acceptance/enemy-generation-pairing-late-join-20261021-e.md`)
- Priority: High
- Category: Enemy sync / generation roster / rebuild
- Source: batch `20261006-g`'s third client (2026-10-06) — a member that joined the lobby and entered the
  world AFTER the run had started logs the pairing failure once per 60 s cycle for the whole session while
  the member that was present at world entry reads the mapping as established in the same cycles. The
  rejection of 2026-10-20 widens the trigger: members present at entry fail too, so the defect is not
  late-join-only.
- Related: `done/enemy-runtime-spawn-classification.md`, `done/enemy-snapshot-binding-recovery.md`,
  `done/layer-change-member-dropout.md`, `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemyRosterAdoption.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyRosterBinder.cs`,
  `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md`,
  `docs/evidence/sync-coverage-matrix.md` row N1,
  `docs/evidence/acceptance/enemy-generation-pairing-late-join-20261021-e.md`

## Why the old scheme was rejected

The rejected scheme used a POSITION as an identity: the host recorded where it first bound an animal
("the spawn anchor"), the member sorted its own frozen copies the same way, and the two ordered lists were
paired index-by-index inside a 0.5-unit tolerance, all-or-nothing over the whole set. That premise fails in
three independent ways, and batch `20261025-d` observed the first one with both members present at entry:

- **The generation does not have to be identical.** Enemies and traps share the game's entity-distribution
  path, whose placements are physics raycasts — outside the random-stream isolation. The trap layout
  already treats the host's live scene as the authority for exactly this reason.
- **The host's first-bind position is not the generation position** whenever the world ran for a while
  before the session started (the batch's 17.021-unit case): the anchor is simply where the animal stood
  when the host first looked.
- **The verdict is whole-set and has no repair.** One pair outside tolerance failed all 85 enemies on every
  60 s cycle, left the member's whole generated set local, and gave the player a different world (the
  reported `shadecrawler`).

## Rebuild decisions (user, 2026-10-21)

The user chose the four options the proposal put to them:

1. **Who owns the roster — the host.** The host's enemy table is the single roster; the member adopts its
   local copies against it instead of reproducing the host's generation.
2. **An unmatched copy — converge per copy.** Reuse the local copy a row plausibly is; materialize the row
   when no copy can be reused; destroy local copies the roster does not contain. No permanent local-only
   enemy.
3. **One identity — the host id, one table.** Generated and runtime-spawned enemies are rows of the same
   roster; the runtime creation key is a column for exact reuse/materialization, not a second matching
   scheme.
4. **Reporting — per enemy, recoverable.** Each row ends reused, materialized, destroyed or unresolved with
   its reason; an unresolved row is retried on the next snapshot and never blocks the others; "aligned"
   means every roster row is bound and no surplus local copy remains.

## Rebuild (landed 2026-10-21)

- **The host's roster is the kernel enemy table plus the snapshot's state.** Every captured animal gets a
  host id (generated and runtime-spawned alike); `EnemyKernelProjection` already carries id, prefab,
  runtime flag and health durably, and the member's kernel is the host's replay. The snapshot now carries
  one row per enemy — id, prefab, match hint and current state — stamped with the world/layer
  generation (`EnemySnapshotMsg.Generation`); the runtime rows' creation key and tint ride the
  runtime-spawn detail list. No new persistent table and no second identity scheme.
- **The snapshot is authoritative, including when it is empty.** The old `if (_enemies.Count == 0) return;`
  is gone: an empty roster makes the member destroy its surplus copies. A roster of another generation is
  refused before anything is applied. The host holds snapshots while its world is generating or absent
  (`MarkRosterReady`/`MarkRosterUnstable`), so a partial table can never tell a member to delete enemies its
  own generation has not produced yet.
- **The host's states come from the BOUND table, not a one-frame scene scan.** `CaptureHostEnemies` walks
  `EnemyBindingTable` and prunes only Unity-destroyed objects, so a live enemy that is inactive or out of
  the scan's reach keeps its id and its row — a scan-only state list would silently drop the id and the
  kernel projection would tombstone it.
- **The member adopts per row.** `EnemyRosterAdoption.Plan` is the pure judgment (creation key first, then
  nearest same-prefab copy within the reuse radius of the match hint; unmatched rows materialize; unclaimed
  copies are surplus; a row with no prefab is unresolved). `EnemyRosterBinder` applies it: reuse and freeze,
  materialize at the host's current position with the creation key and tint, destroy surplus. Only a
  COMPLETE roster destroys unclaimed copies — an unresolved row may be one of them.
- **Bindings the roster no longer contains are dropped.** A bound id absent from the snapshot is a removal
  the member missed (the snapshot is the removal's fallback) and is destroyed; a bound copy whose Unity
  object is gone is pruned before the adoption.
- **Runtime spawns use the same identity.** The 20 Hz stream announces a new id; the kernel fact names its
  prefab; `TryAdoptNewRows` binds the local copy the creation channel already made (nearest same-prefab
  inside the reuse radius). If that pass cannot bind it, the next snapshot reuses or materializes the row.
- **The layer boundary resets the roster on both roles.** `GameAdapterBridge.OnWorldGenerate` calls
  `EnemySyncCoordinator.OnWorldGenerationStarted` (bindings, hints, sets and flags cleared; the host's
  roster marked unstable); the host's committed `EnemiesResetEvent` clears the member's buffer and the
  adapter's bindings; the generation stamp refuses a stale old-layer snapshot.
- **Deleted:** `EnemySpawnArbitration` (the all-or-nothing sorted pairing), `EnemyRuntimeSpawnArbitration`'s
  positional passes, the empty-snapshot no-op, and the scan-only host state list. The pure adoption matrix
  replaces their test suites; a source-shape gate pins the adapter wiring and the absence of the old
  matching names.
- **The rejected scheme's landing (2026-10-07) is kept in git and in
  `docs/evidence/selfchecks/enemies/enemy-generation-pairing-late-join-selfcheck.md`.** It moved the key out
  of the caller into the arbitration; that fix was correct for the field it named and still rested on the
  position-as-identity premise this rebuild removes.
- **Self-check:** `docs/evidence/selfchecks/enemies/enemy-roster-adoption-selfcheck.md` — the mechanism
  inventory, the pure/service/source-shape evidence and the limits this batch must judge.

## Rejection (batch `20261025-d`, 2026-10-20)

Three clients in one lobby, all three in the world from the run's start: the member and the third client
joined the lobby before the host ran `start-run`, and both were `inWorld` when the run began. On the
deployed artifact `0.1.0+426f71be…`, both guests logged, on every 60 s cycle (23:17:11 and 23:18:11):

```text
[WRN] [Enemy] generation spawn pairing failed (85 host vs 85 guest generated enemies, first key mismatch at index 52: 17.021 world units apart, tolerance 0.5) — generated copies stay local (generation divergence); runtime spawns are still bound.
[INF] [Enemy] snapshot applied: 0 generated bound, 0 runtime spawns, mapping=False.
```

The host sent both members `[Enemy] snapshot sent to <member>: 85 enemies, 0 runtime spawns.` in the same
cycles. The two guests' warnings are identical — same index, same distance — so the divergence is between
this host's anchor set and both members' regenerated set, not a per-member or late-join artifact.

What the player sees, reported by the user during the batch: the host's screen carries a `shadecrawler`
(a generation-distributed enemy) that neither guest can see. Each guest renders its own local copy of the
generated set; the host's copy is not the one the guests show.

What this rules out:

- **Not a count mismatch**: 85 host vs 85 guest on both clients, and the host's snapshot says 85.
- **Not the runtime-spawn half**: 0 runtime spawns on both sides.
- **Not late-join-only**: both members were present at world entry.
- **Not the repair being skipped**: the failure repeats on consecutive 60 s cycles.

Rows against the rejected scheme's acceptance table: row 1 failed (the member present at world entry read
`mapping=False`), row 5 failed (the same member is the regression row), rows 2–4 were not driven. The
batch's own subject was the craft ticket; this reading is the rejection's evidence.

## Acceptance

Read on the deployed artifact, three clients, one lobby, and judged on each member's own window. The batch
must include a member present at world entry and a member that joins mid-run; a row that needs a layer with
no enemies may be judged from the tests if the session cannot produce one, and is then named as a residual.

1. Both members (present at entry) read `[Enemy] roster adopted: N rows (… aligned=True)` with N equal to
   the host's row count, zero unresolved and zero surplus destroyed, on the entry snapshot; no pairing
   failure line exists any more.
2. The batch's exact drift shape: the host's world ran before the session (an animal moved > 0.5 units
   before the host first bound it). The member's row is reused or materialized at the host's position,
   `aligned=True`, and the member does not keep a local-only set for the session.
3. A member that joins mid-run after the host's animals moved: within its entry snapshot it reads
   `aligned=True` with the host's rows, sees the same enemies at the host's positions, and a killed/removed
   enemy disappears from all three views.
4. Runtime spawns: the host triggers a runtime animal after a member is in the world; the member logs
   `live-adopted` for it within one stream pass, both members see it at the host's position, and it follows
   the host's state stream.
5. Layer descent: all three descend; the new layer's rows adopt (`aligned=True`, zero unresolved), the old
   layer's bindings are dropped, and neither member keeps an old-layer enemy.
6. Regression: damage, kills, removals, attack announcements and bites still reach the bound copies; the
   host's screen carries no new warning; the member's 20 Hz drive still moves the bound copies.

## Limits

- The adapter is Unity wiring: the test assembly references the GameAdapter with `ExcludeAssets="compile"`,
  so the adoption's application (reuse/materialize/destroy, the freeze, the layer reset) is judged by the
  batch and pinned by the source-shape gate; the judgment itself is pure-tested.
- A generated enemy materialized through `RuntimeEntityFactory` is created by the runtime-creation path, not
  by the generator: presentation, damage and drop ownership are judged by the batch, not by a unit test.
- The match hint is a reuse heuristic. A member's copy outside the reuse radius is replaced (a one-frame pop)
  rather than bound far away; same-prefab copies are interchangeable, so a nearest choice can only cost that
  pop, never a wrong identity.
- A row whose prefab has not reached the member (the kernel fact lags) is unresolved and retried; the
  adoption's surplus destruction is deferred while any row is unresolved.
- The 60 s repair is still the removal/materialization fallback for a runtime spawn whose live bind failed;
  the live pass normally binds it within one 20 Hz batch.
- The batch cannot force a generation divergence deliberately; the drift row depends on the host's world
  having run before the session. If it cannot be staged, it is a residual, not a pass.

## Review (2026-10-21)

Independent adversarial review on the uncommitted tree (HEAD `4d209f10`): 1 blocker, 9 majors, 7 minors.
Every blocker/major is fixed in the same commit; the pure `Plan` survived the reviewer's adversarial
inputs. The dispositions:

- **B1 (blocker)** — destruction was gated on `plan.Unresolved` but not on a row whose materialization
  failed. Fixed: the gate is `plan.Unresolved.Count + failed`, both counted, the damage held; the
  source-shape gate pins the combined condition.
- **M1** — a 20 Hz frame rebuilt the buffer and dropped prefab/key/tint. Fixed: `Merge` preserves the
  snapshot-only fields; `EnemyStateMsg` now carries `PrefabId` per row; a snapshot-then-stream test pins it.
- **M2** — the adoption was not guarded across the regeneration fade and the live pass had no guard.
  Fixed: `CanPublishRoster(hasWorld, generating, regenerating)` gates the host's readiness, the snapshot
  deferral, the pending apply and the live pass; the pure theory covers the fade.
- **M3** — a held entry snapshot was never flushed at the ready edge and the hold was Debug-only. Fixed:
  `EnemyRosterPublisher` records the held peers, flushes them at the first ready transition and logs at
  Information; a simulation test pins the hold and the flush.
- **M4** — the docs claimed a per-row creation key the wire did not carry (and a prefab only from the
  kernel). Fixed: `EnemyStateMsg` carries the prefab; the five documents now say the runtime creation key
  and tint ride the runtime-spawn detail list.
- **M5** — both internals pages taught the rejected pairing. Fixed: the absolute-snapshot and
  runtime-spawn sections rewritten on both sides; the alignment pair re-recorded.
- **M6** — the safety properties had only substring pins. Fixed: the hold/flush and snapshot-field
  preservation are simulation-tested; the live rule is the pure `FindLiveCopy` with its own matrix; the
  destroy gate is pinned as the combined condition; the gate renamed to what it asserts.
- **M7** — the checklist's build row was empty and its structure row had false numbers/claims. Fixed:
  corrected (the HEAD coordinator was 587; the rebuild reached 730 and was split into 413 + 96 + 350; the
  two new booleans are named with their readers), the build row filled from the final run, the review
  dispositions recorded here.
- **M8** — the selfcheck's RED citation pointed at a deleted `## Rejection`. Fixed: the batch's quoted
  rejection is restored above.
- **M9** — the live pass ignored the creation key. Fixed: `FindLiveCopy` binds only the keyed copy when
  the row's key is known, a generated row never takes a keyed copy, and a runtime row without a known key
  may bind provisionally; the snapshot's plan owns the final mapping.

Minors: `OnEnemyRemoved` forgets the presentation record; the stale doc comments (capture, presentation,
`Merge`, `NetMsg`, `WorldEntryFanout`, `RuntimeEntityMatch`) and the watchlist entry are updated; the
materialization failure names the enemy id; the held line is Information; `TryGetEnemyFact`'s unused
`runtimeSpawned` out is gone. Accepted as-is: the decision register's 267 row sits above 266 (the register
is not strictly numeric), the binder receives the coordinator's tracking sets by reference (the two halves
of one domain; a separate tracker is a follow-up if it grows), and the `RemoveBoundEnemiesNotInTheLayer`
null guard stays as defence after the prune. The review's residual risks — whether `Utils.Create` resolves
a generated enemy prefab, the Unity transition timing, and the three-client rows — are the batch's job.

## Rejection (batch `20261021-e`, 2026-10-21)

Three clients in two sessions (a world-entry session with both members present at entry, and a session whose
world had run 30 s solo before the lobby so both members joined mid-run) on `0.1.0+44ce057f…`. Rows 1, 2, 3,
5 and 6 pass; **row 4 fails**. Full evidence: `docs/evidence/acceptance/enemy-generation-pairing-late-join-20261021-e.md`.

What row 4 read (three runtime spawns, zero `live-adopted` lines in any of the three logs):

```text
[2026-10-21 19:20:18.015] [WRN] [Enemy] attack SpiderBite #1 arrived for unknown enemy 8DF2F650248BED1:78:0 — the snapshot binding may not have arrived yet; the attack is dropped.
[2026-10-21 19:20:21.035] [INF] [Enemy] roster adopted: 79 rows (78 kept, 1 reused, 0 built, 0 surplus destroyed, 0 stale bindings dropped, 0 unresolved) — aligned=True.
```

The host bound the summoned animal at 19:20:17.048; both members created their local copy at
19:20:17.030 and still refused the attacks as "unknown enemy"; the row was adopted only by the 60 s repair
snapshot at 19:20:21.035 (four seconds later, through the creation-key pass). A second summon repeated it.
In the mid-run session the alt adopted a summon through an entry repair at +5 s while the **guest never
received it at all** (its table stayed at 76 rows after the summon and 75 after the kill, while the host and
alt held the runtime row).

The leading cause, from the code: `EnemyRosterBinder.TryAdoptNewRows` takes the prefab from the kernel fact
but `allowKeyedCopy` from the stream buffer row's `RuntimeSpawned`; `EnemyStreamWireMapper.ToWireEnemyStreamState`
carries no prefab/runtime/key, so a freshly announced id reads `RuntimeSpawned == false` and
`EnemyRosterAdoption.FindLiveCopy` refuses the keyed `RuntimeEntityCreation` copy the creation channel just
made. The kernel fact does carry `RuntimeSpawned: true` (read back from the guest). The second, independent
gap is the missing repair wave: the host's last in-session repair group for the guest is at 19:29:52.637,
so the snapshot fallback never reached it in the rest of the session. Rows 1/2/3/5/6 and the batch's own
shape (78-row entry adoption aligned, 76-row mid-run adoption with 4 materialized rows / 4 surplus copies
aligned, 71-row layer adoption aligned with the old bindings dropped, damage/removal/bite/drive regression)
are recorded there; the drift's per-animal pre-bind distance and the "no visual frame" reading are its
Limits.
