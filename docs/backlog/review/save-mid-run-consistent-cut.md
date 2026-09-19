# S3 — Mid-run consistent cut and world diff

- Status: Review (landed 2026-09-17; awaiting the final unified acceptance pass; approved for implementation by the user on 2026-09-10).
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
  no peer claimed) and minor/nit items — all fixed in the same cycle. **S3.5 (exactly-once plus the
  documentation and re-anchoring pass) closed 2026-09-17** (see *S3.5 closure* below): scopes 7-9 are no
  longer open — scope 7 landed with S4.4, scope 8 with the S3.5 increment, and scope 9 as S3.6 — and the
  mid-run trigger is enabled, so a build produces both the S2
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
  increment; scope 9 moved to its own stage ticket (S3.6, since landed as
  `review/save-solo-menu-exit-trigger.md`). **A third pass (2026-09-12) closed recorded
  gap 1**: the restore account now carries the restore ATTEMPT's identity
  (`ItemKernelAuthority.RestoreSequence`, stamped by every arm and echoed by every contribution), so a
  half of an earlier attempt can no longer be counted toward a newer restore's account — see *the
  restore ATTEMPT identity* below.
- Priority: High
- Category: Persistence / save system
- Source: Stage 3 of `docs/backlog/review/save-system-mid-run-and-layer-end.md`; this is the user's hard requirement — "需要重点关注存档的中途性质，防止出现多生成、少生成内容的情况"
- Related: `docs/architecture/save-archive-format.md` §4/§6 (S3.2 also recorded the restore apply seam in §6.1), `review/save-layer-end-save-and-restore.md` (S2), `review/save-multiplayer-restore-and-backups.md` (S4), `review/restore-account-arm-release.md` (the restore-account residuals this ticket records)

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
   `review/save-native-run-field-parity.md`), S3.5 exactly-once plus documentation and re-anchoring.
3. **Native run fields** — decided per field in `review/save-native-run-field-parity.md`; a field
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
S3.4b and are NOT implemented; see `review/save-native-run-field-parity.md`.

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
   See `review/save-native-run-field-parity.md` → "Disposition of the recorded gaps (2026-09-17)".

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
   `WorldCutDeferral.MaxFrames` (eight frames), and a state that outlasts the deadline is
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

   **Landed (S4.4, 2026-09-14).** The retry runs after a decode refusal and it does more than fall
   back: `WorldRestoreRecovery` walks the world's backups newest-first (skipping the one the load
   already used), decodes each with the same decoder that refused the live snapshot, and takes the
   first that decodes; `WorldBackupPromotion` then preserves the refused snapshot as
   `damaged-<stamp>/`, archives the pre-restore copy into `backups/` under the format's own
   `pre-restore-backup` reason, and promotes the backup into `live/` — because a restore read out of
   an archive that never becomes live is deleted by the next cut's transaction, evidence and all.
   See `docs/backlog/review/save-interval-autosave-and-backup-recovery.md` and decision 182.
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
   is its own stage, S3.6, because it is a trigger-edge gap on the solo surface rather than part of the
   consistent cut's scope. **Landed 2026-09-14** as `review/save-solo-menu-exit-trigger.md`, with the
   mechanism finding that the fix is NOT the in-world → menu transition edge this scope first named
   (the leave's own scene load destroys the world before any next-frame cut could read it) but the
   leave ACTION itself, intercepted at `PlayerCamera.ToMainMenu` and replayed by the frame-end seam
   after the cut (decision 176).

## S3.5 increment self-check (2026-09-12)

Scope 8 (host-side world-entity projection) and F3 (the layer-end in-layer-fact rule).

| mechanism | change | evidence |
|---|---|---|
| a restored kernel world-entity table on the HOST/SOLO side | the projection no longer returns early: it holds the facts for the world-entry seam (`IRestoredWorldEntitySource`) instead of projecting them onto the layer being replaced | `WorldEntityProjectionTests.HostCheckpointRestore_ArmsTheWorldEntryWriteWithTheFactsTheGuestProjects`, `.SoloCheckpointRestore_ArmsTheWorldEntryWriteToo`, `.PendingWorldEntryWrite_EndsOnCommitOrCancel_AndNeverTwice` |
| the same table on a GUEST | unchanged: the flat lists are raised immediately, from the SAME mapping function the host's pending read uses | `WorldEntityProjectionTests.GuestCheckpointRestore_ProjectsKernelWorldEntities` + the two rows above comparing both paths' rows |
| the host's write at the seam | `RestoredWorldFactReplay` writes the entity facts through `IRestoredWorldFactSink.ApplyWorldEntities` (the three existing appliers), commits on zero refusals and cancels with the reason otherwise | `RestoredWorldFactReplayTests.ApplyIfPending_WithOnlyTheWorldEntitiesPending_WritesAndCommitsThatHalf`, `.ApplyIfPending_WorldEntityRowsTheLayerDoesNotHave_ReachTheRestoreAccount` |
| the seam's `HasPending` gate | a pending world-entity write keeps the restored kernel tables through the generation (the layer-boundary reset must not run) | `RestoredWorldFactReplayTests.ApplyIfPending_WithOnlyTheWorldEntitiesPending_WritesAndCommitsThatHalf` (no runtime/native half pending, and the write still runs) |
| the restore's live-write account | the halves follow the writers that are ACTUALLY armed when the click returns (the world-fact half always reports; the world-entity and item halves report when armed), never the cut kind alone — a list naming a half nobody will report would leave the restore awaiting forever, and one omitting a writer the seam reports would raise the report before the last writer ran | `WorldRestoreAuditTests.LiveWorldHalves_NameTheWritersThatAreActuallyArmed`, `.AThirdExpectedHalf_HoldsTheReportUntilTheItemReconcileArrives`, `RestoredWorldFactReplayTests.ApplyIfPending_MidRunRestore_ReportsTwoHalvesAndWaitsForTheItemReconcile`, `.ApplyIfPending_WithAnUnarmedWorldEntitySource_ReportsOnlyTheHalvesTheRestoreOwes` |
| a restore reports ONCE | a contribution that arrives after the report is ignored, and the world-entry seam's no-op report only fires while a restore is awaiting it — the seam runs the replay again on every later generation, so a completed restore must not be re-reported (the player would be told about a restore that is not happening) | `WorldRestoreAuditTests.LiveWriteFinished_AfterTheReportWasRaised_IsIgnored`, `RestoredWorldFactReplayTests.ApplyIfPending_OnTheGenerationAfterACompletedRestore_ReportsNothing`, `.ApplyIfPending_NothingToWrite_CompletesAnAwaitingAudit` |
| a throw during the seam write | every owed half reports incomplete and every handover is released (including the entity half, whose write never ran) | `RestoredWorldFactReplayTests.ApplyIfPending_WhenTheWriteThrows_ReportsAndReleasesTheWorldEntityHalfToo` |
| a new run superseding a restore | the kernel's restored per-entity facts are cancelled with the world-fact tables, before the new run's folder is created | `WorldSaveFixture` restore suites + `WorldSaveService.TryBeginRun` (the cancel sits beside `ClearPendingLiveReplay`/`CancelPendingRestore`); `RestoredWorldFactReplayTests` cover the release rule |
| the session ending before the seam | the arm is released with its counts (a session end takes the layer the facts describe with it) | `WorldEntityProjectionTests.SessionEnd_ReleasesThePendingWorldEntryWrite` |
| a layer-end cut's world-entity rows | dropped before the audit begins (the layer they describe is being replaced), mirroring the item rule | `WorldRestoreApplier.TryApply` + `WorldRestoreAuditTests.LiveWorldHalves_NameTheWritersThatAreActuallyArmed` |
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

## S3.5 increment follow-up — the ITEM arm of the entry gate (2026-09-12, later pass)

Fixed the gap the S3.5 adversarial pass left recorded: the world-entry seam's `HasPending` gate did not
consult the item arm, so a restore whose ONLY pending half was the generation reconcile would have
taken the layer-boundary reset — and that reset drops every world-rooted row (the
`ItemLocationChain.IsWorldRooted` subtree rule), which is exactly the set the reconcile exists to
materialize. The loss would have been silent: the reconcile would then have nothing left to bind.

The gate now reads all four halves. The item half rides a narrow port instead of the whole item surface
(interface segregation, and it keeps the construction graph unchanged): `IRestoredWorldItemSource.
RestoredWorldItemsPending`, implemented by `IItemControl`, so the gate and the reconcile read the SAME
flag and cannot disagree. `RestoredWorldFactReplay` takes it as the fourth optional source beside
`INativeWorldFacts` and `IRestoredWorldEntitySource`, and `GameAdapterDomains` passes the item control
it already holds.

- Red (on the pre-fix gate): `RestoredWorldFactReplayTests.HasPending_WithOnlyTheItemReconcileOwed_
  KeepsTheGateTrue` FAILED with `Assert.True() Failure: Expected True, Actual False` — 19 passed, 1
  failed of 20. The port was threaded in FIRST as a behavior-preserving step (the field read but not
  yet consulted), so the red was a real assertion failure rather than a compile error.
- Green: the same suite passes 20/20 after the gate change. `HasPending_WithEveryHalfLanded_IsFalse`
  pins that the wider gate did not become always-true.
- Family check: `HasPending` is the only place in the tree that combines the restore halves (grep on
  `HasPendingLiveReplay` / `HasPendingRestore`). `RestoredWorldFactReplay.ApplyIfPending`'s three
  locals are "what this replay WRITES" and deliberately do NOT gain the item arm;
  `WorldRestoreApplier.LiveWorldHalves` already counted it, which is what made the mismatch a gate bug
  rather than a counting bug.
- NOT proven by the above: the seam's own branch (`WorldEventSync`'s keep-vs-reset choice) lives in the
  Game Adapter and cannot be instantiated in the test host, so its decision is pinned through the gate
  input plus static review — the same limit the rest of this increment's adapter bodies carry.

