# World and backup management surface

- Status: Review — landed 2026-09-19 (decision 198). Awaiting the final unified acceptance pass.
- Priority: Medium
- Category: Persistence / UI
- Source: Stage of `review/systemic-save-backup-management.md` (the umbrella re-scoped 2026-09-19); user
  decision 2026-09-19 on the surface and the restore semantics
- Related: `docs/decisions/active.md` 198, `docs/architecture/save-archive-format.md` §2/§6/§7,
  `review/save-format-and-world-repository.md` (S1), `review/save-interval-autosave-and-backup-recovery.md`
  (S4.4), `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldLibraryService.cs`,
  `src/CasualtiesUnknownOnline.Plugin/OnlineUiWorldsDrawer.cs`

## The gap (as it was)

The archive side of this ticket was already delivered by S1–S4: every committed cut writes a `.cuoz`
archive (§5/§7), interval autosave and retention are configurable (`SaveOptions`, defaults 10 minutes /
10 archives), the failure-degradation matrix and the pre-restore backup under a damaged live snapshot
all landed in S4.4, and `/save` is a manual cut at any time — which means a manual backup package also
already existed, because the §5 transaction always archives.

What did not exist was any way to SEE or CHOOSE:

- no surface listed a world's archives, and none restored a chosen one. The only promotion was the
  automatic recovery of a live snapshot the reader or the decode refused (§6) — the player had no
  route back to a good archive, and no route to the world the archive belonged to;
- §2's repository is multi-world, but the Continue entry resolved "the last opened world, else the
  first world with a snapshot" (`WorldSaveService.ContinueWorldId`): there was no picker at all. The
  repository's own `index.json` had been documented as "a cache for the world picker" since S1;
- the ticket's other two scopes conflicted with decisions already frozen with the user (see *Scope
  closed* below), so re-scoping it was part of this stage rather than implementing them.

## Design decisions frozen with the user (2026-09-19)

1. **The management surface is a new page in CUO's existing Online UI window** (Home/Players/Network/
   Admin/Worlds/Console/Preferences), not a console-command family and not new UI on the game's own
   main menu. The native menu has exactly one `Load` button and no slot list — a recorded fact from
   the S1 design work — so a picker had to be built somewhere; reusing the existing CUO window keeps
   one surface instead of two, and it is reachable from the main menu, which a restore requires.
2. **A restore is in place, and the state it replaces is archived first.** The chosen archive becomes
   the world's live snapshot; the snapshot being replaced is written into `backups/` as the
   pre-restore archive, so the restore can itself be undone by restoring that archive. The
   alternative (import the archive as a new, separate world) was rejected because it leaves the player
   with two near-identical worlds to tell apart and adds world-management semantics the repository
   does not have.

## Mechanism inventory

Reused unchanged (this stage adds no second implementation of any of these):

- the §5 write transaction and the §6 promotion (`WorldRepository.WriteSnapshot` / `PromoteBackup`,
  `WorldBackupPromotion`): the restore calls the promotion, it does not re-implement a file swap;
- the reader's manifest + checksum gate (`WorldRepository.LoadBackup` with `VerifyChecksums`), used as
  the pre-swap validation;
- the reader's own isolation guarantees (`.staging/` for the unpack, `.previous/` — the writer's own
  transient name — for the swap, `SaveArchiveFormat.DamagedFolderPrefix` for evidence);
- the world's writer lease (`WorldRepository.TryHoldWorld` / `ReleaseWorld`, §5) as the
  concurrent-instance guard;
- the §7 retention policy (`WorldRepository.PruneBackups` + `SaveOptions.Retention`);
- the Continue target rule (`WorldSaveService.ContinueWorldId`), read through `IWorldSaveControl`
  rather than re-derived, so the page's marker and the native Load button can never disagree;
- the Online UI window shell and its localization catalog (both languages);
- the runtime frame pump (`ICuoService.Update`), which is where the restore executes.

New:

- `IWorldLibrary` + `WorldLibraryService`: the world list, the backup list, the selection, the armed
  restore, every refusal rule, the report and the logging;
- `WorldLibraryEntry` / `WorldMaintenanceReport` / `WorldMaintenanceKind`: the page's row shape and the
  action account;
