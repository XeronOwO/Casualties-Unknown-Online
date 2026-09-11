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
  boundary cancels a handover that belongs to the layer being replaced. S3.3 (the consistent cut and
  the transient policy) is next,
  then S3.4 (native run fields) and S3.5 (exactly-once plus documentation and re-anchoring). The
  mid-run trigger is still closed, so the only cut a build produces today is S2's layer-end one.
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

## Scope

The consistent cut and the full mid-run payload. This is where the hard part of the requirement lives.

1. **The cut seam** — one point on the host main-thread pump where the kernel revision, every domain
   table and the native world tables are read at one instant, with no command batch or frame flush
   interleaved. The manifest's `cutPhase` names the phase; the format doc §4 lists the phases.
2. **Payload completion** — `world-blocks.json` (host block difference table
   `WorldStateMessageService._damagedBlocks` + `BlockDamageRegistry` partial damage + the native
   `WorldGeneration.blockDamages` list) and `world-transients.json` (the explicitly chosen transient
   set), on top of the S2 domain files. The native list needs its own discriminator (or its own kind)
   when it lands: both tables are capped (CUO's registry at 256, the game's list at 128) and a
   restore has to route each row back to the table it came from, never merge them into one.
   S3.1 deliberately left it out for that reason; the Runtime half of both files landed there.
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
4. **Determinism inputs** — populate `GameCheckpoint.RandomStreams` in production
   (`GameStateStore.CreateCheckpoint` passes `null` today) if any domain's restore decision depends on
   them, and decide the same for keypad codes and geyser rolls; the save must carry enough baseline to
   reproduce the world.
5. **Exactly-once restore** — same-id dedup, no re-materialization of generation-time content, container
   children with exactly one parent, terminal facts never resurrected, load-twice idempotence.
6. **Restore-report completeness** (found by S3.1's review rounds) — the world-block tables are
   BOUNDED (CUO's partial-damage registry at 256 cells, its block-diff table at 65536), and the
   apply path can also drop a row whose payload is unreadable. Today those drops reach the log but
   not `WorldContinueOutcome.Summary`, so a restore can report success while a row was dropped.
   Make `IWorldFactSource.ApplyFacts` return what it applied/dropped (or expose the table counts)
   and fold that into the restore report, so §6's "every dropped entry is surfaced" holds for the
   world facts too.
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

