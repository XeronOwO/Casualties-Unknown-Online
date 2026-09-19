# Recipe unlock has no fallback or backfill

- Status: Review
- Priority: Medium
- Category: Network / sync coverage / crafting
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row I6)
- Related: the crafting report half is healed by the item keyframe/character snapshot (matrix rows I3/P3); `docs/decisions/active.md` #186; `todo/sync-cadence-review.md`

## Problem (evidence)

`RecipeUnlock` (NetMsg 77) is one-shot in both directions:

- Guest report: `_sender.Send(_session.HostSteamId, NetMsg.RecipeUnlock, new RecipeUnlockMsg { RecipeIndex = recipeIndex });`
  in `src/CasualtiesUnknownOnline.Runtime/Session/Items/CraftSyncService.cs`, triggered by
  `_craft.SendRecipeUnlock(blueprint.recipeIndex);` in
  `src/CasualtiesUnknownOnline.GameAdapter/Items/CraftingSync.cs`.
- Host relay: `CraftSyncService.FireRecipeUnlockReceived` (relay, source excluded); the apply is a
  per-process static write (`recipe.INT = 0;` in
  `src/CasualtiesUnknownOnline.GameAdapter/Items/RecipeUnlockApply.cs`).
- No periodic re-send and no world-entry / checkpoint member: the ordered world-entry group
  (`src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntryFanout.cs`, both `Send` and
  `SendInSessionRepair`) has no recipe entry, so a late joiner never learns an unlock that happened
  before it joined.
- `CraftReport` itself (NetMsg 76) is better off: the host adopts untracked carried entries and the
  durable item state is healed by the 5 s item keyframe (row I3) and the 1 Hz character snapshot
  (row P3). The gap is the unlock state.

