# S3 — Mid-run consistent cut and world diff

- Status: Todo (unblocked: S1 and S2 landed; approved for implementation by the user on 2026-09-10).
  Staging: S3.1 (payload skeleton) and S3.2 (world-diff capture and replay) landed; S3.2's
  independent adversarial pass then produced 1 blocker + 3 majors + 4 minors + 3 nits, all fixed on
  top of it (the replay was extracted into the Runtime so its order/caps are testable, the archive's
  game-table rows are the only ones written back into the game's own list, the adapter's pending
  handover has a real cancel path, the layer-boundary reset is split, and a restored row carries its
  already-settled support-loss verdict). A second independent pass then caught the retry path itself
  (a taken handover could not be retried): the replay now READS the native handover and commits it
  only after every row reached the live world, keeps both halves pending on any refusal, and a layer
  boundary cancels a handover that belongs to the layer being replaced.
  **S3.3 (the consistent cut and the transient policy) landed 2026-09-11**: the frame-end pump seam
  with the armed-cut trigger (`/save` on the host console, the menu return upgraded to a full
  mid-run cut at the same seam), the per-row transient policy table, the bounded
  resolve-before-save deferral, the restore-report completeness work of scope 6, and the cut
  writer's split out of `WorldSaveService`. **S3.4a (the run-level native fields) landed
  2026-09-11**: the two rarity multipliers now ride the kernel run baseline (and therefore the wire,
  which is what a side that GENERATES the layer reads them from) and a cut stamps the cut instant's
  value on them; the run clock base and the recipe unlock table are `run.json`'s new
  `native-run-fields` row, written back by the adapter at the slot the native
  `SaveSystem.TryLoadGame` used to occupy, and a reader that cannot read them refuses the cut.
  **S3.4b (the character-level native fields — `lastHappiness`, `caloriesConsumed`,
  `WoundView.cInfo`) landed 2026-09-11** on the local restore apply seam decision 170 had just put in
  place: the three fields ride `CharacterDataMsg.NativeFields` (read off the live scene at the cut and
  at each 1 Hz report, all-or-nothing), are written back by the restore path's second pass, and a
  snapshot that carries none of them is named as damage in the restore report (decision 171); the same
  cycle split the restore's write half out of `CharacterDataSync`
  (`review/save-native-character-field-parity.md`). An independent adversarial pass on S3.4b then found
  one blocker (the interaction services' `CloneCharacter` dropped the three fields from the snapshot it
  saves over the stored character), two majors (a 1–9 element happiness row was prefix-written into the
  ten-slot game window; the restore report both under-reported malformed fields and blamed characters
  no peer claimed) and minor/nit items — all fixed in the same cycle. S3.5 (exactly-once plus
  documentation and
  re-anchoring) and scopes 7-9 stay open; the mid-run trigger is OPEN, so a build produces both the S2
  layer-end cut and the frame-end mid-run cut. **2026-09-11 (before S3.4b started)**: the S2 continue path was found not to
  apply the host's own restored character to its own body at all; that gap is fixed first and
  independently (`review/save-layer-end-save-and-restore.md` → *In-game gap found while scoping
  S3.4b*, decision 170), which is also the seam S3.4b's three fields will write back through —
  `CharacterDataSync`'s local restore apply.
  **S3.5 increment (2026-09-12) — scope 8 (the host side of a restored world-entity table) and F3
  (a layer-end cut carries no world-rooted item row) landed.** The restored per-entity facts now
  reach the host's own regenerated world: `WorldEntityKernelProjection` applies them immediately on a
  GUEST and HOLDS them for the world-entry seam on the host/solo side (`IRestoredWorldEntitySource`),
  `RestoredWorldFactReplay` writes them through the same three appliers the guest path uses (each
  returning what the live world took, so an entity the regenerated layer does not have reaches the
  restore report instead of the log alone), and a layer-end cut's rows are dropped before the audit
  begins — the layer they describe is the one being replaced. A mid-run restore therefore owes THREE
  live-world halves and a layer-end restore ONE (`WorldRestoreApplier.LiveWorldHalves`, pinned by
  `WorldRestoreAuditTests`), and `items.json` obeys the same "no in-layer fact" rule as the two
  world-fact files, with the layer-boundary reset's own subtree rule
  (`ItemLocationChain.IsWorldRooted`); `world-entities.json` obeys the same rule, so a produced
  layer-end archive holds no in-layer fact at all. Scope 7 and scope 9 are NOT part of this
  increment; scope 9 moved to its own stage ticket (S3.6).
- Priority: High
- Category: Persistence / save system
- Source: Stage 3 of `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md`; this is the user's hard requirement — "需要重点关注存档的中途性质，防止出现多生成、少生成内容的情况"
- Related: `docs/architecture/save-archive-format.md` §4/§6 (S3.2 also recorded the restore apply seam in §6.1), `todo/save-layer-end-save-and-restore.md` (S2), `todo/save-multiplayer-restore-and-backups.md` (S4)

