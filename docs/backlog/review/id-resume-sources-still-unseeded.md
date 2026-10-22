# The id resume reads one source: the synthesized-item counter and the character rows are not seeded

- Status: Review (landed 2026-10-11; awaiting the acceptance batch). Both gaps were reproduced as failing
  cases on the unfixed tree through the real save/continue seam before anything was changed
  (`.acceptance/20261011-h/red-id-resume-sources.md`), and the acceptance rows below are what the batch judges.
- Priority: Medium
- Category: Remote inventory / item identity / persistence
- Source: found by the independent adversarial review of `done/item-id-counter-is-not-resumed-by-a-restore.md`
  (2026-10-11). That landing resumes every id space from the restored archive's item rows; the review asked
  the completeness question of the rule itself ("does the restore carry EVERY id this process will
  re-stamp?") and found two sources the seed does not read.
- Related: `done/item-id-counter-is-not-resumed-by-a-restore.md` (the landing and its rule),
  `Runtime/Session/Items/ItemInstanceIds.cs` (the format owner),
  `Runtime/Session/Items/IRestoredItemIdSeed.cs` (the port this ticket adds),
  `docs/architecture/save-archive-format.md` §3.4 (what a cut holds)

## The two gaps, reproduced before they were fixed

Both were driven the way the game drives them: one "process" plays a world through the production
composition root and takes a mid-run cut, a SECOND one opens the same archive and continues the world,
so the per-process counters really restart (that second process is modelled in-process — a fresh
composition root over the same archive root is what makes the counters restart — so this is a model of
the relaunch rather than a second OS process, and the world lease sees one PID). Two cases in
`ItemIdCoordinatorTests` failed on the unfixed tree and pass now (the full red is recorded in
`.acceptance/20261011-h/`):

```
AContinuedWorld_ResumesTheSynthesizedNamespace_AboveTheAwardedIdsTheArchiveReissued [FAIL]
  the resumed counter must be above the archive's own (5), got 0 (9223372036894511081)
AContinuedWorld_SeedsAGuestSpaceFromItsCharacterFile_WhenTheKernelNeverLearnedTheId [FAIL]
  Assert.Single() Failure: The collection was empty
```

1. **The host's synthesized-item counter** was per process and nothing resumed it.
   `OtherMedicalRemovalApplier` minted `AwardedHighBit | counter | space` from its own field, so the first
   removal after a continue restarted at `AwardedHighBit | 0 | space` — below whatever counter the
   archive's own awards had reached, and, whenever the world's lowest awarded id is counter 0 (the
   ordinary shape of a first award), literally an id the restored world already holds: the awarded item is
   a real carried item, so it is in the cut's item rows AND in the operator's character file. The resume
   skipped those ids on purpose (reading their counter as the plain one would push the plain allocator
   into that namespace), and nothing took them up. The case above pins the unresumed counter; the parent
   ticket's real-machine probe is the run that showed the collision itself.
2. **The character rows** (`characters/<playerKey>.json`'s `CharacterItemMsg.InstanceId`) were not read at
   all. For a mid-run cut the two carriers usually agree, but the subset relation is not structural: the
   character record is filled by the live 1 Hz report (`CharacterDataHandler` →
   `CharacterDataStore.SaveCharacterData`, which creates no kernel row), while the kernel learns a guest's
   carried ids through the carried-registration cadence (dense-then-60 s). An id that reached the
   character file inside that window was seeded for nobody, and the member whose process restarted
   resumed below it — then re-issued an id its own restored character wears.

## What landed

| # | What the rule needed | Where it lives |
|---|---|---|
| 1 | the synthesized namespace's counter and its resume point owned by ONE object, beside the plain watermarks | `ItemIdCoordinator` (`_nextAwardedCounter`, `NextAwardedItemId`, `ResumeAwardedIds`) |
| 2 | the awarded counter resumed from the archive's own awarded ids, per id space (only the LOCAL space can be this host's: an awarded id is minted by the host that plays the world, and a case pins that a foreign space's awards leave this counter alone) | `ItemIdCoordinator.ResumeRestoredIds` → `ResumeAwardedIds` |
| 3 | the format's decode half for the synthesized namespace (bit 63 masked off before the counter is read) | `ItemInstanceIds.AwardedCounterOf` |
| 4 | one consumer, reached directly instead of by wiring that can go missing | `OtherMedicalRemovalApplier.GrantRemovedItemToOperator` → `ItemIdCoordinator.NextAwardedItemId` |
| 5 | the seed reads BOTH carriers of an archive's ids — the checkpoint's rows and every character's carried ids, container contents included | `RestoredItemIds.Of(checkpoint, characters)` (the carrier reader), called from `WorldRestoreApplier.TryApply` |
| 6 | one call, at the one seam that holds both carriers, before anything can allocate | `WorldRestoreApplier.TryApply` → `IRestoredItemIdSeed.SeedRestoredItemIds` → `ItemService.SeedRestoredItemIds` → `ItemIdCoordinator.ResumeRestoredIds` |
| 7 | the save layer reaches the rule through its own port, never the handler surface behind it | `IRestoredItemIdSeed`, registered as its own port in `ItemComposition` (the world-item half rides `IItemControl`, which extends `IRestoredWorldItemSource`; this one has no such host interface to ride) |
| 8 | the rule's call site no longer claims to be the kernel event (a checkpoint is only one carrier) | `ItemKernelProjectionWiring.OnCheckpointRestored` keeps the restored-world-item half only |

## Why the seam moved (and why nothing else resumed a kernel)

`ItemIdCoordinator.ResumeRestoredIds` used to be called from
`ItemKernelProjectionWiring.OnCheckpointRestored`, i.e. from inside the kernel restore, which sees the
checkpoint and nothing else. An archive writes instance ids in two places, and only
`WorldRestoreApplier.TryApply` holds both (it decodes the checkpoint AND the characters). The move costs
no coverage on the host: the ONLY host/solo caller of `ItemKernelAuthority.Restore` is that method
(`GuestCheckpointReceiver.Restore` is the guest path, and a guest must NOT seed its own counters from the
host's archive — its resume point arrives as the host's watermark grant). The rule is raise-only and
idempotent, so a superseding restore handing a larger set over needs no bookkeeping on either side.

## The family sweep (nothing else in this family is left unseeded)

The family is "an id a restore re-stamps while a per-process counter restarts". Every member was walked:

- **plain item ids** — resumed in both directions by the parent landing (the local event, the member
  grant) and now fed from both carriers;
- **synthesized (awarded) item ids** — this ticket: their own counter, resumed from their own ids;
- **network entity ids** (`EntitySyncService`) — structurally immune: the id is
  `(wall-clock epoch, counter)`, and the epoch is `_time.UtcNowTicks`, so a restarted process cannot mint
  an id a restored world holds;
- **medical operation ids** (`MedicalOperationIdAllocator`) — session-scoped and never persisted; the
  sessions themselves are cut-time transients (`WorldTransientPolicy`), so no archive re-stamps them.

## Acceptance matrix

| # | Scenario | Expected | State |
|---|---|---|---|
| 1 | Save a world in which a splint was removed (an awarded id exists), continue it in a new process, remove another splint | The new awarded id is above the restored awarded ids of that space, and no kernel refusal follows | pending |
| 2 | The same, read from the archive: the awarded ids in `items.json` and in the operator's character file | Both carry the same ids, and the resume point covers them | pending |
| 3 | A guest's carried id that reached the character file while its kernel registration was still pending | The restore's seed covers it | pending |
| 4 | Continue a world with a guest in the lobby whose space the archive seeded, and one whose space it did not | The present member's grant carries the restored mark; the other member's does not | pending |

The four rows are the batch's judgement; the in-process suite above is the development evidence, and it
covers rows 1–3 in shape (the splint removal itself, the archive read, and the character-file-only id,
nested contents included). Row 4's "the other member's does not" half is covered in shape too (the
present-member case asserts the untouched other member).

## Limits recorded with the landing

- **A member whose process restarted mid-session is still bounded by what the host last recorded**: the
  archive seeds a world CONTINUE, and a crash inside a running session is not one (the same limit the
  parent ticket records — a watermark report swallowed in the window before the crash grants a stale value).
- **The awarded counter is resumed for the LOCAL id space only.** That is the complete rule, not a
  shortcut: the synthesized namespace is minted host-side, so an archived awarded id of another space
  belongs to the host account that played the world before this one — a different space cannot collide
  with this side's awards, and resuming a counter that is not ours is the mistake the plain half already
  pins.
- **A host-side second restore path would have to seed too.** Today `WorldRestoreApplier.TryApply` is the
  only one; the seam is stated in `IRestoredItemIdSeed` and in the wiring's comment so a new path reads
  the rule where it would have to call it.
- **The seed port is optional, and a composition root that omits it resumes nothing in silence.** It is
  taken as `IRestoredItemIdSeed? restoredIds = null` like the item and world-entity halves, and unlike
  them it is NOT part of the restore's live-write account (`WorldRestoreAudit`): those halves are writes
  that land later and can be owed, while the resume is applied synchronously at the click, so there is no
  live write for the account to await. The production root always resolves it
  (`SaveArchiveComposition` → `ItemComposition`), so the exposure is a new composition root, not a
  runtime state.
- **The rule's premise is that the local identity is known at the click.** The seed addresses "this
  side's" space through `_session.LocalSteamId`; in IP-direct mode that id is the transport's peer id and
  is 0 until the transport is active (`IpDirectSteamService.LocalSteamId => _isActive ? ... : 0`), so a
  Continue taken BEFORE hosting would find no local space and resume nothing, with no line saying so. The
  plain half of the rule has had the same dependency since the parent landing, and whether the UI can
  even reach that order is not decidable by reading (the Steam path is unaffected: its local id is set at
  Steam init). Left as stated rather than patched blind; a real-machine Continue row is what would
  decide it.
- **The two carriers can disagree about the SAME id** (the 1 Hz character report and the kernel command
  for one item can land in different windows, so the checkpoint row and the character row may place it
  differently). This landing makes the seed read both, which is about WHICH ids exist, not about that
  disagreement; the restore has no rule for it, and its reachability is unproven. Filed with the finding
  as `done/id-carriers-can-disagree-about-placement.md` by the adversarial review of this cycle, where
  the cut now settles it against the kernel.

## What the adversarial review changed in this commit

The independent review ran against the frozen tree before the commit, and its findings were fixed in the
same commit rather than filed:

- **two gate failures** — the backlog index row was over its 160-character budget, and the checklist's
  "plan approved" evidence quoted the ticket's OLD `todo/` path (the reference gate resolves those paths);
- **a stale architecture claim** — `docs/architecture/save-archive-format.md` §6.1 still described the
  kernel-restore seam and the "synthesized ids are skipped" rule this landing replaced, so the format page
  now states the two carriers and the synthesized counter;
- **stale in-file claims** — `ItemInstanceIds`' own doc named `OtherMedicalRemovalApplier` as a composer
  and "the archive's own rows" as the seed's source, both untrue after this change;
- **an over-stated red** — the synthesized case's archive holds its awarded id at counter 5, so what the
  case pins is the unresumed counter, not a collision with a live id (the collision is the parent
  ticket's real-machine probe); the ticket and both artifact records now say exactly that;
- **a coverage gap** — the "local id space only" rule had no case, so
  `AContinuedWorld_IgnoresAnotherAccountsAwardedIds_WhenResumingTheSynthesizedCounter` was added and the
  suite now holds 9 cases;
- **a text regression it caught in this cycle's own edits** — the test file's comments had been mangled by
  a shell write that round-tripped through a non-UTF-8 encoding (22 em dashes and three line breaks);
  repaired, and the file is UTF-8 with the repository's CRLF endings;
- **three limits it asked to be stated rather than left implicit** — the seed port is optional and a
  composition root omitting it resumes nothing silently (it is not part of the live-write account, because
  the resume is synchronous at the click while those halves are writes that land later); the rule's
  premise that the local identity is known at the click (IP-direct mode); and the placed-differently
  question above.