- `WorldPromotionTrigger`: what a promotion does with the snapshot it replaced (see below);
- `OnlineUiWorldsDrawer` + `OnlineUiPage.Worlds` + the window state fields + 22 localization keys per
  language.

## What landed

- **The Worlds page** (`OnlineUiWorldsDrawer`): every world with its display name, layer, member count,
  backup count and last-saved time; the row the Continue entry opens is marked as the target; a world
  without a snapshot is listed but cannot be selected (the entry would skip it, so selecting it would
  mark a world the Load button never opens). It also lists the archives of the world the player opens,
  newest first, with kind, local time and size.
- **A restore takes two clicks and one frame.** The first click picks an archive, the second confirms —
  the action replaces the state the player is playing, and the archive of that state is written by the
  same action. The click only ARMS (`TryRequestRestore`); the promotion runs on the runtime frame pump
  (`ICuoService.Update` → `WorldLibraryService.Pump`), because the click happens inside an IMGUI draw
  callback that runs more than once per frame while a restore must happen exactly once.
- **Every refusal is named, and none of them is silent.** A guest (the save layer's existing
  host-only rule, decision 164); a world that is loaded or being generated (the same `worldActive` fact
  `TryArmIntervalAutosave` takes, asked at the click AND again at the boundary); another CUO process
  holding the world's lease (the holder is named); an unknown world; a file that is not one of that
  world's archives; an archive that no longer exists at the boundary; an archive that cannot be opened.
  Each one lands in `LastReport` (which the page shows) and in one warning line.
- **The chosen archive is validated BEFORE anything is replaced.** It goes through the same
  manifest/checksum gate the load uses, and a failure refuses the restore while the working snapshot is
  still in `live/` — promoting first would destroy a good snapshot to install a broken one and defer
  the discovery to the next Continue.
- **A promotion now knows why it is happening** (`WorldPromotionTrigger`). The recovery
  (`RefusedSnapshot`) keeps the snapshot it replaced as `damaged-<stamp>/` evidence, exactly as before.
  A player-chosen restore (`PlayerChoice`) relies on the pre-restore archive it just wrote and swaps the
  replaced folder through `.previous/`, which the finished swap removes: a `damaged-` folder per
  restore would accumulate one full snapshot each time under a name that means "refused" and that no
  cut, prune or load ever deletes. If that pre-restore archive could NOT be written (an unreadable live
  manifest, a write failure), the folder IS preserved — the copy is then the only one.
- **The list cannot go stale** (`IWorldLibrary.Revision`). Listing worlds and archives touches the disk,
  so the rows are cached in the window state — and one revision number decides when they are re-read: it
  is bumped by every finished cut attempt of ANY trigger (the save layer's `CutReported` — a `/save`, the
  interval autosave, a layer boundary, a menu return) and by every management action, so a `/save` while
  the page is open refreshes it, and reopening the page after a session of saves shows that session's
  archives rather than the ones from the first open. Refresh still forces a reload by hand. This was the
  cycle's review finding (MAJOR 1): the first shape cached the rows behind a flag nothing ever cleared,
  which made three comments and this ticket's own text claim an invalidation that did not exist.
- **A restore selects the world it restored**, so the native Load button continues what the player just
  restored instead of whatever was selected before, and it runs the §7 retention pass afterwards (the
  pre-restore archive it just wrote is the newest, so retention takes the oldest instead).
- **The promotion step is now contained** (`WorldBackupPromotion` moving the replaced snapshot aside):
  a filesystem refusal there returns a refusal instead of throwing out of the primitive, which is what
  the recovery path needs when it promotes inside a Continue click. Static evidence only — see Limits.
- **Registration stays out of the composition root.** `CuoBootstrap` sits at 599 lines (`SourceShape`
  fails above 600, so 2 lines of headroom), so `WorldLibraryService` is registered from the plugin's own
  `extraRegistrations` (`PluginDependencyRegistrar.Apply`), the same place the adapter-facing ports and
  config monitors are registered — and it is registered after the game adapter so the frame boundary
  reads this frame's "is a world loaded" fact.
- The protocol stays 34: this is a local disk and UI surface (a fact, never a design input —
  decisions 137/188).

## Scope closed rather than deferred