## Approved decisions (2026-09-10)

Frozen with the user before implementation started:

1. **Cut trigger** — a mid-run cut is taken from the existing in-game command console (`/save`, host
   only) plus the host's deliberate menu return, which is upgraded from S2's layer-end-class cut to a
   full mid-run cut (every world object is still alive at that moment, so the diff is available). The
   cut itself runs on the host main-thread pump, never inside the console callback: a command batch
   must not interleave with the cut.
2. **Staging** — S3.1 payload skeleton (typed rows, encoder/decoder, the Runtime fact ports), S3.2
   world-diff capture and replay in the adapter, S3.3 the consistent cut and the per-state transient
   policy, S3.4 the native run-field ownership (the frozen per-field decision lives in
   `todo/save-native-run-field-parity.md`), S3.5 exactly-once plus documentation and re-anchoring.
3. **Native run fields** — decided per field in `todo/save-native-run-field-parity.md`; a field
   deliberately left out is still named in the restore report, never silently defaulted.
4. **Random streams** — the world's generation baseline already rides `RunState.RandomState` in
   `run.json`, and no kernel domain makes a random decision, so `GameCheckpoint.RandomStreams` stays
   without a producer in S3 and the encoder's refusal guard stays until a domain actually owns a
   stream. Keypad codes and geyser liquid types are captured as DECIDED values (world-transients),
   never re-rolled.

## S3.3 independent adversarial pass (2026-09-11)

An independent reviewer (fresh context, no stake in the change) audited the S3.3 commit against the
six claims it makes. It could NOT falsify: the CUO-side seam quiescence (no Runtime service runs
after the adapter pump, `ItemKernelAuthority` commits synchronously, every flush runs before the
seam), the trigger/deferral state machine, the "unreadable native table is not an empty table" rule,
the restore-audit chain, and the structure gates. It DID find three real defects, all fixed on top
of the stage before this ticket moved on:

1. **Four `drop-with-log` rows had no observer** (craft batches, item physics, the run clock, the
   earthquake timers), so "everything the cut does not carry is named" was false for them. Fixed by
   making observability part of the policy row (`WorldTransientDetection.Observed` / `Standing`): an
   observed row is named with its count, a standing row is named as a class every mid-run cut leaves
   behind, and a contract test pins which rows are standing. The rows were NOT given fake counters —
   the count is only claimed where a CUO owner can actually see it.
2. **`/save` was unreachable in solo play** (the console's `HostOnly` gate; solo has no session
   role). Fixed: the command is open to anyone and the SAVE LAYER owns the authority rule (a guest
   never writes a world archive), which is the same predicate the rest of the save system uses.
   Remaining gap, recorded below as scope 9: solo play has no menu-return trigger (the teardown hook
   is session-driven).
3. **A throwing engine call during a restored cut's live-world write** produced no report and left
   both handovers armed, so the next generation could receive the previous layer's rows. Fixed:
   `RestoredWorldFactReplay` reports the loss to the audit and releases both handovers; the
   "restore carried nothing to write" case now completes the audit instead of leaving it armed.

Smaller findings, also handled in the same pass: the seam's wording no longer claims Unity's frame
boundary (the guarantee is the single synchronous read); the menu return's "cut refused → leave
anyway, previous snapshot intact" behaviour is documented; a composition with no native reader now
names what it cannot carry in the cut report (not only the log); the `/save` answer no longer
promises "this frame" when a deferral can wait.

## S3.4a self-check (2026-09-11)