Re-checked against the tree on 2026-09-19, before implementing: both send sites of the world-entry
group still carried no recipe entry, `SendRecipeUnlock` was still the only sender, and the unlock was
still the game's own static write — the reported gap was still open. The native semantics were
re-read as well: the unlock is `recipe.INT = 0` inside the blueprint's own `useAction`
(`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`, the `blueprint` item's delegate), the table is
rebuilt per run by `Recipes.SetUpRecipes()` (`reversing/Assembly-CSharp/Assembly-CSharp/Recipes.cs`,
called from `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs` in `Awake`), and
`Recipe.visible` is `specialKnown || skills.INT - INT >= -3` (`Recipe.cs`) — which is why "INT == 0"
is the crafting list's own notion of "needs no skill".

The divergence is user-visible, not cosmetic: `recipe.INT` is ALSO the crafting margin the outcome is
computed from — `RecipeResult.SpawnResult(recipeInt)` derives `skills.INT - recipeInt` and returns a
reduced-condition result for -1, produces NOTHING half the time for -2, and injures the crafter for -3
(`reversing/Assembly-CSharp/Assembly-CSharp/RecipeResult.cs`), while `Recipe.visible` gates whether the
recipe is listed at all. A peer whose table still holds the recipe's original INT therefore cannot see
the recipe, or crafts it at a worse outcome, than the side that learned it.

## Goal

An unlocked blueprint converges on every peer (host and guests) without a
reconnect, including late joiners, while the host remains the authority over
which recipes are unlocked.

## Design direction (decide at implementation)

1. **Unlock set in the backfill group** — carry the unlocked recipe-index set in
   the world-entry group / kernel checkpoint (or a small absolute message next to
   it) and re-send it on the existing 60 s cycle. **TAKEN**, as its own message
   (`RecipeUnlockSnapshot`) rather than a checkpoint member: the table is a game fact the Runtime
   cannot read (it goes through `INativeWorldFacts`), not kernel state, and the checkpoint is the
   run baseline, whose shape the kernel owns.
2. **Hash/dirty re-request** — the guest sends its unlock hash; the host answers
   with the authoritative set when it differs. Not taken: it heals only the host → guest
   direction (acceptance row 1 needs a swallowed GUEST report to reach the host too), and a hash
   would add a third comparison shape beside the sets both sides already hold.
3. **Accepted loss** — rejected: a missing blueprint changes the crafting list,
   a user-visible divergence.

## What landed

**1. The absolute unlock SET is the fact, and both halves read it from the game's own table at
send time.** No CUO mirror exists: `INativeWorldFacts.CaptureUnlockedRecipeIndexes()` is a new
narrow read on the port that already owns the recipe table for the save layer, and the adapter
implements it over `RecipeUnlockTable.CaptureUnlockedIndexes()` (the same traversal the save path
uses, selecting the `INT == 0` rows). The read refuses when there is no live world — the static
`Recipes.recipes` OUTLIVES a run (the game rebuilds it in `WorldGeneration.Awake`), so reading it in
the menu could report the previous run's unlocks — and when the table is not built. A refusal is
null, never an empty set: an empty set means "nothing is unlocked" and asks for no write.

**2. The host's backfill rides the world-entry group and the 60 s in-session repair.**
`WorldEntryFanout` takes `ICraftControl` and calls `SendRecipeUnlockSnapshot(steamId)` in both
groups — after the kernel checkpoint, with the other absolute tables, and in `Send` before the
completion marker — so a member that joined after an unlock learns the whole set exactly once and a
swallowed entry send is re-sent by the repair group. The set is deliberately NOT generation-stamped:
its keys are recipe indices, the table is a RUN fact, and an unlock only ever adds, so a set read in
an older layer still asks for nothing but writes the receiver already holds. An empty set is not
sent; a set that cannot be read is logged and skipped.

**3. A guest's own set is the swallowed-report half, on the shared fallback cadence.**
`SendRecipeUnlock` (guest) arms the index BEFORE its session check — an unlock made while the session
is still coming up must still go out — and `PumpRecipeUnlockFallback(nowMs)` feeds
`PendingReportFallback` (the 60 s window every other report fallback uses), driven by
`WorldReportFallbackPump` beside the block-state, damage and runtime-entity channels. The re-report
carries the guest's ABSOLUTE set. An index leaves the unconfirmed table when the host's set CARRIES
it, and its re-report is BOUNDED: each index has a budget of three fallback windows
(`MaxRecipeSetReports`), after which it is dropped and NAMED in a warning. That bound is what keeps a
genuinely divergent index — one this host's recipe table does not have, so its set can never carry
it — from re-reporting every minute for the rest of the session; the first window is what heals a
swallowed send, so what the budget drops is the residue the host refused, not a lost report. A fresh
unlock of an index starts its budget over, and an index the arrived set does not mention is never
treated as confirmed.

**4. The host merges a guest's set through the ordinary unlock path.**
`FireRecipeUnlockSnapshotReceived` on the host computes the difference against its own live set and
feeds exactly the indices it had not learned through `FireRecipeUnlockReceived` — apply locally (the
adapter's write, with the game's alert only on a real learn) plus relay to the other members, source
excluded. So there is still ONE apply path, a repeated set costs one comparison, an index whose
recipe this host does not have is refused by the same apply the live report uses, and a set that
cannot be compared (no live table) is NOT applied blindly: the reporter's entry stays pending and
comes back on the next cycle.

**5. A backfill applies silently, and an open crafting list refreshes.** The guest's batch apply
writes `INT = 0` for every index with NO per-recipe alert — the Runtime raises only the set event,
and the alert lives on the per-index event (`RecipeUnlockApply.OnRecipeUnlockReceived`), so a late
joiner is never handed the whole run's alerts. Both the live and the backfill paths now refresh the
crafting list when it is open — the native branch does the same right after its own write (`Item.cs`,
the blueprint `useAction`: the alert, then `OpenCraftScreen`/`RefreshRecipeList`), so a remote unlock
can no longer be unlocked in the table but missing from the list a player is looking at.

**6. Protocol 30 → 31** (`ProtocolVersion.Current`, its new 31 entry, `docs/decisions/active.md`
#137 and the new #186). The message is `NetMsg.RecipeUnlockSnapshot = 139`, bidirectional with one
payload shape (`RecipeUnlockSnapshotMsg.RecipeIndexes`), registered through
`[PacketHandler(NetMsg.RecipeUnlockSnapshot, NetMessageDirection.Bidirectional)]`.

**7. Tests.** `tests/CasualtiesUnknownOnline.Tests/Items/RecipeUnlockBackfillTests.cs`, one new
class (10 cases, `[Trait("Category", "Integration")]`): the late joiner's entry-group set with no
per-recipe event, the empty set not sent, an unlock made after the entry riding the repair group, the
host with no live table sending nothing, a swallowed guest report converging through the guest's set
re-report (third party included), the host's set ending the re-report, the merge relaying only what it
had not learned (a repeat being idempotent), the host with no live table not judging a guest's set,
the unconfirmed index stopping after its budget instead of re-reporting forever, and a later unlock
re-arming that budget. The suite fakes the live table per NODE (the real table is a per-process
static) and mirrors the adapter's write onto it, because the port reads the very table that write
touches. The direction contract is updated with it: the member is declared in the bidirectional table
that `BidirectionalDirectionTests.Bidirectional_AllowedOnBothSides` (the theory over that table) and
`DirectionClassificationTests.EveryNetMsg_IsExplicitlyClassified` both read — the review found this
missing (item 9).

**8. Evidence and matrix.** Row I6 moved from `Event-only gap` to `OK`: its cells now carry the set's
direction, event, fallback, backfill and loss rules, the vocabulary index lists
`NetMsg.RecipeUnlockSnapshot`, the gap list records the closure, and the verdict summary moved
(`OK` 50 → 51, `Event-only gap` 5 → 4). The evidence file gained 16 anchors for the row (11 → 27) and
its declared count followed (880 → 896); the stale W1 anchor that quoted
`public const int Current = 30;` was re-pointed at 31, and so were the two documents #137 names
(`docs/api/mod-api.md` §7, whose list now names this family, and the previous cycle's ticket).

**9. Independent adversarial review** (one round, fresh context, frozen tree; verified unmodified
before and after). It reproduced every number (focused 8/8, the neighbour decomposition 10 + 12,
gates 56/56) and confirmed the mechanism, the production reachability of the new port (`BindToSession`
subscribes the set event), the ordering rule and the run-scope argument against the game's own code.
It found four things, all fixed in this same commit:
- **blocker** — `NetMsg.RecipeUnlockSnapshot` was never added to the direction-classification family,
  so the repository's own full suite was RED (`EveryNetMsg_IsExplicitlyClassified`, 1 of 3376). Fixed
  in `BidirectionalDirectionTests`, which also switched its per-role acceptance assertion on.
- **major** — the guest's re-report had no bound: an index the host's table cannot hold could be
  re-sent every 60 s for the whole session. Fixed by the per-index budget in item 3.
- **major** — the "no per-recipe alert" claim was cited against a test that only observes the Runtime
  seam. The claim is now stated where it is actually pinned (Runtime raises the set event only; the
  adapter's alert lives on the per-index event) and the matrix row points at that.
- **major/minor** — stale version references in `docs/api/mod-api.md` (both the §7 number and an old
  pre-reset number) and in the previous cycle's ticket; the batch apply's out-of-range wording now
  matches the live path's; the guest's "could not read the table" case is logged.

## Acceptance matrix (result)

| # | Scenario | Result | Evidence |
|---|---|---|---|
| 1 | Guest uses a blueprint; the report is dropped | Host and third parties learn the unlock without a reconnect: the guest's set re-report reaches the host on the next window, the host merges the difference and relays it | `SwallowedGuestUnlock_ConvergesThroughTheGuestsSetReReport` |
| 2 | Host unlocks a recipe; the relay is dropped | The guest converges through the host's set on the 60 s repair group | `UnlockMadeAfterTheEntry_RidesTheSixtySecondRepair` |
| 3 | Late joiner | Receives the current unlock set in the entry group — and again from every repair cycle by design — with no per-recipe alert | `LateJoiner_ReceivesTheHostsUnlockedSetOnEntry_WithoutAlerts` pins that the Runtime raises ONLY the set event for a backfill; the alert itself lives on the per-index event in `RecipeUnlockApply` (coverage gap 1) |
| 4 | Reconnect | Same entry group, idempotent apply (`INT = 0` twice is the same write) | that case + `HostMerge_RelaysOnlyTheIndicesItHadNotLearned` |
| 5 | Duplicate delivery | Idempotent: the merge compares before applying, the apply is one static write | `HostMerge_RelaysOnlyTheIndicesItHadNotLearned` |
| 6 | Host rejects / does not have the recipe | No unlock is applied anywhere: an index this host's table does not have is judged by the ordinary apply (refused by name in the log on every side), and the merge only feeds what its own table judged | `RecipeUnlockApply` apply path + `HostWithoutALiveTable_DoesNotJudgeAGuestsSet` |
| 7 | CraftReport dropped at the same time | The item facts still converge via I3/P3; the unlock converges via this ticket | unchanged paths (rows I3/P3) |

## Verification

- Focused: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~RecipeUnlockBackfill"` — 10/10.
- Neighbour families, same command with `~GuestBlockDamageReportRecoveryTests` and
  `~CraftSyncSimulationTests` plus the two direction-contract classes — 62/62. That neighbourhood caught
  two real defects of this cycle: the new optional port parameter had no default value, so a composition
  WITHOUT `INativeWorldFacts` could not construct `CraftSyncService` at all; and (under the review)
  the missing direction-classification row that made the main suite red.
- Normative gates: 56/56 (`SyncCoverageGateTests` for row I6's vocabulary, anchor counts and quote
  currency; `BacklogIntegrityGateTests`/`BacklogReferenceGateTests` for the move to `review/` and every
  reference to the ticket; `RepositoryGateTests` for the delivery checklist and absolute paths).
- Main suite with build: 3379/3379 (HEAD carried 3368; this cycle adds the 10 new cases and the one
  direction-contract theory row).
- `dotnet format`: exit 0 before the final run.
- Independent adversarial review: one round, fresh context, frozen tree (FULL tier). Verdict before the
  fixes: not shippable — one blocker (the direction contract above; the reviewer reproduced the red main
  suite three times), three majors and four minors, all fixed in this commit. The reviewer explicitly
  could not falsify the mechanism, the run-scope argument, the host-authority/one-apply-path claim or
  the "no premature clearing" claim, and confirmed production reachability of the new port.

## Known coverage gaps (declared, not silently carried)

The automated suite runs the Runtime half over a faked port. It does NOT execute, and the unified
dual-client acceptance pass has to cover:

1. The adapter's Unity half: reading the real `Recipes.recipes` (`INT == 0`), `RecipeUnlockApply`'s
   silent batch write, and the crafting-list refresh when the panel is open (all game types — the
   test host cannot touch them).
2. The protobuf round trip of `RecipeUnlockSnapshotMsg` in a real two-process session (the fake
   transport moves frames without protobuf).
3. The real host's 60 s cycle (`WorldEventSync.Update`) that calls the repair group, and the real
   lazy-P2P swallow window it exists to heal.
4. The branch that arms the pending set while the session is NOT active yet (an unlock made in the
   menu before the handshake completes) — covered by code review only: the sim world has no such
   window.
5. A suggested acceptance check: a late joiner entering while the host holds several unlocks should
   see exactly those recipes unlocked in the crafting list, with no alert spam, and a recipe
   unlocked by one guest should appear for the other guest within a minute even when its one-shot
   report was lost.
6. The per-index give-up of *What landed* 3 is a LOG signal only (a warning naming the index and the
   window count): a divergent recipe index has no user-visible surface in this cycle, and an explicit
   content-version gate for it is NOT claimed here (the mod manifest is already fail-closed).

## Non-goals

- Crafting recipe balance or content changes.
- The `CraftReport` item-fact path (already healed by the keyframe/snapshot).
- `hasMadeBefore` (a per-player crafting history, not a run fact) — deliberately not carried.
