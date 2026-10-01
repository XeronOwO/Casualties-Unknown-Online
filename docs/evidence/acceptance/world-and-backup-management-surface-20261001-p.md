# Acceptance record — World and backup management surface

- Ticket: `world-and-backup-management-surface` — verdict: moved to `done/`
- Batch: `20261001-p` — tickets `save-interval-autosave-and-backup-recovery`,
  `world-and-backup-management-surface`, `save-multiplayer-restore-and-backups` (its rows 4–6)
- Commit: `f4ddfcf11499338b4125aa32846fe54172d3b3dc` · Deployed artifact:
  `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+f4ddfcf11499338b4125aa32846fe54172d3b3dc`
  (`tools/deploy.ps1` rebuilt the tree at run start, `tools/verify-deploy.ps1` exit 0)
- Run: 2026-10-01 12:19 → 12:33 +08:00 · Host: physical machine (Steam) · Guest: Sandboxie Steam1
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (host + guest), `capture`,
  `input`, `logs`, `artifacts`
- Artifacts: the `batch-p/` files named below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The page lists every world with its facts and marks the one the Continue entry opens | visual + machine | pass | `p-worlds-host-1.png` (world rows with layer, players, backup count, last-saved; `Continue target` on `w-20261001-3315`); `p-worlds-marker-857c.png` (the marker after selection, on `w-20261001-857c`); `WorldLibraryServiceTests.ListWorlds_MarksExactlyTheWorldTheContinueEntryOpens` |
| 2 | Selecting a world moves the Continue target and survives a restart | machine + live | pass (limit) | `worlds.select.w-20261001-857c` → `index.json` `lastOpenedWorldId: w-20261001-857c` and the frame's `Last action: the Continue entry now opens this world`; `SelectedWorld_IsRememberedAcrossARestart` reads it back from `index.json`. **Limit:** the client restart itself was not performed — the read-back is the machine case, the live half is the write |
| 3 | Selections that cannot work are refused with a reason the page can show | machine + live | pass | `SelectWorld_RefusesAWorldWithNoSnapshotAndRecordsWhy`, `SelectWorld_RefusesAWorldThatIsNotThere`; live, a snapshotless world is listed with `no snapshot yet` and offers no select control (`p-worlds-backups-scrolled.png`) |
| 4 | A chosen archive becomes the live snapshot, and the replaced state is archived and loadable | machine + live | pass | Live: the chosen `mid-run-20261001-042952.cuoz` became `live/` (its manifest `savedAtUtc 04:29:52Z`); the replaced state was archived as `mid-run-20261001-043042.cuoz` with `saveReason = pre-restore-backup` (read from the archive); `Restore_ReplacesTheLiveSnapshotArchivesWhatItReplacedAndSelectsTheWorld` |
| 5 | A player-chosen restore leaves no `damaged-` folder and no `.previous/`; a restore whose pre-restore archive could not be written keeps the folder | machine + live | pass | Live: after the restore the world folder holds exactly `backups/`, `live/`, `world.json` (no `damaged-*`, no `.previous/`); the recovery path, whose pre-restore archive could not be written (a corrupt live does not round-trip), kept `damaged-20261001-043214/`; `PlayerChosenPromotion_ArchivesTheReplacedSnapshotAndLeavesNoEvidenceFolder`, `KeepsTheReplacedFolderWhenNoPreRestoreArchiveCouldBeWritten` |
| 6 | The recovery path's evidence behaviour is unchanged | machine + live | pass | Live: `damaged-20261001-043214/` preserved, `mid-run-20261001-043204.cuoz` promoted to `live/`, account lines per half; `Promotion_MovesTheRefusedSnapshotAsideAndPutsTheBackupInPlace` |
| 7 | A restore is refused while a world is loaded — at the click and again at the frame boundary | machine | pass (limit) | `Restore_IsRefusedWhileAWorldIsLoadedAtTheClickAndAgainAtTheFrameBoundary` in the focused suite. **Limit:** the live click was deliberately not made while a world was loaded (a real refusal was expected, but a wrong one would have replaced the live snapshot mid-session) |
| 8 | A restore is refused for a guest, for a foreign archive and for a leased world | machine + live | pass | Guest (live): the guest's confirm produced `WRN … Restore of world w-20261001-857c was refused: a guest never writes a world archive; the host is the only save authority (decision 164)` and the frame `p-worlds-guest-refusal.png` shows `Last action: … the host is the only save authority` with no file change on the host; `Restore_IsRefusedForAGuest`, `Restore_RefusesAnArchiveThatIsNotOneOfTheWorlds`, `Restore_IsRefusedWhileAnotherInstanceIsWritingTheWorld`; the live lease refusal was observed on the cut path (`LeaseHeld`, `run-c-host-console-after-lease.json`) |
| 9 | An archive that cannot be opened destroys nothing | machine | pass | `Restore_RefusesAnArchiveThatCannotBeOpenedAndLeavesTheLiveSnapshotAlone` |
| 10 | Retention runs after a restore, and WHICH archives survive is the policy | machine + live | pass | Live: `1 older backup(s) were pruned (retention 3)` after the restore; the pre-restore archive was kept and the archive just restored from was pruned by retention; `Restore_RunsTheConfiguredRetentionAfterPromoting` |
| 11 | Backups are listed newest-first and foreign files are ignored | machine + live | pass | `p-worlds-backups-scrolled.png`: the `BACKUPS OF …` section (the world's display name) with `autosave · 2026-10-01 12:25 · 0.0 MB` above `mid-run · 2026-10-01 12:22 · 0.0 MB`; `ListBackups_IsNewestFirstAndIgnoresForeignFiles` |
| 12 | A world with no live snapshot restores from its archives and says so | machine | pass | `Restore_WorksWhenTheWorldHasNoLiveSnapshot` |
| 13 | The cached rows are invalidated by a committed cut and by a management action | machine + live | pass | Live: the page's counts followed the cuts (`3 backup(s), last saved 2026-10-01 12:26` on the guest's frame; the host's frames after later cuts), and the explicit `worlds.refresh` during the unusable-root setup re-read to `No worlds yet`; `Revision_AdvancesOnACommittedCutAndOnAManagementActionSoThePageCannotGoStale` |
| 14 | The page is reachable while a world is loaded and at the main menu | visual | pass | `p-worlds-host-1.png` (in world), `p-worlds-marker-857c.png` and `p-worlds-after-restore.png` (main menu), `p-worlds-badroot.png` |
| 15 | A restore takes the click (choose), the confirm, and one frame boundary (arm/execute split) | visual + machine | pass | `run-c-host-restore-click1.json` / `run-c-host-restore-click2.json` (the `worlds.restore_yes.*` confirm controls offered and applied); the result frame `p-worlds-after-restore.png` (`Last action: the chosen backup is the world's live snapshot`); `Restore_Replaces…` pins the file outcome |

## Residuals for the user

None. The page's visual rows were judged from window-level frames the run captured and the agent read;
nothing in this batch is a subjective-only row.

## Limits

- The machine rows were run in this batch's focused suite (`batch-p/p-focused-suite.txt`, 104/104) and the gates project passed 300/300 (`batch-p/p-gates.txt`); the full suite is 4548/4548 (`batch-p/p-full-suite.txt`).

- Frame reading judges what is on screen; pixel-level typography and the page's own feel remain outside
  this record. The frames are named above and live in the artifact directory.
- Row 2's client restart was not performed; the live half is the pointer write and the Continue entry,
  the read-back is the machine case.
- Row 7's live click was not made (see the row); the refusal is machine evidence, and the run says so.
- The guest's Worlds page reads the host's repository through Sandboxie's read-through, so it lists the
  same worlds; it was used for the guest-refusal row, not as an independent third view.
- The live lease refusal observed in this batch is the cut path's `LeaseHeld`; the restore's own lease
  gate stays a machine case in this run.