An independent adversarial pass (fresh context, no stake in the change) could NOT falsify the fix. It
confirmed the four-arm gate and its false-when-everything-landed case, the assembly path (the item
control is a required constructor argument of the domain graph, and the same singleton serves the gate,
the reconcile and the audit), the interface move (no other implementer, no reflection/structure contract
reads that member), and the gap itself — the layer reset really does drop the world-rooted set the
reconcile binds (`ItemLocationChain`: `World` true, a contained subtree resolving to `World` true,
carried and terminal false; the reset's outcome reaches the restore report as a COMPLETE account, so the
loss is silent at report level). No blocker and no major; its minors and nits are closed here:

- The PRODUCTION shape (all four sources wired, only the item half owed) is now pinned by
  `HasPending_WithEverySourceWiredAndOnlyTheItemReconcileOwed_KeepsTheGateTrue`, so a gate that dropped
  the arm whenever another source was present would be caught instead of passing through the null
  world-entity source the first case uses.
- `WorldRestoreAudit`'s class comment claimed a restore owes "one for the world facts, plus one for the
  item reconcile", omitting the world-entity arm the count has carried since scope 8. Corrected to name
  all three contributions and the "follows the writers actually armed" rule.
- Wording: the replay's class comment now says the WRITE takes three owners while the GATE reads a
  fourth, and the port's cref points at its own member instead of at the interface that inherits it.
- Recorded, NOT fixed here: `WorldSaveService.TryBeginRun` cancels the fact, world-entity and native
  arms but not the item arm (its comment claims "every half"; `AbandonRestore` does cancel all four).
  The adapter's `RunSaveCoordinator` cancels the item arm first on the only path that reaches
  `TryBeginRun`, so no current caller is exposed — but the new gate makes a drifted item arm more
  consequential (it would keep the seam skipping the layer reset across generations). Closing it needs
  either an item control in the save fixture or a narrower dependency on the service, so it is its own
  change rather than a rider on this one.

Recorded but NOT fixed (pre-existing; none of them made worse by this increment):

- **`WorldRestoreAudit` carries no restore IDENTITY** — **FIXED 2026-09-12** (the restore ATTEMPT
  identity: `ItemKernelAuthority.RestoreSequence` is stamped by every arm and echoed by every
  contribution, and an account ignores a half of another attempt; the red/green pair and the
  verification limits are in *the restore ATTEMPT identity* below).
- **An action that returns `false` for a reason other than "already in that state"** (e.g.
  `CrystalStateActions.ApplyCrystalMimic` on a crystal with no mimic effect) was counted as applied by
  `TrapVisualReplay.ReplayState`, so that divergence could be under-reported. Distinguishing the two
  needed a tri-state verdict from the shared action library — **FIXED 2026-09-13** (`TrapActionOutcome`
  plus the Runtime's one verdict rule; the red/green pair, the family audit and the verification limits
  are in *the shared action verdict* below).
- **Sibling-domain gaps** (recorded above): the world-entity registries reset their kernel tables for
  a host only, no layer boundary resets the enemy/fluid/player tables at all, and those reset
  commands stay wire-reachable. Scoping notes for the fix are below (reading, no adversarial pass yet).

### The sibling-domain layer reset — gap 4 (2026-09-13)

Recorded gap 4 is closed. The defect was not "a dead command": the kernel's enemy and fluid tables
describe ONE layer (a live enemy row names a position in that layer's layout, and the host's enemy-id
counter keeps allocating from a per-session sequence; a fluid chunk is a coarse total of that layer's
grid), no layer boundary reset them, and the rows therefore survived into a world that was no longer
that layer. Two consumers saw them: a late joiner's checkpoint, and a `layer-end` restore, which reads
the archive back into the kernel before the regenerated layer exists.

**The seam, and why the archive carries the rule too.** The whole layer-scoped family resets at the
WORLD-ENTRY seam (`WorldEventSync.TryCaptureWorldBaseline` -> `WorldService.ResetWorldLayerTables`),
which is also the generation the mid-run restore's pending-replay gate already skips. The layer-end
cut's own kernel commit is deliberately NOT a seam anything may depend on, and this section's first
draft got the reason wrong (an adversarial pass caught it): the reset and the cut run inside ONE call
stack (`WorldParamsService.CaptureAtBoundary` resets, then `PublishWorldParams` commits the advance that
takes the cut), so the reset DOES run before the cut — while the per-frame enemy projection
(`EnemyKernelProjection.Sync`, a mirror of the LIVE scene) can write the old layer's rows back into the
kernel before the checkpoint is read, because the game destroys those entities over the following frames
(`WorldGeneration.Clear`, `reversing/.../WorldGeneration.cs:1077-1104`). Which of the two wins is a
frame-timing fact, so the ARCHIVE states the rule on its own side instead: `WorldSnapshotEncoder` now
drops a layer-end cut's live enemy rows and fluid chunks — keeping the tombstones — exactly as it
already drops the world-rooted item rows, the per-entity rows, the block diff and the transients, and it
names what it dropped. The reset, the encoder rule and the restore prune below therefore agree whichever
of them runs first.