| mechanism | change | evidence |
|---|---|---|
| `WorldGeneration.lootRarityMultiplier` / `.trapRarityMultiplier` (per-layer accumulation, `WorldGeneration.cs:1061-1062`) | captured at the generation boundary and carried in the run baseline (`RunState` → `WireRunState` → `WorldStartParams`); a cut STAMPS the cut instant's value on `run.json`'s `run` row | `WorldRunFieldTests.MidRunCut_WritesTheRunBaselineAndTheNativeRunFields`, `WorldRunStateProjectionTests.RunBaseline_CarriesTheRarityMultipliersThroughTheWireAndBack` |
| the same values on a peer that GENERATES the layer | guest applies them with the rest of the world params (`WorldParamsService.Apply`) | `WorldRunStateProjectionTests.RunBaseline_CarriesTheRarityMultipliersThroughTheWireAndBack`; the in-game two-side generation match stays a dual-client check |
| `SaveSystem.savedRunTime + world.realTimeElapsed` (`SaveSystem.cs:165`) | cut-instant read, `run.json`'s `native-run-fields` row, written back at the slot the native load occupied | `WorldRunFieldTests.Continue_HandsTheRestoredRunFieldsToTheNativeApplier`, `Decode_NativeRunFieldsRow_RoundTripsTheClockAndTheUnlocks` |
| `Recipes.recipes[].hasMadeBefore` / `.INT` (`SaveSystem.cs:151-157`, `:442-447`) | same row, one entry per recipe, applied by INDEX at the WORLD-ENTRY seam (the game rebuilds the table in `WorldGeneration.Awake` and CUO appends the custom recipes on a later Update frame, so the save slot would see only the vanilla table) | `RestoredWorldFactReplayTests.ApplyIfPending_WritesTheCutInTheLoadBearingOrder`, `ApplyIfPending_RecipeRowsTheWorldCannotTake_ReachTheRestoreAccount` |
| a native read that cannot see a world | REFUSES the cut instead of writing zeros/empties | `WorldRunFieldTests.UnreadableRunFields_RefuseTheCut` |
| an archive written before the row existed | restores with the live clock/recipe state and NAMES the gap | `WorldRunFieldTests.Continue_WithoutTheNativeRow_NamesTheGapInsteadOfWritingDefaults`, `Continue_WithoutANativeApplier_NamesTheRunFieldsItCouldNotRestore`, `WorldSnapshotCodecTests.Decode_MalformedNativeRunFieldsRow_IsSkippedWhileTheBaselineApplies` |
| a layer-end cut | carries the run fields, still writes the two empty in-layer files, and never reads the keypad table | `WorldRunFieldTests.LayerEndCut_CarriesTheRunFieldsAndStampsTheBaseline` |

**Not proven by the above**: the in-game result (the layer's loot/trap distribution actually
matching, the recipe list actually staying unlocked, the clock display continuing) — those are the
user's dual-client pass. `WoundView.SetCharDetails`/`cInfo` and the two character-level fields are
S3.4b and are NOT implemented; see `todo/save-native-run-field-parity.md`.

## S3.4a independent adversarial pass (2026-09-11)

An independent reviewer (fresh context, no stake in the change) audited the implementation against
its eight claims by reading the code and the decompiled game. It could NOT falsify: the multiplier
capture point and the guest's pre-generation application, the clock base's read/write pair against
the native semantics, the layer-end cut's narrow native read (no keypad roll), the write-back slot
(`Awake` assigns the world before `Start`), the pending/cancel lifecycle, the "no silent loss" rule
end to end, and the salvage/format changes. It DID find real defects, all fixed before this stage
moved on:

1. **The recipe unlock table was written at the wrong seam** (blocker-class for the recipe half): the
   game REBUILDS `Recipes.recipes` in `WorldGeneration.Awake` and CUO's mod-content provider appends
   the custom recipes on a LATER Update frame, so the save slot saw only the vanilla table and would
   have refused every custom recipe's row — silently, in the log alone. Fixed by moving the recipe
   rows to the world-entry seam (`NativeWorldFactRestore.Recipes` + `IRestoredWorldFactSink.
   ApplyRecipeUnlocks`), where the table is complete, and by letting their refusals reach the restore
   account (`RestoredWorldFactReplay`). New evidence:
   `RestoredWorldFactReplayTests.ApplyIfPending_RecipeRowsTheWorldCannotTake_ReachTheRestoreAccount`.
2. **Stamping the cut instant's multipliers onto the baseline was wrong** for a cut taken while the
   game has already accumulated the NEXT layer's values (the window between
   `WorldGeneration.cs:1061-1062` and the kernel's layer advance spans `Clear()`): the snapshot would
   have rebuilt the named layer with the next layer's loot/trap density. Fixed by keeping the
   baseline's own generation-boundary capture (which IS the value the named layer was generated with)
   and WARNING when the live world disagrees. New evidence:
   `WorldRunFieldTests.MidRunCut_WhileTheWorldIsAlreadyOnTheNextLayer_KeepsTheBaselineMultipliers`.
3. Smaller findings, fixed in the same pass: `TryBeginRun`'s cancel now runs before its early returns
   (a failed folder creation could leave a previous restore armed), a half-pair of multipliers is no
   longer completed with a guessed `1f`, the adapter write is guarded so a throw cannot abort world
   generation, and a `run.json` row without a kind now says "this is the pre-typed-row archive"
   instead of "an unusable kind ''". Recorded but NOT fixed (they are the same family, outside this
   stage): the run clock is not on the wire, `layerTimeSpent`/`maxTimePerLayer` are not carried, no
   value-range guard on wire multipliers, and `WorldParamsService` injects the concrete adapter type.
   See `todo/save-native-run-field-parity.md` → "Recorded gaps".

## Scope

The consistent cut and the full mid-run payload. This is where the hard part of the requirement lives.