- **Native `save.sv` backup (the umbrella's scope 4) is dropped.** It contradicts decision 165 and the
  format doc's §8 non-goal ("CUO never writes `save.sv` and never depends on it"): the native format
  cannot express a mid-run cut, so it carries nothing CUO's own archive does not, and backing it up
  would re-introduce the second source of truth that decision removed. The scope line was written
  2026-09-07, before those decisions were frozen on 2026-09-10.
- **Old-protobuf migration (scope 6) is dropped**: excluded from v1 by the save system's own frozen
  scope, and `CharacterDataFileStore` was retired outright in S4.1 (decision 178) — the module is
  unreleased, so there is nothing to migrate.
- **No world deletion, and no separate importer UI.** The repository has no delete primitive at all,
  and a destructive surface is not what a missing picker needs; a `.cuoz` copied into a world's
  `backups/` folder is listed and goes through the same validation gate, which is the import path.

## Acceptance

| # | Scenario | Evidence |
|---|---|---|
| 1 | The page lists every world with its facts, and marks the one the Continue entry opens | `WorldLibraryServiceTests.ListWorlds_MarksExactlyTheWorldTheContinueEntryOpens`, `..._ReportsTheSnapshotAndBackupFactsThePickerNeeds` |
| 2 | Selecting a world moves the Continue target and survives a restart | `WorldLibraryServiceTests.SelectedWorld_IsRememberedAcrossARestart` (a fresh `WorldRepository` over the same root reads it back from `index.json`) |
| 3 | Selections that cannot work are refused with a reason the page can show | `WorldLibraryServiceTests.SelectWorld_RefusesAWorldWithNoSnapshotAndRecordsWhy`, `..._RefusesAWorldThatIsNotThere` |
| 4 | A chosen archive becomes the live snapshot, and the replaced state is archived and loadable | `WorldLibraryServiceTests.Restore_ReplacesTheLiveSnapshotArchivesWhatItReplacedAndSelectsTheWorld` (re-opens the pre-restore archive, checks its reason and its payload) |
| 5 | A player-chosen restore leaves no `damaged-` folder and no `.previous/`; a restore whose pre-restore archive could not be written keeps the folder | `WorldSaveRecoveryTests.PlayerChosenPromotion_ArchivesTheReplacedSnapshotAndLeavesNoEvidenceFolder`, `..._KeepsTheReplacedFolderWhenNoPreRestoreArchiveCouldBeWritten` |
| 6 | The recovery path's evidence behaviour is unchanged | `WorldSaveRecoveryTests.Promotion_MovesTheRefusedSnapshotAsideAndPutsTheBackupInPlace` (same assertions as before the trigger existed) |
| 7 | A restore is refused while a world is loaded — at the click and again at the frame boundary | `WorldLibraryServiceTests.Restore_IsRefusedWhileAWorldIsLoadedAtTheClickAndAgainAtTheFrameBoundary` |
| 8 | A restore is refused for a guest, for a foreign archive and for a leased world | `..._IsRefusedForAGuest`, `..._RefusesAnArchiveThatIsNotOneOfTheWorlds`, `..._IsRefusedWhileAnotherInstanceIsWritingTheWorld` |
| 9 | An archive that cannot be opened destroys nothing | `..._RefusesAnArchiveThatCannotBeOpenedAndLeavesTheLiveSnapshotAlone` (archive list and live payload compared before/after) |
| 10 | Retention runs after a restore, and WHICH archives survive is what the policy says | `..._RunsTheConfiguredRetentionAfterPromoting` (the newest cut and the pre-restore archive of the replaced state are kept — the latter re-opened and checked by reason — while the oldest cut and the archive just restored from are gone) |
| 11 | Backups are listed newest-first and foreign files are ignored | `..._ListBackups_IsNewestFirstAndIgnoresForeignFiles` |
| 12 | A world with no live snapshot restores from its archives and says so | `..._Restore_WorksWhenTheWorldHasNoLiveSnapshot` (no pre-restore copy to take, no evidence folder invented) |
| 13 | The cached rows are invalidated by a committed cut and by a management action | `..._Revision_AdvancesOnACommittedCutAndOnAManagementActionSoThePageCannotGoStale` (a real cut through the save service) |

## Family sweep

- **Both promotion callers** were aligned in one change: the recovery passes `RefusedSnapshot` and its
  behaviour (and its tests) is unchanged, the library passes `PlayerChoice`. The trigger is a required
  parameter, so a third caller cannot silently inherit either behaviour.
- **Every listing surface** uses one rule for "the world the entry opens" (`ContinueWorldId`), so the
  page, the index pointer and the native Load button cannot drift apart.
- **Every staleness signal is one number.** The page does not subscribe to anything itself: the library
  owns the revision and bumps it from the save layer's own report event plus its own actions, so a
  second cached surface added later inherits the invalidation instead of re-inventing it.
- **Every refusal** goes through one `Refuse` helper: one `LastReport`, one warning line, the same
  words the page shows — a click that does nothing can never be silent.
- **The promotion primitive's failure contract** was checked for the new caller *and* fixed for the old
  one (the contained "move aside" step above), and the one path where it still cannot hold is now stated
  in the primitive's own documentation, in the log line and in Limits below.

## Limits

- **The page itself is not machine-verified.** The service's branches are covered by tests; the rendered
  window, its availability at the main menu (it is drawn by the same unconditional `OnGUI` overlay the
  lobby surfaces already use) and every frame-level behaviour are the user's acceptance pass. There is
  no game-in-process test harness in this tree, so no claim beyond "the logic and the refusals are
  pinned by tests, and the page compiles and is wired" is made here.
- **The invalidation signal is only as good as the events behind it.** Every committed cut and every
  management action bumps the revision; a change made OUTSIDE the game (a player copying or deleting a
  `.cuoz` by hand) is not observed until Refresh is clicked — the page does not poll the disk.
- **The validation reads the archive twice** (once to validate, once to unpack). That is the deliberate
  price of refusing a broken package before the working snapshot is replaced; a large world pays it
  again on the promotion's own read.
- **Restoring while a Continue attempt is still generating** is covered by the same `worldActive` gate
  the adapter feeds; the exact frame in which the game flips that flag is not something these tests can
  pin.
- **The contained "move aside" failure branch is verified by reading, not by a test.** Reaching it needs
  a real `IOException`/`UnauthorizedAccessException` from `Directory.Move`, and the promotion primitive
  has no seam to inject one (adding a process-global hook for it would put the suite into the shared
  game-assembly collection for one defensive branch). The residual is named in the primitive's doc: if
  the put-back fails for the same reason, the replaced snapshot stays in `.previous/` or
  `damaged-<stamp>/` with `live/` absent, the next load recovers it, and the pre-restore archive holds
  the same state.
- **A clock that steps backwards between a cut and a restore** would stamp the pre-restore archive
  older than the newest cuts, so retention could prune it first. That is the tree-wide absolute-time
  limitation (the handoff records it for `Environment.TickCount`), not something this surface creates.
- **The Runtime's refusal text is English** and is shown verbatim on the page, exactly as the console
  shows a cut report or a join error: one wording, one owner. The page's own labels are localized in
  both languages.
- World deletion, cloud saves and cross-machine sharing remain out of scope (§8 non-goals).

## Evidence

- Focused: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~WorldLibrary|FullyQualifiedName~WorldSaveRecovery|FullyQualifiedName~WorldRepositoryBackup"` → 31 passed / 0 failed (15 in `WorldLibraryServiceTests`, 5 promotion cases in `WorldSaveRecoveryTests`, 11 in `WorldRepositoryBackupTests`).
- Normative gates: `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` → 69 passed / 0 failed.
- Full suite with build: `dotnet test CasualtiesUnknownOnline.slnx` → `CasualtiesUnknownOnline.Tests` 3531 passed / 0 failed, gates 69 passed / 0 failed (the 3514 of the previous cycle plus this change's 17 cases). `dotnet format CasualtiesUnknownOnline.slnx` exits 0.
- Independent adversarial review (fresh context, frozen working tree, no writes by the reviewer): 0 blockers, 1 major, 4 minor, 2 nits — all reproduced numbers matched; the major (the row cache had no invalidation) and every minor/nit were fixed in this same change, and the two items that cannot be machine-verified are in Limits.
- Deployment: `tools/verify-deploy.ps1 -GameDir "<game-dir>"` — recorded in the delivery checklist for this cycle.