**The family.** The reset names its four members in one place — the world-rooted item rows, the
world-entity facts, the enemy live rows and the fluid chunks — because the membership rule is one rule:
a fact about the layer being LEFT must not survive into the layer being entered. The seam is
`WorldService.ResetWorldLayerTables` (reached through the adapter's world-entry baseline capture); the
first two members were already reset there, and the two new ones are issued by `LayerScopedTableReset`,
the RULE as its own type so the seam stays readable. The PLAYER table is deliberately not a member (every
`PlayerState` fact is cross-layer, so a reset would erase injuries and carry relations on every
descent), and its reset command family is DELETED rather than left as dead vocabulary. The enemy
TOMBSTONES are kept: `EnemyStateTable.WithoutLiveEnemies` drops the live rows and leaves the terminal
facts standing, so "the enemy standing here was killed" still stops a stale live row from resurrecting
it — the same acceptance rule the restore suite pins.

**The wire.** The layer-scoped `Reset*Command`s were removed from `KernelWireMapper` and from
`WireCommandKind`/`WirePayloadType`/`ProtocolFrameValidator`, closing the "wire-reachable but no
caller" half of the gap: `TryResetEnemies` and `TryResetFluids` are now driven by the host's own
boundary, `TryResetWorldEntities` by its registries, and `TryResetPlayers` is gone. That is not
bookkeeping: the received-command path carries no role check (`KernelProtocolCommandHandler` executes
what the envelope names, and the kernel's `CommandContext` carries no role), so the wire mapping IS the
gate — an unmapped kind cannot be reconstructed by `KernelWireMapper.FromWireCommand` at all. Their
EVENTS keep their wire form, which is load-bearing: the host's committed batch is how a guest's replay
kernel learns that the boundary happened (it is how the world-item and world-entity resets already reach
a guest). Removing the event forms was tried and reverted — `KernelProtocolService.BroadcastCommittedBatch`
encodes every committed event, so an unmappable one throws out of the host's own reset.
`PlayersResetEvent` is gone entirely, because nothing can produce it once the command family is deleted.

**The adversarial pass (fresh context, 2026-09-13) falsified two claims of the first cut.** Both are
fixed here rather than recorded, because both were real:

- **`ResetWorldEntities` was still wire-mapped and reachable by a guest.** The first cut removed three
  of the four layer-scoped reset commands and the docs claimed all four were host-local; the fourth was
  live: `WireCommandKind.ResetWorldEntities = 13` + `ProtocolFrameValidator.cs:322` +
  `KernelWireMapper.cs:495-499` (reconstructing the command with `actor = header.SenderId`) +
  `KernelProtocolCommandHandler.cs:65-66` executing it after only an epoch check, with no actor/authority
  validation anywhere in the kernel. A guest could therefore wipe the host's consumed-trap / opened-lockable
  / building-health table at will — the exact "family, not the reported case" miss this cycle exists to
  avoid. Its wire identity is gone, so the command is host-local like its three siblings, and the E7 row
  plus decision 174 now state the mechanism correctly.
- **The seam rationale was wrong.** The first cut argued a reset at the layer-end cut would be "undone
  frame by frame"; the reset and the cut actually run inside one call stack, with the reset before the
  cut, and whether the projection re-writes the rows in between is frame timing. The rationale (decision
  174, this ticket, `LayerScopedTableReset`) is restated in those terms, and the encoder-side rule above
  is what makes the archive correct regardless of that timing.

The pass also produced three findings that are recorded rather than fixed, and one nit that is fixed:

- The guest-gate test drove the HOST's `WorldService` while seeding the guest's kernel, so it could not
  fail on the gate it names. **Fixed**: it now drives the guest's own `WorldService`.
- `WorldSaveService` captured `session.LocalSteamId` once at construction (the repo's documented "late
  Steam init captured as 0" shape). **Fixed**: the applier takes a `Func<ulong>` and resolves the actor
  at call time. (The other three resets resolve theirs at call time already.)
- The seam's reachability in a live session is static-review only; the per-frame ordering around the
  game's `Clear`/`InstantiateWorld` is read from `reversing/`, not observed.
- `ProtocolVersion` is deliberately NOT bumped although the wire kind space changed (three command kinds
  and six payload members removed). A bump would be the honest signal, but `SaveArchiveReader` refuses an
  archive whose protocol version differs, so bumping it invalidates every existing save — a product
  decision about save compatibility that this consistency fix must not make on its own. It is recorded
  here for the version-policy cycle (the project's stated stance is a hard version refusal at release,
  which is the same switch).

**Red, recorded on the pre-fix source** (the fix's `src/` changes stashed, tests in their final shape):
`WorldSaveContinueTests.LayerEndRestore_LendsNoReplacedLayerEnemyRowToTheNewLayer` and
`.LayerEndRestore_LendsNoReplacedLayerFluidRowToTheNewLayer` both failed with
`Assert.Empty() Failure: Collection was not empty` — 2 failed / 0 passed of 2. The enemy case is the
harm directly: a layer-end restore handed the replaced layer's `spider (epoch 1, counter 7)` row to the
new layer's kernel. The same pair was re-checked AFTER the encoder rule landed (the encoder now drops the
rows this fixture used to write, so the pair reaches the restore half only through a hand-patched
archive): with `DropReplacedLayerKernelTables` commented out they fail again with
`Collection: [FluidRegionState { ChunkX = 1, ChunkY = 2, TotalAmount = 7, MainType = 1, UpdatedAtMs = 50 }]`,
which is what pins the restore-side prune as load-bearing rather than decorative.

**Green:** the targeted families, the full suite and the normative gates in the commit's own run. New
coverage: the two layer-end restore cases above plus their mid-run counterpart
(`WorldSaveContinueTests.MidRunRestore_KeepsTheLayerScopedRowsTheCutNames`); the encoder rule and its
mid-run half (`WorldSnapshotWorldFactsTests.Encode_LayerEndCut_DropsTheLiveEnemyAndFluidRowsButKeepsTheTombstone`,
`.Encode_MidRunCut_KeepsTheLiveEnemyAndFluidRows`); `LayerBoundaryKernelResetTests` (the boundary drops
both families, keeps the player table and the tombstones, and leaves a GUEST's kernel alone — driven
through the guest's own service); `EnemyDomainKernelTests` now pins the reset's scope (live rows cleared,
tombstone kept, the killed id still not resurrectable). `RemovedEnemy_StaysTerminalAfterRestore` keeps
passing unchanged, which is what forced the tombstone rule in the first place.

**What is NOT proven:** the seam's own branch (that the adapter reaches `TryCaptureWorldBaseline`) is
Game-Adapter code the test host cannot instantiate, so the wiring is pinned at the Runtime port
(`WorldService.ResetDamagedBlocks`) plus static review — the same limit the rest of this stage's adapter
bodies carry. The per-frame ordering between the host's generation boundary and the game's scene teardown
is read from the decompiled game paths, not observed (which is exactly why the archive rule does not
depend on it). The user's dual-client pass is what shows that entering a layer starts with a clean table:
no enemy of the previous layer appearing on the new one, and no stale fluid blob.

**Residuals recorded, not fixed here:**

- A guest's kernel keeps the previous layer's rows for the window between the host's boundary and the
  arrival of the host's reset batch, and its own `FluidKernelReadProjection` / `EnemySyncService`
  buffers are only cleared by the removal events and the new layer's snapshot. The window closes by
  itself (the `EnemiesReset`/`FluidsReset` batch, then the world-entry checkpoint fan-out); making it
  synchronous would need a second wire path for a state that is already converging. The reset EVENTS
  therefore keep their wire form even though the commands have none — the guest's replay kernel has to
  learn that the host's table restarted.
- `EnemySyncCoordinator._idByEntity` / `_mappingEstablished` survive a layer boundary (they are cleared
  only by `Unbind`), so the new layer's enemies are allocated as `runtimeSpawn: true` and the stale
  bindings linger until the scene swap. Adapter-side identity, a different owner from this reset, and
  not made worse here — it is recorded for its own ticket rather than patched in this cycle.

### Gap 4 scoping notes (2026-09-12, reading only - SUPERSEDED by the fix above)

Reading the code before choosing a fix separates the recorded bullet into three parts:

- **The player table must NOT be reset at a layer boundary.** `PlayerState` carries the durable
  cross-layer terminal facts — alive/conscious, the carry relation, the limb latch set, body state and
  skills (`GameState/Domains/Players/PlayerState.cs:5-19`) — and its readers are the limb/status/carry
  projections. A boundary reset would erase a player's injuries and carry relation on every descent, so
  the "enemy/fluid/**player**" grouping in the recorded bullet is a false positive for player.
- **The enemy and fluid tables already converge on the host through their own projections.**
  `EnemyKernelProjection.Sync` removes every kernel row the live scene no longer has
  (`Runtime/Session/EntitySync/EnemyKernelProjection.cs:45-49`), and `FluidKernelProjection.Sync` writes
  a zero fact for a chunk that left the host's non-empty set
  (`Runtime/Session/World/FluidKernelProjection.cs:56-60`); a guest converges too, because its kernel is
  replaced from the host's snapshot. What the missing reset leaves is therefore a WINDOW (the rest of the
  generation) and a rule disagreement rather than a lasting ghost: F3 removes every in-layer fact from a
  layer-end cut's `items.json` / `world-entities.json` but deliberately keeps the enemy/fluid rows
  ("no layer boundary resets them") — which flips the moment the boundary itself resets them.
- **The three reset commands have no product caller.** `TryResetEnemies` / `TryResetFluids` /
  `TryResetPlayers` (`Runtime/Session/Items/ItemKernelAuthority.cs:236-267`) are reachable from the wire
  (`KernelWireMapper` maps all three) and handled by their domain modules, but nothing under `src/` calls
  them; only `TryResetWorldEntities` has callers (the three world-entity registries). `EnemyDomainModule.
  Decide` accepts a reset unconditionally (`GameState/Domains/Entities/EnemyDomainModule.cs:27`), and
  `AuthorityKind.HostOnly` is declared metadata only — the kernel's `CommandContext` carries no role, so
  no authority check refuses such a command (the project's accept-first stance, not a new hole).

The fix is therefore a design choice, not a mechanical one: wire `ResetEnemies` / `ResetFluids` into the
layer boundary (and then decide whether a layer-end cut must drop those rows from the archive too, to
keep F3's rule coherent), or delete the three dead commands and keep relying on the projections —
`ResetPlayers` being redundant in either case while the player table stays cross-layer.


## S3.5 increment follow-up — the restore ATTEMPT identity (2026-09-12, third pass)

Recorded gap 1 is closed: the restore account could not attribute a contribution, so a live-world half
that reported after a NEW restore had reopened the account was counted toward the new one.

**The identity.** The kernel already knows which restore produced the state every arm is armed from, so
`ItemKernelAuthority.RestoreSequence` now counts them (bumped by every successful `Restore`,
`ItemKernelAuthority.cs:79`; 0 = the kernel was never restored, `:129`). It is the value a writer stamps
onto the arm it creates, and the value the account is opened for:

| writer | stamp (arm time) | carried by |
|---|---|---|
| the Runtime world-fact tables | `WorldFactLifecycle.ApplyFacts(..., restoreSequence)` (`WorldFactLifecycle.cs:126`), handed down by `WorldFactRestore.Apply` | `IWorldFactSource.AppliedRestoreSequence` (`IWorldFactSource.cs:69`) → the world-fact contribution, and also the "carried no live-world fact" report, which is a statement about the same attempt |
| the restored world-entity facts | armed inside the kernel restore (`WorldEntityKernelProjection.cs:148`) | `IRestoredWorldEntitySource.PendingRestoreSequence` (`:88`) |
| the restored world-item set | armed inside the kernel restore (`RestoredWorldItemSet.cs:48`) | internal — the set is its own reporter (`:83`, `:105`) |

`WorldRestoreApplier` reads `kernel.RestoreSequence` ONCE after `kernel.Restore`
(`WorldRestoreApplier.cs:188`) and passes it both to `WorldFactRestore.Apply` (`:195`) and to
`BeginRestore` (`:230`), so the arms and the account cannot disagree.
`WorldRestoreAudit.LiveWriteFinished` ignores a contribution whose sequence is not the open account's
and logs it at warning level (`WorldRestoreAudit.cs:130`; `LiveWriteAbandoned` takes the identity too,
`:163`). The audit now takes the composition's logger (optional, so a test host may omit it;
`WorldSaveCompositionTests` pins that the production root registers it). The identity rule binds an OPEN
account only: a write that reaches the seam with no account at all still reports itself, which is the
documented no-Begin diagnostic path (`WorldRestoreAuditTests.LiveWriteFinished_WithoutABegin_StillReportsTheWrite`).

The adapter's NATIVE handover carries no stamp of its own — it is handed to the live world by
`WorldFactRestore` in the same call that applies the Runtime tables, and the world-fact half is what
reports both. That is why the supersession below has to release it too: an attempt that leaves native
values armed while a new attempt's apply stamps the Runtime half would otherwise have its rows written
into the new layer under the new attempt's identity.

**A new restore SUPERSEDES the previous attempt** (`WorldRestoreApplier.cs:146-166`, before the kernel
restore that arms this attempt's own halves): the account is closed (`AbandonRestore`, so a dead
attempt's arm releases cannot report into it) and every handover it left armed is released — the Runtime
world-fact marker, the world-entity facts, the adapter's native handover and the restored world-item set
— so that NOTHING is armed while this attempt's arms are created. The world-fact marker is released even
though `ApplyFacts` would replace it: that replacement needs the restore to APPLY, and the refusal paths
below must not leave a dead attempt's "the live world still owes these facts" marker behind for the next
generation to act on.

This is not tidiness, and it is the fix for the MAJOR the adversarial pass found (below): the seam and
the generation reconcile act on PRESENCE, and the expectation counts the writers that are armed, while a
contribution is attributed by the arm's STAMP. An arm a dead attempt left behind therefore has to go —
kept, its rows are written into THIS layer, and the half is counted as one the new restore owes while its
identity can never match the new account's, which leaves that account awaiting a contribution the audit
refuses, forever. (The same release closes the pre-existing leftover-handover leak for a cut that carries
no native values and for a checkpoint that projects no world-entity fact.)

- **Red, recorded on the pre-fix source**: the three new cases in `WorldRestoreAuditTests` were written
  in the pre-fix call shape (`BeginRestore(worldId, expected)` / `LiveWriteFinished(complete, refused,
  summary)`) and run with the `src/` changes stashed, which is the only form that compiles against them;
  they FAILED with `Assert.Empty() Failure: Collection was not empty` — 3 failed / 15 passed of 18.
  `AStragglerFromAnEarlierRestore_DoesNotStandInForAHalfTheNewRestoreOwes` produced
  `[WorldRestoreLiveWriteReport { WorldId = w-new, Complete = False, Summary = "a straggler from the
  previous restore; the new restore's world facts" }]`: the new restore's report was raised one half
  early, with the previous attempt's row inside it. `AStragglerFromAnEarlierRestore_
  DoesNotCompleteTheNewAccount` and `AnAbandonedStragglerFromAnEarlierRestore_IsIgnoredToo` failed the
  same way, so the gap was demonstrated as an assertion failure, not as a compile error.
- **Red for the supersession**: `WorldSaveContinueTests.TryContinue_ReleasesTheHalvesThePreviousAttempt
  LeftArmed` FAILED with `Assert.False() Failure: Expected: False, Actual: True` (the dead attempt's
  world-entity arm was still armed after the second Continue) when the release hunk alone was reverted —
  the whole-tree pre-fix state no longer compiles, because the committed test files use the identified
  call shape. 1 failed of 1.
- **Green**: `WorldRestoreAuditTests` 18/18; the restore families (audit, replay, item contract, continue,
  composition, entity projection, world-fact port, run start, console) 116/116; full suite and gates
  below. `LiveWorldHalves_NameTheWritersThatAreActuallyArmed`, every pre-existing audit case and the
  whole restore family passed UNCHANGED around the new identity, which is what says the plumbing is
  behaviour-preserving.
- **The wiring is pinned where a test host can see it**:
  `WorldSaveContinueTests.TryContinue_OpensTheAccountForTheSameRestoreItStampedTheFactTablesWith` (a
  half carrying the kernel's restore sequence is counted, and the fact arm carries the same value),
  `WorldSaveContinueTests.TryContinue_ReleasesTheHalvesThePreviousAttemptLeftArmed` (the supersession:
  a world-entity arm stamped `7` and a native keypad armed by a dead attempt are both released with their
  reasons named, the stale entity half is NOT one of the halves the new restore owes, and the native arm
  does not ride this attempt's world-fact half), and `RestoredWorldItemContractTests` (its three audit
  cases now open the account for `kernel.RestoreSequence` AFTER the kernel restore, which is the order
  production runs; an arm stamped with anything else would leave the case without its report).
- **Adversarial pass (fresh context, no stake in the change)**: it could NOT falsify the attribution
  chain (every contribution is stamped before the write/commit, including the throw path and the "carried
  nothing" report), the account/arm agreement (one read of the counter, the wire checkpoint path is
  guest-only and cannot move a host counter, `ResetForSession` deliberately keeps it), the pre-existing
  audit guarantees (report-once, closed accounts, the no-Begin path, `LiveWriteAbandoned` completeness
  inside the audit) or the structure/gate claims. It DID find one MAJOR and three minors, all handled:
  the MAJOR is the presence-vs-identity hang fixed by the supersession above; MINOR (native handover
  attributed to the new attempt) is closed by the same release; MINOR (a session end releases the
  entity/native arms without a contribution, so an account that owed them stays awaiting — pre-existing,
  see the residuals) and MINOR (the red's stated form) are recorded below.

**NOT proven by the above**: that the window is reachable in a live session. The suite proves the
accounting is ATTRIBUTABLE, not that a straggler occurs in play — every writer is gated behind its own
pending flag and the seam runs on the main-thread pump, so a stale half needs a second restore to open
its account between another restore's arms and its report. The episode is defensive correctness for a
narrow window, and this ticket does not claim it was ever observed.

**Why the expectation did not have to become identity-aware.** The adversarial pass offered two fixes for
the hang: filter the expected halves by the arm's stamp, or release a stale arm before the account opens.
The release was chosen because the stamp filter would leave the OTHER half of the defect in place — the
seam writes whatever is armed, so a dead attempt's rows would still land in the new layer (the exact
thing the layer-end cancels exist to prevent) — and because after the release presence and identity agree
by construction: nothing is armed when the kernel restore arms this attempt's own halves. The count stays
"the writers actually armed" (`WorldRestoreApplier.LiveWorldHalves`), which is the contract its own test
pins.



**Recorded, NOT fixed — the residuals this pass leaves** (each with the scenario, so a later cycle can pick
one up without re-deriving it). **Moved to `review/restore-account-arm-release.md` on 2026-09-17** so they
stay in the work queue while this ticket waits in `review/`; the text below is the record as written, and
the re-verification that closed #4 as already-covered is in the new ticket:

- **A session end releases the world-entity and native arms without a contribution**, so an account that
  owed them stays awaiting: `WorldEntityKernelProjection.OnSessionEnded`
  (`WorldEntityKernelProjection.cs:72-73`) and `GameAdapterSessionBinding`'s session-end cancel release
  the arms with no report, while `ItemService.ResetSessionState` (`ItemService.cs:358`) does report its
  half. An account that expected three halves receives one and never completes. PRE-EXISTING (found by
  this pass's adversarial review, not caused by the identity change, and no worse with it: the next
  restore's `BeginRestore` reopens the account). Closing it needs the audit reachable from the
  projection (or the release routed through the save layer, which already has it).
- **A mismatch is silent once the account is closed** (`WorldRestoreAudit.cs:125-128`): the identity
  warning only fires while an account is OPEN, because a closed or abandoned account ignores every
  contribution by design. A late writer after a completed restore is therefore not named. Accepted: the
  rule that a straggler must not invent a report for a restore that already reported is the stronger one.
- **The supersession releases the arms only when a restore actually applies.** The release sits after the
  two content refusals (an unreadable or absent archive) and before the kernel restore, so a refused
  Continue leaves whatever the previous attempt armed in place; the next applying attempt releases it,
  and the pre-existing `AbandonRestore` paths cover a new run. Not a regression (a refusal never armed
  anything new), recorded so the invariant is stated exactly: "a new APPLIED restore supersedes the
  previous attempt". (The kernel-rejection branch below the release is unreachable today —
  `GameStateKernel.Restore` returns `Ok()` unconditionally — so the release's asymmetry there has no
  current trigger.)
- **The item release has no test of its own.** The entity and native releases are pinned by
  `WorldSaveContinueTests.TryContinue_ReleasesTheHalvesThePreviousAttemptLeftArmed`; the item half needs an
  `IItemControl` in the save fixture (the narrow `IRestoredWorldItemSource` port the gate reads is not the
  surface the release calls), and no test double for that interface exists — the release is the same
  statement as its two neighbours in the same block, verified by reading.



## S3.5 increment follow-up — the shared action verdict (2026-09-13)

Recorded gap 3 is closed: the shared trap/entity action library answered with a `bool`, so
`TrapVisualReplay.ReplayState` could not tell "the local copy already carries this state" (a duplicate
from the two-trigger race — the state the row names IS in the world) from "this copy cannot carry the
fact at all" (a divergence — the fact exists nowhere), and counted BOTH as a row the restore had
written. The adapter now returns a tri-state verdict and the Runtime owns the single rule that turns
it into the account's currency.

**The verdict** (`src/CasualtiesUnknownOnline.Runtime/Session/World/TrapActionOutcome.cs`):
`Applied` (the action wrote the transition), `AlreadyInState` (the local copy already carried it), and
`NotApplicable` (this entity cannot carry the fact). Every `return` in `TrapStateActions` /
`CrystalStateActions` names its reason, and the tri-state is COMPILE-enforced at both call sites —
`TrapVisualReplay.ReplayState` and `TrapEffectApplier.ApplyState` take `Func<T, TrapActionOutcome>`,
so a future action cannot quietly fall back to a bool. `CrystalMimicAccess.TryActivate` is where the
conflated case lived and now answers all three: no mimic effect on this crystal (or the latch member
the field contract expects is gone) is `NotApplicable`, an already-set latch is `AlreadyInState`, and
`ApplyCrystalMimic` passes that verdict through unchanged.

**The rule** (`Runtime/Session/World/TrapActionVerdict.cs`): `ReachedTheLiveWorld(outcome)`, where the
outcome is the action's verdict or NULL for "no entity of the expected kind at the position" — the two
observations every caller actually has. `Applied` and `AlreadyInState` reached the world; everything
else is REFUSED, and the reached set is written POSITIVELY so a follow-up outcome fails closed (counted
as refused, never as restored) and has to be classified deliberately. It lives in the Runtime because
that is where the account's currency lives
(`EntityEventSync.OnTrapStateProjected` → `LiveWorldWriteOutcome` → `WorldRestoreAudit`), and because
the rule is then machine-checkable without a game. A refused trap row reaches the report as that
half's refused COUNT (`GameRestoredWorldFactSink.ApplyWorldEntities` folds the three appliers'
counts), and the row itself — kind and position — is what the log line names; the adapter keeps only
what it OBSERVED (was an entity there, what did the action answer) and logs one line per verdict —
including the new "cannot be represented on the local entity — the fact is NOT in the world" warning,
which used to be mislabelled as a duplicate. The DESTRUCTIVE families (mine/turret/unstable-crystal
explosions) keep their consumption checks inline because they do not go through the action library, so
a future change to what "reached" means has to be applied there too (the review's nit 3).

| mechanism | change | evidence |
|---|---|---|
| the Runtime rule (the account's currency) | `TrapActionVerdict.ReachedTheLiveWorld(outcome)` — the action's verdict, or NULL for a missing entity: applied and already-in-state reach, everything else (cannot-carry, no entity, an undeclared verdict) refuses | `TrapActionVerdictTests` (7 cases: applied, duplicate, cannot-carry, no entity, two undeclared-verdict rows that must fail closed, and a `Enum.GetValues` sweep pinning that every declared member carries a deliberate verdict) |
| the shared action library's verdict | `bool` → `TrapActionOutcome` on all 29 actions (`TrapStateActions` 21, `CrystalStateActions` 8), each `return` naming its own reason | the type system (both call sites take `Func<T, TrapActionOutcome>`); the actions' game-typed bodies are read-only reviewed (see the limits below) |
| the conflated case | `CrystalMimicAccess.TryActivate` returns `NotApplicable` for "no mimic effect / missing latch member" vs `AlreadyInState` for "already consumed"; `ApplyCrystalMimic` passes it through | `CrystalBehaviour.SetUpEffects` rolls the effect list per crystal — the mimic is one of seventeen weighted effects (weight 8 of 139) and about seven crystals in ten are destroyed before any effect is set (`CrystalBehaviour.cs:83-102`, the mimic weight row at `:196`), so a crystal of the expected kind at the expected position CAN carry no mimic; the mimic's latch is `CrystalMimic.cs:52` |
| the over-claim the review found | `ApplyHeat` answered `Applied` after its toggle loop could not reach the requested state (an out-of-range `Extra` — the trigger side sends 0..2, `TrapLifepodButtonPatch.cs:25`); `LifepodHeatChanged` IS projectable durable state, so the account read that as restored | `TrapStateActions.ApplyHeat` now answers `NotApplicable` when `heatState != target` after the loop; read-only reviewed (the action needs a live `LifepodController`) |
| the replay path's verdict | `TrapVisualReplay.ReplayState` / `ReplayShuttleDoor` return the rule's answer instead of "an entity was found" | `TrapVisualReplay.cs` (`ReplayState`, `LogOutcome`, `ReplayShuttleDoor`); the restore account reads it through `EntityEventSync.OnTrapStateProjected` |
| the host apply path | `TrapEffectApplier.ApplyState` logs the three verdicts distinctly (it feeds no account — it applies a relayed event) | `TrapEffectApplier.cs` (`ApplyState`) |
| the row can actually reach the replay path | `CrystalMimicTriggered` IS a one-shot consumption (`EntityEventProfiles.cs:36`) → `WorldEntityState.Consumptions` → `WorldEntityKernelProjection.BuildFacts` (`:210-219`) for both roles | `EntityEventProfiles.cs`, `WorldEntityKernelProjection.cs`; `EntityEventProfilesTests` pins the classification table |
| the whole family | the two action libraries are the ONLY shared-action verdicts; the other appliers that feed the same account (`WorldBuildingEntitySync.OnOpenedEntitiesProjected` / `OnBuildingHealthProjected`) are idempotent by construction (health = 0 again / the same health rewritten), so their `false` is only "no entity there" — the case the account already refuses | grep for `Func<…, bool>` action parameters and `internal static class …Actions` in the adapter; `WorldBuildingEntitySync.cs:188-254` |

- **Red (recorded) — the gap itself**: the verdict type and the rule were threaded in FIRST as a
  behaviour-preserving step — the rule still answered `entityFound` (the old bool behaviour) while the
  outcome was already carried — so the red was a real assertion failure rather than a compile error.
  `TrapActionVerdictTests.ACopyThatCannotCarryTheFact_IsRefusedEvenThoughTheEntityExists` FAILED with
  `Assert.True() Failure: "an entity that exists but cannot carry the fact must be a REFUSED row the
  restore report names, never a restored one"` — 1 failed / 4 passed of 5 — and the four other cases
  passed, so the gap was demonstrated as the divergence case alone.
- **Red (recorded) — the review's fail-open finding**: after the adversarial pass, the same file gained
  `AVerdictTheRuleDoesNotKnow_IsRefused` first and was run against the still-fail-open rule:
  `(TrapActionOutcome)(-1)` and `(TrapActionOutcome)99` both FAILED with
  `Assert.True() Failure: "an undeclared verdict (99) must fail CLOSED — refused, not counted as
  restored"` — 2 failed / 5 passed of 7 — because `entityFound && outcome is not NotApplicable` counts
  every unknown value as reached while both log arms say it is not in the world. The reached set is now
  written positively (`is Applied or AlreadyInState`), and the entity/no-entity arm became the rule's
  own nullable input so the no-entity case flows through it instead of around it.
- **Green**: 7/7; full suite 3018/3018; normative gates 32/32; `dotnet format` exit 0 (the repo's
  `--verify-no-changes` is never clean — see `AGENTS.local.md`).
- **Adversarial pass (fresh context, no stake in the change)**: it could NOT falsify the live-path
  behaviour preservation (the live relay discards `Replay`'s bool — `EntityEventSync.cs:157` — and the
  only consumer is the restore path at `:185`; `ReplayShuttleDoor`'s live branch can only see
  `Applied`/`AlreadyInState` and both old log lines survive byte-comparable), the 22 `false →
  AlreadyInState` conversions (each checked against the decompile), the family audit, the account
  arithmetic (counted once, no double count, no forever-awaiting), the REACHABILITY of the fixed case
  (`EntityEventProfiles.cs:36` → `TrapStateRegistry.cs:70-81` → `WorldEntityKernelProjection.cs:210-219`
  → `ReplayState` — so the fix is not decoration), the mechanism cites, or the structure/wire claims.
  It DID falsify the absolute "every return is classified right": `ApplyHeat` could answer `Applied`
  without reaching the requested state (fixed above), plus three over-claims that stay recorded as
  residuals (below) and two doc-level nits (the "three of seventeen effects" bound; the "ONE rule"
  wording now naming the destructive family's inline checks). The no-entity rows it called
  unreachable are gone with the nullable input — that arm is now the production path.
- **Deployed identity**: the plugin folder on the machine carries this build — 34 DLLs, all 32
  build-produced ones hash-equal the build output and the two reference DLLs hash-equal `references/`;
  main `CasualtiesUnknownOnline.dll` `6E9BAE81`, `Runtime` `9204EC3B`, `GameAdapter` `CED4D909` (no
  sandbox copy of the plugin exists, so no shadow can serve an older build).
- **NOT proven**: the adapter's game-typed bodies cannot be instantiated in the test host, so WHICH
  reason each converted `AlreadyInState` names is pinned by reading the decompiled game paths and by
  the type system, not by a test — the same limit the rest of this domain carries. What a real
  regenerated layer does with a restored mimic row is the user's dual-client pass.
- **Recorded, NOT fixed here — moved to its own ticket** (`review/trap-action-divergence-hardening.md`,
  closed by that ticket's own cycle and decisions 175):
  the adversarial pass restated three over-claims of the same family with their reachability.
  `ApplyShower` would throw through `LifepodController.ActivateShower` (`LifepodController.cs:45-47`)
  if the controller had no shower (a serialized lifepod prefab member) instead of answering
  `NotApplicable` — and on the restore path that throw reaches `RestoredWorldFactReplay`'s catch,
  which marks the WHOLE live-world half incomplete and releases every handover (block/damage/keypad/
  geyser/recipe rows too), not just the shower row. `ApplyBioTerminal` answers `Applied` when the
  terminal's `BuildingEntity` component is missing, although the unlock itself is then not written.
  `ApplyCrystalShy` answers `Applied` when its 64-unit scan finds no neighbour, although no swap
  happened — that one is a different defect shape (a `true` that did nothing rather than a conflated
  `false`) AND it needs its own evidence question answered first: what the row's position means after
  a swap, and whether a late-joiner replay re-swaps the right pair. None is demonstrated reachable
  today, which is why they are recorded rather than patched in this cycle.


## S3.5 closure — exactly-once, documentation, re-anchoring (2026-09-17)

The staging decision named S3.5 as "exactly-once plus documentation and re-anchoring". This cycle
closed it as a VERIFICATION and DOCUMENTATION pass: no runtime code changed, so the runtime this stage
lands is exactly the runtime the earlier increments shipped.

**Scope closure, re-read against the tree rather than against this ticket's own staging narrative**
(the narrative still listed scopes 7-9 as open):

| Scope | State | Where it landed |
|---|---|---|
| 1 the cut seam | landed | S3.3 — format doc §4 |
| 2 payload completion | landed | S3.1/S3.2 — format doc §3.4 |
| 3 transient policy | landed | S3.3 — `WorldTransientPolicy` + format doc §4 |
| 4 determinism inputs | decided, no producer | S3.3 — `RandomStreams` stays empty by decision; keypad/geyser are captured as decided values |
| 5 exactly-once restore | landed | the claim below |
| 6 restore-report completeness | landed | S3.3, the ITEM arm follow-up, and scope 8's own refused count |
| 7 refusal recovery | landed | S4.4 (`review/save-interval-autosave-and-backup-recovery.md`) |
| 8 host-side world-entity projection | landed | the S3.5 increment |
| 9 solo menu-exit trigger | landed | S3.6 (`review/save-solo-menu-exit-trigger.md`) |

**The exactly-once claim at the level it is proven.** Scope 5's five parts — same-id dedup, no
re-materialization of generation-time content, one parent per container child, terminal facts never
resurrected, load-twice idempotence — each have a Runtime/format test named in acceptance rows 2 and 5
(`HostRestoreItemReconcileTests.ARegeneratedItemAtARestoredItemsSpot_DoesNotBecomeASecondWorldItem`,
`WorldSaveContinueTests.ContainerTree_AfterRestore_HasExactlyOneParentPerChild`,
`WorldSaveContinueTests.RemovedEnemy_StaysTerminalAfterRestore`,
`WorldSaveContinueTests.TryContinue_Twice_KeepsTheSameFingerprintAndFacts`). NOT claimed here, and not
provable by the machine: that the live scene shows exactly the saved set — acceptance row 2's in-game
half, which stays the user's pass.

**Re-anchoring.** Every test anchor the acceptance table cites was re-resolved against the test tree on
this commit — 54 `Type.Method` anchors plus 28 shorthand `.Method` anchors, 82 test anchors in all, all
present, 0 missing (the remaining backticked `.Name` tokens are field mentions, not test methods) — and
`SyncCoverageGateTests`'s 12 cases still check the sync-coverage evidence file's own 821 entries. The
ticket's historical sections keep the citations they were written with; the LIVE claims are anchored by
test names and quotes rather than by line numbers, and the two drifted `AGENTS.md` line citations in the
move conditions below are replaced with quotes.

## Acceptance

Read this table as TWO claims per row, because they are proven in different places:

- `machine` — a test, a gate or a structural contract in the Runtime/format layer. The named suites
  were re-run on `5b952bd` when this column was written: 288 cases across `Persistence`,
  `CommandConsoleSaveTests` and `WorldEntityProjectionTests`, 0 failures.
- `in-game` — a claim about what the Unity scene shows after the Continue click, which no test host can
  instantiate (the appliers call `Physics2D.OverlapPoint`, `TrapEffectApplier.FindTrap<T>`,
  `Object.Destroy`). These rows are the user's dual-client pass and are NOT claimed as observed here.

| # | Scenario | Expected | Verification |
|---|---|---|---|
| 1 | Mid-run save with mined/placed/quaked blocks + partial damage | Reload reproduces the same block diff exactly (compare against the pinned post-restore dump) | **machine**: both row shapes (a cell diff and the game's own `native-block-damage` row) round-trip (`WorldSnapshotWorldFactsTests.Codec_RoundTripsBothBlockRowShapes`, `.Codec_RoundTripsTheGameDamageRow`); a mid-run cut writes one typed row per fact while a layer-end cut writes both world files empty (`.Encode_MidRunCut_WritesOneTypedRowPerFact`, `.Encode_LayerEndCut_WritesBothWorldFilesEmpty`); the restored tables are applied ABSOLUTELY, never merged onto leftovers (`WorldSnapshotWorldFactsTests.TryContinue_AppliesTheRestoredWorldFactsAbsolutely`); a restored air row arrives with `SupportLossSettled` so a receiver does not re-roll the drops the saved world already rolled (`WorldRestoreSupportLossTests.RestoredBlockStateRows_ArriveMarkedAsSupportLossSettled`); and every row the game's own bounded 128-entry table refuses is named in the outcome (`WorldSnapshotWorldFactsTests.Restore_NamesTheRowsTheBoundedTableRefusedInTheOutcome`). **in-game: user pass** — that the replayed per-cell diff yields the same block map in the regenerated layer, and that the cracked/damaged sprites match |
| 2 | Mid-run save with world items on the ground, in containers, carried, worn | Same identities, locations, container trees; no duplicates, no loss | **machine**: identity/location/revision survive encode → decode for the whole item domain and the characters (`WorldSnapshotCodecTests.EncodeThenDecode_RoundTripsEveryDomainAndTheCharacters`); a container tree keeps exactly one parent per child after a restore (`WorldSaveContinueTests.ContainerTree_AfterRestore_HasExactlyOneParentPerChild`); a CARRIED record is the one that crosses a layer boundary (its `SaveLayerEnd` helper seeds a carried bag, and `TryContinue_Twice_KeepsTheSameFingerprintAndFacts` compares identity/location/revision per item); a terminal record is never resurrected (`WorldSaveContinueTests.RemovedEnemy_StaysTerminalAfterRestore`); a region the game regenerated at a restored item's spot binds to the restored id instead of being published beside it (`HostRestoreItemReconcileTests.ARegeneratedItemAtARestoredItemsSpot_DoesNotBecomeASecondWorldItem`, `RestoredWorldItemContractTests.ARestoredCut_ArmsTheReconcileAndPublishesTheRestoredSet`). **in-game: user pass** — the four restings as the player sees them (on the ground, inside a container, in hand, WORN — the kernel has no separate worn location, so the worn case is a scene-side claim), no duplicate beside a restored ground copy, and the corpse-loot bind in `CorpseScript.Start` |
| 3 | Mid-run save with opened/damaged buildings, consumed traps, fluids, enemies | Same facts; no re-trigger; no resurrection | **machine**: a consumed trap stays consumed across a restore (`WorldSaveContinueTests.ConsumedTrap_StaysConsumedAfterRestore`); the kernel's per-entity table reaches the host's own world-entry write instead of being dropped with the replaced layer (`WorldEntityProjectionTests.HostCheckpointRestore_ArmsTheWorldEntryWriteWithTheFactsTheGuestProjects`, `.SoloCheckpointRestore_ArmsTheWorldEntryWriteToo`), through the same three appliers the guest path uses, whose refusals reach the restore account (`RestoredWorldFactReplayTests.ApplyIfPending_WorldEntityRowsTheLayerDoesNotHave_ReachTheRestoreAccount`) — including a trap row whose entity IS there and cannot carry the fact, which the shared action verdict refuses (`TrapActionVerdictTests`); a restored death is applied as a REMOTE death, so the saved world's drops are not rolled twice (decision 172 + `WorldRestoreSupportLossTests`); opened entities, fluid regions and enemies round-trip (`WorldSnapshotCodecTests.EncodeThenDecode_RoundTripsEveryDomainAndTheCharacters`); the layer boundary drops the two layer-scoped tables of the layer being left while the player table and the enemy tombstones survive it (`LayerBoundaryKernelResetTests`, decision 174), and a `layer-end` restore lends none of those rows to the layer it regenerates (`WorldSaveContinueTests.LayerEndRestore_LendsNoReplacedLayerEnemyRowToTheNewLayer`, `.LayerEndRestore_LendsNoReplacedLayerFluidRowToTheNewLayer`) while a mid-run restore still restores them (`WorldSnapshotCodecTests.EncodeThenDecode_RoundTripsEveryDomainAndTheCharacters` runs that shape, and `WorldSaveContinueTests.RemovedEnemy_StaysTerminalAfterRestore` pins the terminal half). **in-game: user pass** — that the regenerated world actually shows the consumed traps, opened lockables and damaged buildings, and that no corpse/building drop was re-rolled. The appliers' game-typed bodies are static-reviewed only |
| 4 | Save during each in-flight state in the table above | The chosen policy applies and is logged; no silent loss, no duplication | **machine**: the policy is a real table with one verdict per class, pinned row by row (`WorldTransientPolicyTests.Rows_CoverEveryInFlightClassTheTicketNames`, `.Verdicts_PerRow_AreTheDecidedOnes`, `.Rows_AreUniqueAndEveryOneCarriesItsOwnerUnitAndReason`, `.Detection_DeclaresTheRowsNoObserverCanCount`); the seam defers the three frame windows with the request still armed (`WorldSaveCutSeamTests.BreakWindow_DefersTheCutAndKeepsItArmed`), takes the cut once the state resolves (`.BreakWindow_Resolved_TakesTheCutOnTheNextFrame`), names a window that outlasts `MaxFrames` instead of starving the request (`.BreakWindow_OutlastingTheDeadline_IsNamedInTheReport`), names counted drops (`.DroppedState_IsNamedWhileTheCutStillSucceeds`) and the game-owned `Standing` classes without claiming a count nobody has (`.StandingRows_AreNamedEvenThoughNoCounterCanSeeThem`), REFUSES an undeclared class (`.UndeclaredTransientClass_RefusesTheCutAndWritesNothing`) and refuses to write a clean-looking snapshot from an unreadable native table (`.UnreadableNativeTable_RefusesTheCutAndWritesNothing`); the runtime half of the observation is merged with the adapter half (`.RuntimeHalfOfTheObservation_IsMergedIntoTheCutReport`, `.Observation_MergesBothHalvesOfTheSameClass`), and the player-facing report is printed for a cut the player asked for (`CommandConsoleSaveTests.CutReport_IsPrintedForTheCutsThePlayerAskedFor`). **in-game: user pass** — that the deferral is invisible in play and the reported class list matches what the player saw |
| 5 | Save → load → save → load | Byte-comparable domain tables (modulo timestamps/revisions); world fingerprint stable | **machine**: restoring the same snapshot twice converges on the same item identity/location/revision, the same revision counter and the same run id (`WorldSaveContinueTests.TryContinue_Twice_KeepsTheSameFingerprintAndFacts`); a salvaged snapshot opens identically twice and leaves the live files untouched (`SaveArchiveSalvageTests.SalvagedSnapshot_OpensTwiceIdentically_AndLeavesTheLiveFilesUntouched`); the folder recovery pass is idempotent (`WorldFolderRecoveryTests.RecoveryIsIdempotent_ASecondPassHasNothingLeftToDo`); a manifest that lists the same file twice is not applied twice (`SaveArchiveContractFixesTests.ManifestListingTheSameFileTwice_IsNotAppliedTwice`); a float condition survives the text format exactly (`WorldSnapshotCodecTests.Encode_FloatCondition_RoundTripsExactly`). **Not machine-proven**: byte-level reproducibility of the produced JSON/archive itself — no test asserts that two encodes of the same checkpoint are byte-identical. **in-game: user pass** — a real save → load → save → load cycle in one session leaves the world fingerprint stable |
| 6 | Save taken mid-frame while a command batch is pending | The cut is consistent: no half-applied operation in the snapshot, revision matches the payload | **machine**: the manifest's `globalRevision` equals the checkpoint the payload was written from (`WorldSaveCaptureTests.MenuReturnCut_MenuReturnPhaseIsRecordedOnTheManifest`); the manifest records which seam took the cut (`WorldSaveCaptureTests.MenuReturnCut_WritesTheHostCharacterUnderTheSteamKey`, `WorldSaveCutSeamTests.ArmedCut_IsTakenAtTheSeamAndClearsTheRequest`); a layer-end-class cut cannot be armed at the frame-end seam (`WorldSaveCutSeamTests.LayerEndTrigger_CannotBeArmedAtTheSeam`); a request armed for a world a new run superseded is dropped (`.NewRun_DropsARequestArmedForThePreviousWorld`); a menu return supersedes a queued command cut with one snapshot and one reason (`.MenuReturn_SupersedesAQueuedCommandCut_OneSnapshotOneReason`). **Structural, not testable in this host**: "no half-applied batch" is the seam's POSITION — the CUO pump's last step, after every domain update and the frame's drop/break flushes (`docs/architecture/save-archive-format.md` §4, decision 167) — and the trigger only ARMS, so no cut runs inside the console callback. **in-game: user pass** — that nothing the player did appears half-applied after the restore |
| 7 | Restore of a mid-run snapshot | Resumes the *same* layer with all mutations — never a regenerated-but-different layer | **machine**: the run baseline (layer index, random state, biome, settings, the two rarity multipliers) rides `run.json` and is restored AS the run, so the layer the restore regenerates is the one the snapshot names (`WorldSaveContinueTests.TryContinue_RestoresTheKernelCheckpoint` asserts the restored `LayerIndex`; `WorldSnapshotCodecTests.EncodeThenDecode_RoundTripsEveryDomainAndTheCharacters` pins run epoch + global revision; `.Decode_RunBaselineWithoutGenerationState_IsRefused` and `.Decode_WithoutARunFile_RefusesTheWholeSnapshot` refuse a snapshot without it); the native run fields come back at their own seams (`WorldRunFieldTests.Continue_HandsTheRestoredRunFieldsToTheNativeApplier`, `.MidRunCut_WhileTheWorldIsAlreadyOnTheNextLayer_KeepsTheBaselineMultipliers`); a mid-run restore's in-layer tables are NOT erased by the layer-boundary reset (`RestoredWorldItemContractTests.TheRestoreGeneration_KeepsTheRestoredWorldTable`, `RestoredWorldFactReplayTests.ApplyIfPending_WithOnlyTheWorldEntitiesPending_WritesAndCommitsThatHalf`); and repair mode never changes `layerIndex` (`docs/architecture/save-archive-format.md` §6). **in-game: user pass** — that the regenerated layer actually IS the saved one (same layout, same mutations), which is the whole of the user's requirement |

### The seven rows, condensed

| Row | Machine coverage | User dual-client pass |
|---|---|---|
| 1 block diff + partial damage | row shapes round-trip, absolute apply, bounded-table refusals, support-loss verdict | the replayed block map and its sprites |
| 2 items | identity/location round-trip, container tree, exactly-once dedup, reconcile contract | the four restings on screen, no duplicate, corpse loot |
| 3 buildings/traps/fluids/enemies | kernel facts + host/solo world-entry write + refusal account + remote-death rule | the regenerated world's entities and their drops |
| 4 in-flight policy | policy table, deferral + deadline, undeclared-class refusal, both report surfaces | deferral invisible, reported classes match |
| 5 save/load/save/load | idempotent restore, identical reopen, idempotent recovery, no double apply | a real cycle's fingerprint stability |
| 6 mid-batch cut | revision pin, seam recorded, seam rules asserted | nothing half-applied visible |
| 7 same layer | baseline + layer index restored, that generation's reset skipped | the layer really is the saved one |

## Verification limits

Item/entity/block facts are machine-verifiable through the kernel and the format layer. Native world
tables (keypad codes, geyser rolls, earthquake timers, `WorldGeneration.blockDamages`) live behind the
adapter; those rows are verified by adapter-level tests plus the user's dual-client pass, and this
ticket names which is which.

Two claims decide whether this stage is accepted, and neither is provable by the machine:

1. **Exactly-once as the player sees it.** The kernel's dedup, the container-tree single-parent rule,
   the reconcile contract and the restore idempotence are pinned (rows 2 and 5 above), but "the
   restored world shows exactly the saved set" is a statement about the live scene.
2. **Same-layer regeneration.** The run baseline is restored and the layer-boundary reset is skipped
   for that generation, but whether Unity's regeneration from that baseline produces the layer the
   player was standing in is a fact about the game, not about the archive.

## Conditions for moving to `review/` (self-check, not a gate on the user)

`review/` is the waiting state for the single unified acceptance pass
(`docs/backlog/README.md`: code-complete items move there immediately, per-ticket acceptance is
explicitly not required, and only the final unified pass moves a ticket to `done/`). So the boxes
below are the DEVELOPMENT half of "we owe nothing but the acceptance pass" — the in-game rows in the
matrix above are NOT a precondition for the move, exactly as S2 landed its continue flow with its
in-game rows open.

- [x] **Scope closure**: scopes 1-6 and 8 are landed and documented here; scope 7 is owned by S4
      (`review/save-multiplayer-restore-and-backups.md`) and landed with S4.4; scope 9 is owned by S3.6
      and landed (`review/save-solo-menu-exit-trigger.md`). No scope is silently dropped — evidence:
      the scope-closure table in *S3.5 closure* above, re-read against the tree on this commit.
- [x] **The exactly-once claim is stated at the level it is proven**: the machine evidence above is
      claimed, the in-game half is named as the user's pass and is not claimed as observed
      (`AGENTS.md`: "No self-assumption: every claim needs source evidence (the path plus the quoted
      text, never a line number) or runtime evidence.") — evidence: the *exactly-once claim* paragraph
      above names the four test anchors; all 83 acceptance anchors re-resolved on this commit.
- [x] **Each of the four recorded gaps is fixed in this stage, deleted from the stage's scope with a
      reason, or explicitly deferred BY THE USER** and recorded here as deferred for the unified pass
      — never silently reclassified as "future work" (`AGENTS.md` Development Workflow step 8: "Keep
      incomplete or unverified work open. Do not claim completion, do not reclassify known gaps as
      future, and do not move to `review/` until the exact scenario and full acceptance matrix are
      verified.") — evidence: all four are FIXED in this ticket (1 the restore ATTEMPT identity,
      2 the ITEM arm of the entry gate, 3 the shared action verdict, 4 the sibling-domain layer reset),
      each with its red/green pair recorded in the section it names:
      1. `WorldRestoreAudit` carries no restore identity (an epoch on the account), so a very late
         writer could credit a newer restore's account; the window needs a writer that reports across
         a `BeginRestore`. — **FIXED 2026-09-12** (the restore ATTEMPT identity: the kernel's
         `RestoreSequence` is stamped by every arm and echoed by every contribution, and an account
         ignores a half of another attempt; red/green and the wiring pins are in *the restore ATTEMPT
         identity* above).
      2. the world-entry seam's `HasPending` gate does not consult the ITEM arm — **FIXED 2026-09-12**
         (the narrow `IRestoredWorldItemSource` port, the red/green pair and the family check are in the
         follow-up section above).
      3. a shared-action `false` that means "not applicable" is counted as applied by
         `TrapVisualReplay.ReplayState`, so that divergence can be under-reported. — **FIXED
         2026-09-13** (the shared action verdict: `TrapActionOutcome` plus the Runtime's
         `TrapActionVerdict`; the red/green pair, the family audit and the verification limits are in
         *the shared action verdict* above).
      4. the sibling-domain reset family: host-only kernel resets, no layer boundary reset for the
         enemy/fluid/player tables, and those reset commands stay wire-reachable. — **FIXED 2026-09-13**
         (the layer-boundary reset family: enemy and fluid join the world-entry reset, the player table
         is not in the family, and the four layer-scoped reset commands lost their wire form; the red/green pair, the
         family audit and the residuals are in *the sibling-domain layer reset* below).
- [x] **Development verification trail is on `master` for the commit being moved**: the named suites
      pass on it, `dotnet format` is clean, and the full suite + normative gates are green —
      evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; full suite 3 202/3 202
      (`CasualtiesUnknownOnline.Tests`) and 32/32 normative gates on this commit; the focused
      save/restore + world-entity run is 376/376.
- [x] **Deployment identity**: the plugin folder on the machine carries that commit's build (plugin
      DLL hash equals the build output's, BepInEx-family DLLs excluded), so the later unified
      acceptance run exercises it rather than an older build — evidence: this cycle changes no runtime
      code, so the deployed runtime IS this commit's runtime; the pre-move deployment was `c8754f36`
      (33/34 deployed files matched that build's hash set, `steam_api64.dll` the allowed native
      payload), and the delivery rebuild + `tools/verify-deploy.ps1` are run on this commit's own build
      as the last step of the cycle, because the plugin embeds its sha in `ProductVersion` and a commit
      cannot deploy itself.
- [x] **The last increment's independent adversarial pass is recorded** in this ticket, with every
      blocker/major either fixed or recorded as a residual — evidence: the S3.5 increment pass, the
      ITEM-arm pass, the restore-ATTEMPT-identity pass and the shared-action-verdict pass are all
      recorded above with their findings and fixes, and the residuals that could not be fixed in this
      stage are moved to `review/restore-account-arm-release.md` so they stay in the work queue — where
      the independent re-verification of 2026-09-17 found the recorded "the item release has no test"
      residual already covered by
      `RestoredWorldItemContractTests.ACancelledReconcile_ReportsTheLossInsteadOfWaitingForever`, leaving
      the entity/native session-end contribution and the item port wiring as the live gaps.
- [x] **The ticket moves with its acceptance table and the README index line** in the same commit,
      and the move does NOT claim the in-game rows: `review/` means they wait for the unified pass.
      The user-facing acceptance procedure for that pass stays a user action, not a repo artifact —
      the automated version of it is already deferred by decision
      (`future/adapter-shell-verification-harness.md`) — evidence: this commit moves this ticket from
      `todo/` to `review/` together with its acceptance table, its umbrella
      (`review/save-system-mid-run-and-layer-end.md`) and both README index lines, and the in-game half
      is stated as the user's pass rather than as observed.