1. **The cut seam** — one point on the host main-thread pump where the kernel revision, every domain
   table and the native world tables are read at one instant, with no command batch or frame flush
   interleaved. The manifest's `cutPhase` names the phase; the format doc §4 lists the phases.
   **Landed (S3.3):** the seam is the Game Adapter pump's last step (`SaveCutSeam`), the manifest
   phase is `frame-end`, and every trigger (the console's `/save`, the deliberate menu return) ARMS
   a cut that the seam takes — a cut can no longer be taken from inside a console callback. The
   layer-end cut keeps its own seam (the kernel's layer-advance commit, phase `layer-boundary`), and
   `TryRequestCut` refuses to arm a layer-end cut at the frame-end seam.
2. **Payload completion** — `world-blocks.json` (host block difference table
   `WorldStateMessageService._damagedBlocks` + the native `WorldGeneration.blockDamages` list) and
   `world-transients.json` (the explicitly chosen transient set), on top of the S2 domain files.
   The native list carries its own kind (`native-block-damage`) because only the adapter can read and
   write it. The CUO side of that kind is GONE as of the block-damage capacity decision: CUO's
   `BlockDamageRegistry` was deleted rather than aligned, so there is exactly one partial-damage
   table and no routing decision to make — the Runtime half of both files landed in S3.1, and the
   `block-damage` kind it wrote no longer exists.
3. **Transient policy — one explicit verdict per in-flight state, no silent loss.** Each row below
   gets `capture` / `resolve-before-save` / `drop-with-log`, proven by a test:

   | In-flight state | Owner |
   |---|---|
   | Block-break pending drops | `BlockBreakPendingState` |
   | Trap drop hold | `TrapDropPendingState` |
   | Pickup queue (500 ms hold) | `PendingPickupQueue` |
   | Drop flush | `DropPendingState` |
   | Medical / shrapnel / other-medical sessions | `MedicalOperationSessionService`, `ShrapnelOperationSessionService`, `OtherMedicalOperationSessionService` |
   | Craft batches | craft batch owner |
   | Deferred entity-creation reports | runtime entity creation path |
   | Item physics transients (velocity / rotation / angular velocity / fresh) | item motion path |
   | Keypad codes, geyser liquid types, radiation line, world time, earthquake timers | host world tables |

   Silent loss is forbidden; a dropped transient must be logged with what was dropped and why, and the
   player must be able to tell (see the format doc §6 reporting rule).

   **Landed (S3.3)** as a real table, not a paragraph: `WorldTransientPolicy` declares one row per
   class with its verdict, owner, reason and OBSERVABILITY (13 rows covering this table verbatim),
   and a contract test pins the keys, the per-row verdicts and which rows no observer can count. The
   verdicts are: `resolve-before-save` for the three frame windows (block-break pending, trap drop
   hold, drop flush) — the cut is DEFERRED with the request still armed, bounded by
   `WorldSaveService.MaxCutDeferralFrames` (eight frames), and a state that outlasts the deadline is
   named in the report; `capture` for the decided native values and the radiation line (they ride
   `world-transients.json`); `drop-with-log` for every other row. A `drop-with-log` row that a CUO
   owner counts (the pickup queue, the three operation-session families, the deferred creation
   reports) is named WITH its count; the four rows the game owns (the craft coroutine, item physics,
   the run clock, the earthquake timers) are marked `Standing` and named as classes every mid-run cut
   leaves behind, because claiming a count CUO cannot see would be the silent loss this rule exists
   to prevent. An owner that reports an undeclared class REFUSES the cut. The Runtime half of the
   observation is `WorldCutTransientProbe` (a read-only query over the services that own the state),
   the adapter half is `SaveCutSeam.LiveTransients()`, and the verdicts are applied in one place
   (`WorldSaveService.TryCollectTransients` + the pure `WorldCutTransients`).
4. **Determinism inputs** — populate `GameCheckpoint.RandomStreams` in production
   (`GameStateStore.CreateCheckpoint` passes `null` today) if any domain's restore decision depends on
   them, and decide the same for keypad codes and geyser rolls; the save must carry enough baseline to
   reproduce the world.

   **Decision re-verified (S3.3):** no kernel domain consumes `RandomStreams` (the only producers of
   a checkpoint are the kernel's own `CreateCheckpoint` callers, and no domain contributes a stream),
   so the field stays without a producer and the encoder's refusal guard stays — a non-empty stream
   set still REFUSES the cut rather than writing a snapshot with no file to restore it from. Keypad
   codes and geyser liquid types are captured as DECIDED values (S3.2's `world-transients.json`
   rows), never re-rolled, which is what makes them independent of any stream.
5. **Exactly-once restore** — same-id dedup, no re-materialization of generation-time content, container
   children with exactly one parent, terminal facts never resurrected, load-twice idempotence.
6. **Restore-report completeness** (found by S3.1's review rounds; WIDENED by the block-damage
   capacity change) — the world-block tables are BOUNDED: the block-diff table at 65536 cells, whose
   refusals DO reach `WorldContinueOutcome.Summary`, and the GAME's own partial-damage list at 128,
   whose refusals today reach only `RestoredWorldFactReplay`'s error log and never the outcome — so a
   restore can report success while a native damage row was dropped at the world-entry seam.
   The apply path can also drop a row whose payload is unreadable.
   Make the live-write outcome travel back to the caller of `TryContinue` (the sink already returns
   per-write counts; nothing carries them out of the world-entry seam), so §6's "every dropped entry
   is surfaced" holds for the native half too.
   Make `IWorldFactSource.ApplyFacts` return what it applied/dropped (or expose the table counts)
   and fold that into the restore report, so §6's "every dropped entry is surfaced" holds for the
   world facts too.

   **Landed (S3.3):** `IWorldFactSource.ApplyFacts` already returned a `WorldFactApplyReport` (its
   refusals reach `WorldContinueOutcome.Summary`); the live-world half now reports too. The adapter's
   world-entry replay sends its per-write applied/refused counts to `WorldRestoreAudit`
   (`LiveWriteFinished`), which raises `Reported` and keeps `Last`; the Continue caller
   (`RunSaveCoordinator`, the one that invoked `TryContinue`) subscribes and logs an incomplete
   restore at error level, and the command console prints it for the player. A restore can therefore
   no longer be reported as a success while the game's own 128-entry table refused a row. The
   capture-side half of the same finding is closed as well: the native reader reports an unreadable
   table set (`NativeWorldFactCapture.Failure`) instead of an empty one, and the cut is REFUSED, so a
   "clean" snapshot can no longer be written from a missing world.
   **Widened again (S3.5, 2026-09-12):** the restored world-entity write is its own contribution with
   its own refused count — an entity the regenerated layer does not have (a consumed trap, an opened
   lockable, a damaged building) now names itself in the restore account instead of stopping at a
   warning in the log, which is what makes the count the audit waits for (three halves on a mid-run
   restore) meaningful rather than decorative.
7. **Refusal recovery** (found by S3.1's review round 2) — a decode-level refusal (a snapshot whose
   manifest kind contradicts its payload) is reported but has no fallback: the reader retries a
   backup only while the manifest is read, and `loadSnapshot` has already returned by then. Give
   the repository a "newest readable snapshot OF THIS WORLD" retry that runs after a decode
   refusal, so a damaged live snapshot falls back to its own newest backup the way an unreadable
   manifest already does (§6). Owner: S4.
8. **Host-side world-entity projection on a mid-run restore** (found while landing S3.2) — a
   restored layer's per-layer game objects are regenerated from the run baseline, and
   `WorldEntityKernelProjection` only raises its flat fact lists when the local role is GUEST. On
   the HOST a mid-run restore therefore keeps opened/consumed/damaged-building facts in the kernel
   and ships them to guests, but never writes them onto its own fresh world. S3.2's world-diff
   replay deliberately does not compensate by re-triggering the side effects it cannot undo
   (a replayed air write marks no building support loss, so the deaths the saved world already
   resolved are not re-rolled as fresh drops). The one seam where the two sides disagreed — the same
   restored row reaching a GUEST through the snapshot apply — is now explicit rather than implicit: a
   row read back out of the archive carries `SupportLossSettled` (`BlockStateEntryMsg` field 4), and
   the guest's snapshot apply re-settles support loss only for rows a LIVE write produced, so a guest
   no longer kills a building the host still holds. Deciding the host-side project-or-suppress rule
   and proving it — including what happens to the drops of a building the saved world already killed —
   belongs to S3.3/S3.5. Verification limit for S3.2: the replayed per-cell block diff is proven
   in-game (the adapter's world reads need a running game), the row routing/caps/replay lifecycle are
   proven in the Runtime suites, and the guest-side "restored row never re-settles" branch itself is
   an engine-side branch that the dual-client pass has to confirm.

   **Landed (S3.5, 2026-09-12).** The rule is ROLE-based, because the two roles restore at different
   moments: a guest's checkpoint lands on a world the host already generated (its live world IS the
   restored layer), so `WorldEntityKernelProjection` keeps raising the flat fact lists immediately,
   while the host/solo side restores at the Continue click — the only world alive then is the layer
   being REPLACED — so the projection HOLDS the facts (`IRestoredWorldEntitySource`) and
   `RestoredWorldFactReplay` writes them at the world-entry seam, after the block diff that defines
   the world they stand in and before the world-entry keypad broadcast. The write goes through the
   same three appliers the guest path uses (`EntityEventSync.OnTrapStateProjected` →
   `TrapVisualReplay.Replay`; `WorldBuildingEntitySync.OnOpenedEntitiesProjected` /
   `OnBuildingHealthProjected`), and each now returns an applied/refused count, so a fact whose
   entity the regenerated layer does not have reaches the restore report as its own half instead of a
   warning in the log. A death applied here stays a REMOTE death (`MarkRemoteEntityDeath`), so the
   drops the saved world already rolled are not rolled a second time — the question this scope left
   open about a building the saved world already killed.
   **The layer-end case is the opposite rule**: a layer-end cut names the layer being ENTERED, so its
   world-entity rows describe the layer being replaced and are dropped before the audit begins
   (mirroring its world-item rows) — and the ARCHIVE does not carry them either, for `world-entities.json`
   as for `items.json`, so a guest joining before the regenerated layer's seam cannot be handed facts
   about a layer the world no longer is. That is also why the seam's `HasPending` check matters: a
   mid-run restore must keep the restored kernel tables through the generation (the layer-boundary
   reset would erase the very facts the write is about), while a layer-end restore must not keep the
   arm alive into the next layer.
   **Not proven by the above**: the in-game result — that the host's fresh world actually shows the
   consumed traps / opened lockables / damaged buildings the cut described, that a corpse's loot was
   not re-rolled by the restored deaths, and that a guest receives exactly the restored set — needs
   the user's dual-client pass. The appliers' game-typed bodies (Unity `Physics2D.OverlapPoint`, the
   trap replay's game types) cannot be instantiated in the test host, so their counting contract is
   pinned at the Runtime seam and by static review of the adapter; see the S3.5 increment self-check.
9. **Solo menu-exit trigger** (found by the S3.3 adversarial pass) — **MOVED OUT of this ticket**: it
   is its own stage, `todo/save-solo-menu-exit-trigger.md` (S3.6), because it is a trigger-edge gap on
   the solo surface rather than part of the consistent cut's scope. The gap is unchanged there: the
   deliberate menu return is requested from session-teardown events and decided by
   `RunMenuReturnPolicy` for a HOST, so solo play (no session, no role) gets no menu-return cut; the
   fix is an in-world → menu transition edge in the run coordinator that requests the same seam cut,
   not a second cut path.

## S3.5 increment self-check (2026-09-12)

Scope 8 (host-side world-entity projection) and F3 (the layer-end in-layer-fact rule).

| mechanism | change | evidence |
|---|---|---|
| a restored kernel world-entity table on the HOST/SOLO side | the projection no longer returns early: it holds the facts for the world-entry seam (`IRestoredWorldEntitySource`) instead of projecting them onto the layer being replaced | `WorldEntityProjectionTests.HostCheckpointRestore_ArmsTheWorldEntryWriteWithTheFactsTheGuestProjects`, `.SoloCheckpointRestore_ArmsTheWorldEntryWriteToo`, `.PendingWorldEntryWrite_EndsOnCommitOrCancel_AndNeverTwice` |
| the same table on a GUEST | unchanged: the flat lists are raised immediately, from the SAME mapping function the host's pending read uses | `WorldEntityProjectionTests.GuestCheckpointRestore_ProjectsKernelWorldEntities` + the two rows above comparing both paths' rows |
| the host's write at the seam | `RestoredWorldFactReplay` writes the entity facts through `IRestoredWorldFactSink.ApplyWorldEntities` (the three existing appliers), commits on zero refusals and cancels with the reason otherwise | `RestoredWorldFactReplayTests.ApplyIfPending_WithOnlyTheWorldEntitiesPending_WritesAndCommitsThatHalf`, `.ApplyIfPending_WorldEntityRowsTheLayerDoesNotHave_ReachTheRestoreAccount` |
| the seam's `HasPending` gate | a pending world-entity write keeps the restored kernel tables through the generation (the layer-boundary reset must not run) | `RestoredWorldFactReplayTests.ApplyIfPending_WithOnlyTheWorldEntitiesPending_WritesAndCommitsThatHalf` (no runtime/native half pending, and the write still runs) |
| the restore's live-write account | the count follows the writers that are ACTUALLY armed when the click returns (the world-fact half always reports; the world-entity and item halves report when armed), never the cut kind alone — a count naming a half nobody will report would leave the restore awaiting forever, and one that is too low would report before the last writer ran | `WorldRestoreAuditTests.LiveWorldHalves_CountTheWritersThatAreActuallyArmed`, `.AThirdExpectedHalf_HoldsTheReportUntilTheItemReconcileArrives`, `RestoredWorldFactReplayTests.ApplyIfPending_MidRunRestore_ReportsTwoHalvesAndWaitsForTheItemReconcile`, `.ApplyIfPending_WithAnUnarmedWorldEntitySource_ReportsOnlyTheHalvesTheRestoreOwes` |
| a restore reports ONCE | a contribution that arrives after the report is ignored, and the world-entry seam's no-op report only fires while a restore is awaiting it — the seam runs the replay again on every later generation, so a completed restore must not be re-reported (the player would be told about a restore that is not happening) | `WorldRestoreAuditTests.LiveWriteFinished_AfterTheReportWasRaised_IsIgnored`, `RestoredWorldFactReplayTests.ApplyIfPending_OnTheGenerationAfterACompletedRestore_ReportsNothing`, `.ApplyIfPending_NothingToWrite_CompletesAnAwaitingAudit` |
| a throw during the seam write | every owed half reports incomplete and every handover is released (including the entity half, whose write never ran) | `RestoredWorldFactReplayTests.ApplyIfPending_WhenTheWriteThrows_ReportsAndReleasesTheWorldEntityHalfToo` |
| a new run superseding a restore | the kernel's restored per-entity facts are cancelled with the world-fact tables, before the new run's folder is created | `WorldSaveFixture` restore suites + `WorldSaveService.TryBeginRun` (the cancel sits beside `ClearPendingLiveReplay`/`CancelPendingRestore`); `RestoredWorldFactReplayTests` cover the release rule |
| the session ending before the seam | the arm is released with its counts (a session end takes the layer the facts describe with it) | `WorldEntityProjectionTests.SessionEnd_ReleasesThePendingWorldEntryWrite` |
| a layer-end cut's world-entity rows | dropped before the audit begins (the layer they describe is being replaced), mirroring the item rule | `WorldRestoreApplier.TryApply` + `WorldRestoreAuditTests.LiveWorldHalves_CountTheWritersThatAreActuallyArmed` |
| `items.json` of a layer-end cut | world-rooted rows (a ground item and everything inside a ground container) are dropped by the encoder with the layer-boundary reset's own rule; carried records and tombstones stay; a caller's pre-reset rows are named in the log | `WorldSnapshotWorldFactsTests.LayerAdvanceCut_KeepsTheWorldRootedItemsOutOfTheArchive` (red before the fix: the world item id was in the written file), `WorldSnapshotCodecTests.Encode_WritesOneEntryArrayPerDomainFileAndTheCharacters` |
| `world-entities.json` of a layer-end cut | the same rule: every per-entity row of the replaced layer is dropped by the encoder (named), so the kernel checkpoint a later guest join receives carries no fact about a layer the world no longer is | `WorldSnapshotWorldFactsTests.LayerAdvanceCut_KeepsTheWorldEntityFactsOutOfTheArchive`, `WorldSaveContinueTests.ConsumedTrap_StaysConsumedAfterRestore` (a mid-run cut, where the fact belongs to the restored layer) |
| the appliers' outcome counts | `TrapVisualReplay.Replay` and the building-entity appliers report whether the row reached the live world: a duplicate the guard drops counts as present (the state IS there), and a missing entity is REFUSED — including the destructive families, whose explosion is replayed as presentation but whose consumption fact is then not in the world | Runtime-seam contract tests above; the game-typed bodies are static-reviewed only (see below) |

**Not proven by the above**: the in-game result. The adapter's appliers run against Unity types that
the test host cannot instantiate (`Physics2D.OverlapPoint`, `TrapEffectApplier.FindTrap<T>`,
`MarkRemoteEntityDeath`), so what a real host's regenerated world shows after a mid-run restore — and
whether a guest sees exactly the restored set — is the user's dual-client pass.

## S3.5 increment independent adversarial pass (2026-09-12)

An independent reviewer (fresh context, no stake in the change) audited the increment — scope 8 and
F3 — against its claims by reading the diff, the Runtime seam and the adapter appliers, and by
running the focused suites (89/89) and the normative gates (32/32). It could NOT falsify: the wire
surface (no protocol member or message id changed), the structure/gate claims, the guest path's
unchanged immediacy, the host/solo arming at the Runtime seam, and the item rule's behaviour
(ground items and ground-container children dropped for a layer-end cut, carried records and
tombstones kept, `ItemLocationChain` cycle-safe). It DID find real defects, all fixed on top of the
increment:

1. **BLOCKER — the destructive trap facts counted as applied with no live entity.** The three
   explosion replays returned "applied" unconditionally, so a fact whose entity the regenerated layer
   does not have (exactly the divergence the count exists to surface) was reported as restored.
   Fixed: the three helpers return whether the entity exists (a duplicate that the guard drops still
   counts as present — the state IS in the world), and a missing entity is a refused row.
2. **BLOCKER — the audit could report twice.** `WorldRestoreAudit.LiveWriteFinished` accepted
   contributions after the report was raised, and the world-entry seam's "carried nothing" report
   fired on EVERY later generation, so a normal generation after a completed restore raised a second
   report for a world that was no longer current. Fixed: the no-op report only fires while a restore
   awaits it, and the audit ignores contributions once it has reported (`_reported`, reset by
   `BeginRestore`/`AbandonRestore`).
3. **MAJOR — a layer-end archive still carried world-entity rows into the kernel.** The item rule
   (F3) had no counterpart for `world-entities.json`, so a pre-reset layer-end archive put the
   replaced layer's per-entity facts into the kernel — and a guest joining before the regenerated
   layer's seam would project them. Fixed: the encoder drops them for a layer-end cut with the same
   named warning (the enemy/fluid tables deliberately stay: no layer boundary resets them, so
   carrying them matches what the kernel itself keeps).
4. **MAJOR — the new arm was not released on a session end.** Fixed: the projection subscribes to
   `ISessionControl.SessionEnded` and cancels the arm with its counts (the new-run path already
   cancelled it in `WorldSaveService.TryBeginRun`).
5. **MAJOR (latent) — the expected-halves count could wait forever.** The count was derived from the
   cut kind, but the two optional writers can be absent from a composition; fixed: it is derived from
   the writers that are armed at the click (`WorldRestoreApplier.LiveWorldHalves`).
6. **MINOR — the parent-lookup map could throw on a duplicate instance id.** Fixed: the map is built
   with an indexer (the kernel's table is keyed by instance id, so a duplicate is a broken caller and
   a cut must not die inside a lookup helper).
7. **NIT — trailing whitespace** in this ticket; removed.

A third focused pass (fresh context) audited exactly those four fixes — the audit's close-on-abandon
state, the release-everything abandon entry point, the empty-projection gate and the corrected
encoder/test wording — and could not falsify any of them (verdict: safe to commit). It raised two
minors and two nits, all closed here: the abandoned and idle account states are now distinct (an
abandon with nothing in flight is a no-op, so a write with no `BeginRestore` still reports itself,
and an abandoned account accepts no straggler), the empty-projection gate asks the PROJECTION
(`HasProjectableFact`) instead of the raw table — the trap-state filter can reduce a non-empty table
to no rows — `WorldSaveService.AbandonRestore` also releases the item reconcile (so the interface's
"releases every handover" contract is true for any caller, not just the one in the adapter), and the
solo wording now says "any non-host state", which is the registries' actual predicate.

Recorded but NOT fixed (pre-existing; none of them made worse by this increment):

- **`WorldRestoreAudit` carries no restore IDENTITY**: a contribution that arrives after a NEW
  restore has opened its account is counted toward the new one, so a very late writer could raise the
  new restore's report one half early. Every writer is gated today (each reports once, behind its own
  pending flag, and a completed or abandoned account ignores stragglers), so the window needs a
  writer that reports late ACROSS a `BeginRestore`; the fix is an epoch on the account (worldId +
  revision, or an id the contributors echo).
- **The world-entry seam's `HasPending` gate does not consult the ITEM arm**: a restore whose only
  pending half is the item reconcile (no world facts, no native run fields, no restored world-entity
  rows) would run the layer-boundary reset and drop the restored world items instead of keeping them
  for the reconcile. In production the native run-field row is pending for every host/solo restore,
  which holds the gate true; a composition with no native reader is where it bites.
- **An action that returns `false` for a reason other than "already in that state"** (e.g.
  `CrystalStateActions.ApplyCrystalMimic` on a crystal with no mimic effect) is counted as applied by
  `TrapVisualReplay.ReplayState`, so that divergence can be under-reported. Distinguishing the two
  needs a tri-state verdict from the shared action library.
- **Sibling-domain gaps** (recorded above): the world-entity registries reset their kernel tables for
  a host only, no layer boundary resets the enemy/fluid/player tables at all, and those reset
  commands stay wire-reachable.


## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | Mid-run save with mined/placed/quaked blocks + partial damage | Reload reproduces the same block diff exactly (compare against the pinned post-restore dump) |
| 2 | Mid-run save with world items on the ground, in containers, carried, worn | Same identities, locations, container trees; no duplicates, no loss |
| 3 | Mid-run save with opened/damaged buildings, consumed traps, fluids, enemies | Same facts; no re-trigger; no resurrection |
| 4 | Save during each in-flight state in the table above | The chosen policy applies and is logged; no silent loss, no duplication |
| 5 | Save → load → save → load | Byte-comparable domain tables (modulo timestamps/revisions); world fingerprint stable |
| 6 | Save taken mid-frame while a command batch is pending | The cut is consistent: no half-applied operation in the snapshot, revision matches the payload |
| 7 | Restore of a mid-run snapshot | Resumes the *same* layer with all mutations — never a regenerated-but-different layer |

## Verification limits

Item/entity/block facts are machine-verifiable through the kernel and the format layer. Native world
tables (keypad codes, geyser rolls, earthquake timers, `WorldGeneration.blockDamages`) live behind the
adapter; those rows are verified by adapter-level tests plus the user's dual-client pass, and this
ticket must name which is which.

