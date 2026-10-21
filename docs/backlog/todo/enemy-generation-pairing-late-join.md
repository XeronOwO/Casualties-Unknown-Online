# Enemy generation sync: the host's roster is the identity (rebuilt from the rejected pairing scheme)

- Status: Todo — Rejected (batch `20261021-f`: row 1 — on a 956-animal world the host's early capture
  during generation put 83 rows in the kernel, the world-entry layer reset then cleared both members'
  adoption candidates after their generation, and the entry adoption built the whole roster (946 built,
  10 trader rows unresolved, `aligned=False`) until the next repair. Row 4's live adoption and the
  late-join release / host-resume fixes pass. Record:
  `docs/evidence/acceptance/enemy-generation-pairing-late-join-20261021-f.md`)
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
  `docs/evidence/acceptance/enemy-generation-pairing-late-join-20261021-e.md`,
  `docs/evidence/acceptance/enemy-generation-pairing-late-join-20261021-f.md`

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

## Root cause and fix (2026-10-21, after batch `20261021-e`)

### Row 4's leading cause, confirmed

The live pass took half of its identity input from the wrong layer. `TryAdoptNewRows` read the prefab from
the durable kernel fact but the runtime marker from the 20 Hz buffer row, and the stream frame carries
presentation only (`EnemyStreamWireMapper.ToWireEnemyStreamState` sends position/velocity/rotation/health/
flags/presentation, and its `ApplyTo` leaves `PrefabId`/`RuntimeSpawned`/`CreationKey` at their defaults for
an id the buffer does not already hold). For the id the stream has just announced, the buffer row therefore
reads `RuntimeSpawned == false`, `FindLiveCopy` treats the row as generation output, refuses the keyed
creation-channel copy — the only copy that exists for it — and the row waits for the next snapshot. The
kernel fact for the same id reads `RuntimeSpawned: true` (the batch's `a-row4-guest-kernel.json`).

Fix: `EnemySyncService.TryGetEnemyFacts(id, out prefabId, out runtimeSpawned)` replaces
`TryGetEnemyPrefab`; both columns come from the same `EnemyState` row, and `TryAdoptNewRows` passes the
fact's marker into `FindLiveCopy` instead of the buffer row's. The fact's absence remains the retry case
(the next 20 Hz batch retries), and the snapshot's creation-key pass remains the exact backfill.

RED before the fix (recorded, `.acceptance/red-20261021-f.log`): the source-shape gate's new pin fails on
the old binder — *"the live pass must take BOTH of its identity inputs — the prefab and the runtime marker —
from the same kernel fact"* — and the gate now also refuses `state.RuntimeSpawned` in the binder. GREEN:
`EnemySyncServiceTests.KernelFact_SuppliesBothLiveRowIdentityFields` pins the fact's two columns together,
and `SourceShapeGateTests.EnemyRosterAdoption_IsTheOnlyMatch_AndTheRosterGatesArePinned` pins the binder's
source.

### The second gap was NOT the repair pump — it was the start gate

The batch read "the guest received no repair group after 19:29:52.637" as a second, independent gap. The
host's log shows the pump working: at **19:29:45.262 / 19:29:45.294** one 60 s cycle sent the in-session
repair group to BOTH members (`Sending the in-session repair group to <guest>` → `[Enemy] snapshot sent to
<guest>: 76 enemies, 0 runtime spawns, run 1 layer 0.`). It never sent again because it had nobody to send
to: at **19:29:58.292** the guest itself reported `Scene state: InMenu (PreGen)`, and at **19:30:04.259** the
alt did the same. The reason is on the guest, one line earlier:

```text
[2026-10-21 19:29:57.763] [WRN] [CasualtiesUnknownOnline.GameAdapter.Run.StartGateCoordinator] WorldReady never arrived within 60s — back to the menu.
```

Both members had been returned to the menu by their own start-gate valve. The host never released them
because the run had started BEFORE the session existed: the host started the run solo at 19:27:42 and created
the lobby from inside the running world at 19:28:45. `WorldStartGate.Arm()` is called only on the host's own
InWorld edge under the host role (`RunCoordinator.UpdateSceneState` → `_world.StartStartGate()`), so it never
ran for that run; `_released` stayed false for the whole session, and both answer paths
(`NotifyMemberInWorld`, `AnswerRepeatInWorld`) treat "no armed gate and not released" as "say nothing". The
guest re-asserted its InWorld report twelve times inside its 60 s budget and got only the entry-group marker,
never the release.

Fix: the gate's verdict for a member that is already in the world (`WorldStartGate.LateJoinerPasses`) is
"pass" when the gate was released OR when no gate was ever armed over this run and the host is already in
the world (`ISessionControl.LocalInWorld`). Both answer paths use it; the `_armed` check still comes first,
so a gate that IS armed over the run holds its members as before. `AnswerRepeatInWorld` therefore also
answers a swallowed release into an already-playing run.

RED before the fix (same log): `WorldStartGateTests.RunAlreadyPlayingWhenTheMemberArrived_PassesWithoutAnArmedGate`
(`Expected: 1, Actual: 0` — no release reached the guest) and
`SwallowedReleaseIntoAnAlreadyPlayingRun_IsAnsweredOnTheRepeat` (`simulation did not converge within 8000 ms`
— the repeat was never answered); `AnArmedGate_StillHoldsTheMemberUntilItsOwnRelease` passed before and after,
pinning that the armed path is unchanged. The acceptance record's "missing repair wave" wording is
corrected there.

## Review (2026-10-21, before the rerun)

Independent adversarial review of the uncommitted fix (fresh context, frozen tree, FULL tier): **no blocker**
in the change set; both behavioural claims were substantiated against the code, the tests and the batch's
logs. One major and four minors came back, all fixed in the same commit and pinned:

- **MAJOR (fixed here) — the host is left on the native loading screen when the lobby opens around its
  running solo world.** `StartGateCoordinator` keeps the screen while
  `SessionActive && !RunCoordinator.IsPlaying`; both `MarkPlayingForHost` call sites are unreachable for a
  run whose world entry happened with no session (the world-entry branch requires the host role) and whose
  gate never arms (the gate-release branch requires `_gateFrozen`). The `SessionActivated` host branch cannot
  fire either: a host-created lobby sets `SessionActive = true` in `OnLobbyCreated`, so `HandshakeHandler`'s
  `!wasActive` guard never calls `FireSessionActivated`. The batch's own session-B host log shows the
  consequence — `[2026-10-21 19:28:45.502] [INF] … StartGateCoordinator [Gate] keeping the loading screen
  up while waiting for the host.` — and no line ever releases it: the fix for the members alone would have
  had them enter a world their host cannot see. Fixed with
  `StartGateCoordinator.ResumeHostPlayingIfTheSessionRoseAroundTheRun` (host, session active, in the
  world, not yet playing, no armed gate → `RunCoordinator.MarkPlayingForHost()`), pinned by
  `SourceShapeGateTests.RunLifecycle_ResumesAHostWhoseSessionRoseAroundARunningWorld`. The batch's session-B
  host log is the runtime proof to read on the rerun (`the session rose around an already-running world`,
  no `keeping the loading screen up` line).
- **MINOR (fixed here) — the gate's flags were not reset per run.** `_released` was only cleared in `Arm()`,
  which runs at the host's own world entry, so a second run in one session began with the previous run's
  release until then, and a member whose world came up first could be passed by the old run's verdict.
  `SetHostRunPending(true)` (the host's click) now clears `_released` and `_armed`; pinned by
  `WorldStartGateTests.ANewRun_ForgetsThePreviousRunsRelease`.
- **MINOR (fixed here) — the new source-shape pin was weaker than its message.** It asserted the
  `TryGetEnemyFacts` call and the absence of `state.RuntimeSpawned`, but not that the marker reaches
  `FindLiveCopy`. The pin now includes the call with `runtimeSpawned` as the argument, and the service
  accessor's two-column signature.
- **MINOR (fixed here) — a stale count.** The selfcheck's `66/66` for the older focused filter is 67/67 on
  this tree (the new service test); the selfcheck is re-measured at the cycle's final run.
- **MINOR (fixed here) — a log line stated the opposite of its premise.** `AnswerRepeatInWorld` said "the
  gate is released" while the new branch answers exactly when it is not; reworded to "the member passes: the
  gate is released, or this run never armed one".
- **Nits (fixed here):** the ticket's trailing blank line; the batch's start-run time is 19:27:42 (19:27:40 is
  the client start); the internals pages no longer claim the stream always announces a runtime row before
  any snapshot; the new test fixture is documented as reproducing the gate's flags, not the production flow.
- **Accepted as-is:** the guest-side `EnemyEntity.RuntimeSpawned` now has no reader on the guest (it is
  preserved in `Merge` and projected by the kernel restore, and the host publishes from it — the buffer
  mirrors the row); the live pass may still bind a keyless same-prefab copy provisionally for a runtime row
  whose creation key is not yet known — the Limits now state its cost precisely (a destroy of the
  creation-channel copy plus a materialization of the generated row's replacement, same prefab, corrected
  by the snapshot; identity stays by row).

## Limits added by the fix (2026-10-21)

- The live pass binds a runtime row whose creation key the member has not seen yet provisionally, by
  nearest same-prefab copy inside the reuse radius. If that copy is a keyless generated copy, the next
  snapshot's plan destroys the creation-channel copy and materializes the generated row's replacement —
  a same-prefab swap with a one-frame pop, never a wrong identity (both rows stay bound, and damage,
  removal and attacks follow the host id).
- A runtime row whose fact has not reached the member is retried on the next 20 Hz batch; the fact's
  absence is not a judgment.
- The host-resume pump is Unity wiring: its proof is the batch's host log (the line and the keeper's
  absence), not a unit test.

## Rejection (batch `20261021-f`, 2026-10-21)

The row-4 fix and the late-join fixes were re-run on `0.1.0+ecec0d5b…`, two sessions, three clients
(session A entry, session B drift). **Rows 2, 3, 4 and 5 pass; row 1 fails; row 6's bite/announcement half
was not driven.** Full evidence: `docs/evidence/acceptance/enemy-generation-pairing-late-join-20261021-f.md`.

- **Row 4 passes, both sessions.** The host's summon binds in the same frame, and both members log
  `live-adopted` within 160–575 ms — session A `…:956:0` (guest 20:25:26.085 / alt 20:25:26.080 after a
  20:25:25.506 host bind), session B `…:88:0` (20:29:26.299/.307 after 20:29:26.143). The three scene
  copies then read the same moving position and health on all three clients, the tables agree, the 60 s
  repair carries `1 runtime spawns`, and no `unknown enemy` line exists on either member in either
  session. The leading cause of the `20261021-e` rejection is fixed.
- **The second gap is fixed and verified.** Session B reproduced the `20261021-e` shape exactly (a solo
  world opened to friends). The host logged `The session rose around an already-running world — the host
  resumes playing.` at 20:28:44.645 and **no** loading-screen keep line; both members were released with
  `Start gate pass — … enters directly (game already running)`, and both stayed `inWorld` past their
  60 s valve (zero `WorldReady never arrived` and zero `InMenu` lines; `inWorld=true` at 20:30:10). The
  repair pump then found them and sent the roster.
- **Row 1 fails on a dense world.** Session A's world generated 956 animals and 10 traders. The host's
  capture ran during generation (`host enemy baseline established over 0 animals`, 20:22:33.154), so 83
  rows existed when the world-entry layer reset fired (`dropped the previous layer's 83 enemy row(s)`,
  20:22:43.718). Both members applied that reset **after** their own generation (guest froze 374 copies
  at 20:22:40.254; reset at 20:22:45.152), which cleared their tracked animal set; the entry adoption then
  had no candidate to reuse and built the roster: `956 rows (0 kept, 0 reused, 946 built, 0 surplus
  destroyed, 0 stale bindings dropped, 10 unresolved) — aligned=False.` at 20:22:45.409, with ten
  `roster row … (prefab trader) could not be materialized` warnings. The ten traders were live-adopted
  from the re-scanned scene at 20:22:49.015, and the entry repair at 20:23:47.942 destroyed the 946 surplus
  and adopted `aligned=True`; the member held 1902 scene copies for about 2.5 s in between.
- **A third finding in the same run.** The host's snapshot reported `291 runtime spawns` at 20:22:43.85 and
  `873 runtime spawns` at 20:22:46.64 for a roster of generation animals: `IsRuntimeSpawn(sessionActive,
  !generating)` classifies every animal whose `Start` runs after the generation flag flips as a runtime
  spawn, and a dense world's generator leaves hundreds of `Start` calls to run after the flip. The
  snapshot's runtime detail list is therefore wrong at this scale (no functional consequence observed in
  this batch, but it is the same rule the live pass trusts).

### Next cycle (in order)

1. **Stop the early capture.** `EnemySyncCoordinator.CaptureHostEnemies` must not bind while the world is
   generating (the same settled-world gate the publication uses): then a run's first layer has no kernel
   rows for the entry reset to drop, and the members' candidates survive. This is the root cause of row 1.
2. **Rebuild the adoption candidates from the live scene.** `AdoptRoster`'s copy list should union the
   tracked set with a scene scan (the host already does), so a roster reset after generation cannot empty
   it — the defence that makes the row-1 shape harmless even if a reset still fires.
3. **Fix the runtime-spawn classification for deferred `Start`s.** The rule needs a "created by the
   generation" fact (a marker stamped at the generation's instantiation path, or the set that existed at
   the generation-finished edge), not the `generating` flag read at `Start`.
4. Re-run this ticket's six rows on a dense world (the `20261021-f` session-A shape) **and** the drift
   shape; row 6's bite/announcement half needs a spider in range of the host.