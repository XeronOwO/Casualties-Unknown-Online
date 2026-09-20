# World and backup management surface (the Worlds page and the player-chosen restore)

Date: 2026-09-19
Scope: the Online UI's new **Worlds** page — the worlds the CUO repository holds, the backup
archives of one of them, the world the native Load button will open, and a restore that replaces
one world's live snapshot with an archive the player picks (decision 198,
`docs/backlog/review/world-and-backup-management-surface.md`).

## What landed

- **Runtime** (`WorldLibraryService` + `IWorldLibrary`, `WorldLibraryEntry`,
  `WorldMaintenanceReport`, `WorldMaintenanceKind`): world and backup listings, the Continue-target
  selection, the armed restore plus its execution on the runtime frame pump, every refusal rule, the
  action account and the logging. It re-uses the §5 transaction, the §6 promotion, the reader's
  manifest/checksum gate, the writer lease and the §7 retention policy — it implements none of them
  again.
- **`WorldPromotionTrigger`** (`WorldPromotionTrigger.RefusedSnapshot` / `PlayerChoice`): the
  recovery keeps what it replaced as `damaged-<stamp>/` evidence, while a player-chosen restore
  relies on the pre-restore archive it just wrote and swaps the replaced folder through the writer's
  own `.previous/`. If that archive could not be written the folder IS preserved.
- **Plugin** (`OnlineUiWorldsDrawer`, `OnlineUiPage.Worlds`, the window state, 22 localization keys
  × 2 languages): the page itself, its two-click restore confirmation, and the revision-based
  invalidation of its cached rows. Registered from the plugin's `extraRegistrations`, so the
  composition root (`CuoBootstrap`, 599 lines against a 601-line gate ceiling) is untouched.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| §5 write transaction / §6 promotion | Re-used unchanged; the restore calls `WorldRepository.PromoteBackup` | `WorldLibraryService.Restore` |
| Reader manifest + checksum gate | Re-used as the pre-swap validation (`VerifyChecksums`) | `Restore_RefusesAnArchiveThatCannotBeOpenedAndLeavesTheLiveSnapshotAlone` |
| Replaced-snapshot evidence | Now trigger-dependent; recovery path byte-identical | `git diff` of `WorldSaveRecoveryTests` (assertions unchanged), `PlayerChosenPromotion_*` |
| Writer lease (§5) | Re-used as the concurrent-instance guard | `Restore_IsRefusedWhileAnotherInstanceIsWritingTheWorld` |
| Retention (§7) | Re-used after a restore | `Restore_RunsTheConfiguredRetentionAfterPromoting` |
| Continue-target rule | Read through `IWorldSaveControl.ContinueWorldId`, never re-derived | `ListWorlds_MarksExactlyTheWorldTheContinueEntryOpens` |
| Online UI window + localization | New page, new keys, both languages | key-parity check: 271 keys, zero one-sided drift |
| Runtime frame pump (`ICuoService`) | New execution point for an armed restore | `Revision_AdvancesOnACommittedCutAndOnAManagementActionSoThePageCannotGoStale` (a real cut) |
| Protocol / wire | **Untouched** — protocol stays 34 (a fact, not a design input) | no message type changed |

## Verification design

- **Focused**: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~WorldLibrary|FullyQualifiedName~WorldSaveRecovery|FullyQualifiedName~WorldRepositoryBackup"` → **31 passed / 0 failed**.
- **Normative gates**: **69 passed / 0 failed**.
- **Full suite with build**: `CasualtiesUnknownOnline.Tests` **3531 passed / 0 failed** + gates 69 (previous cycle 3514 + this change's 17 cases). `dotnet format` exits 0.
- **Independent adversarial review** (fresh context, frozen working tree, reviewer wrote nothing):
  0 blockers / 1 major / 4 minor / 2 nits, with every number in this file reproduced. The major (the
  cached rows had no invalidation) and all four minors and both nits were fixed in the same change;
  the two items that cannot be machine-verified are written into the ticket's Limits.
- **Static evidence**: every branch the page takes goes through one `Refuse` helper (one report, one
  warning line), so a refusal cannot be silent; the page touches no directory during a draw pass (the
  only disk read on the draw path is `FileInfo.Length` for an archive's size, inside its own
  try/catch).

## What only the user can verify (no game-in-process harness exists here)

- That the page renders correctly and its seventh tab fits at the player's resolution.
- That it is reachable from the **main menu**, which a restore requires (by construction it rides the
  same unconditional `Plugin.OnGUI` → `OnlineUiOverlay.Draw` path as the lobby pages — read, not run).
- The end-to-end native flow: restore an archive, press the game's own **Load**, and confirm the
  world regenerates from the restored snapshot. The tests prove the pointer (`SelectedWorldId` ==
  `ContinueWorldId` == the restored world), not the subsequent native load.
- The exact frame in which the adapter's "world is loaded or generating" flag flips during Continue
  generation, and how the refusal reads in a real session.

## Accepted limitations

- The archive is read twice on a restore (validate, then unpack): the deliberate price of refusing a
  broken package before the working snapshot is replaced.
- The invalidation signal covers committed cuts and management actions; a `.cuoz` copied or deleted by
  hand outside the game is not noticed until **Refresh** is pressed (the page does not poll the disk).
- The contained "move aside" failure branch is verified by reading only: reaching it needs a real
  `IOException` from `Directory.Move`, and the primitive has no injection seam. The residual (a failed
  put-back leaves the replaced snapshot in `.previous/`, which the next load restores) is documented in
  the primitive and in the ticket's Limits.
- A clock that steps backwards between a cut and a restore could stamp the pre-restore archive older
  than the newest cut; that is the tree-wide absolute-time limitation, not this surface's.
- The Runtime's refusal text is English and shown verbatim, like every other Runtime message the UI
  displays; the page's own labels are localized in English and Chinese.
- No manual dual-client acceptance was performed: per the development-period rule this is verified
  with unit tests + static evidence + the gates, and the user's acceptance pass covers the rest.
