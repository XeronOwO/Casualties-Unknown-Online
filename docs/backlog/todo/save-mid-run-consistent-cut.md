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
  writer's split out of `WorldSaveService`. S3.4 (native run fields) and S3.5 (exactly-once plus
  documentation and re-anchoring) are next; the mid-run trigger is now OPEN, so a build produces
  both the S2 layer-end cut and the frame-end mid-run cut.
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

   **Still open after S3.3** (the seam and the transient policy did not touch it): a mid-run restore
   on the HOST still leaves opened/consumed/damaged-building facts in the kernel without writing them
   onto its own freshly generated world. The rule and its proof stay in S3.5, together with the
   drops of a building the saved world already killed.
9. **Solo menu-exit trigger** (found by the S3.3 adversarial pass) — the deliberate menu return is
   requested from session-teardown events and decided by `RunMenuReturnPolicy` for a HOST, so solo
   play (no session, no role) gets no menu-return cut at all; `/save` is the only mid-run trigger
   there. Owner: whichever stage owns the solo surface (S3.5 or the multiplayer-restore stage);
   the fix is an in-world → menu transition edge in the run coordinator that requests the same seam
   cut, not a second cut path.

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

