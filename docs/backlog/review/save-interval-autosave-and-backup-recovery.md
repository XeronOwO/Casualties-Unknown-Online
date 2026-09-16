# S4.4 — Interval autosave, backup retention, failure degradation, refusal recovery

- Status: Review (landed 2026-09-14; awaiting the final unified acceptance pass)
- Priority: High
- Category: Persistence / save system
- Source: `docs/backlog/review/save-multiplayer-restore-and-backups.md` scope 4 + 5, plus S3's
  scope 7 (`docs/backlog/todo/save-mid-run-consistent-cut.md`), acceptance rows 4, 5 and 6
- Related: decision 25 (BepInEx `ConfigFile` → `IOptionsMonitor`), decision 164 (the multi-world
  repository; the host is the only save authority), decision 165 (the native save is never read),
  decision 179 (one notification plus the itemized account), decision 181 (the writer lease and the
  degradation matrix), decision 182 (the refusal recovery), §5/§6/§7 of
  `docs/architecture/save-archive-format.md`

## What landed

**The interval autosave (scope 4).** The frame-end seam arms it
(`IWorldSaveControl.TryArmIntervalAutosave`, called by `SaveCutSeam.Update`) and takes it like every
other cut, so an autosave rides the same transient policy, the same deferral deadline, the same
report and the same one-log-line account. Three decisions are worth naming:

- It is its own KIND (`WorldCutKind.Auto`, reason `auto-interval`), not a mid-run cut with a different
  reason: the archive is named `auto-<stamp>.cuoz` and the manifest says what it holds.
- The interval (default 10 minutes, frozen with the user) restarts on every **committed** cut of any
  trigger. A refused or deferred cut has not written the world, so counting it would push the next
  autosave out by a full interval for a world that was never saved; a committed hand save, layer
  boundary or menu return means the world was just written, so the next interval starts there.
- It only arms while a world is LOADED (`inWorld` from the adapter). A host that returned to the main
  menu still owns a world folder, and cutting it there would churn — and prune — the archive set of a
  world nobody is playing. A cut the player asked for always wins the seam: the interval never
  supersedes an armed `/save` or menu return, so the player's own trigger is not relabelled as an
  autosave and is still answered in the console.

**Retention (scope 4).** After every COMMITTED cut the writer prunes the world's archives to the
configured count (`WorldCutWriter.Prune` → `WorldRepository.PruneBackups`), oldest first, never the
newest, and a file that cannot be deleted is reported twice — a warning from the repository and the
cut's own account (`N archive(s) kept, M could not be pruned`) — while the cut stays committed and the
world stays loadable.

**The config surface (scope 4, decision 25).** `SaveOptions` (`AutosaveEnabled`,
`AutosaveIntervalMinutes`, `BackupRetentionCount`) is bound in the plugin's `[Save]` section to a
`BepInExOptionsMonitor<SaveOptions>`, with a `MutableOptionsMonitor` default in `CuoBootstrap` so a
composition without a config file still resolves. The values are read at each DECISION (arm, cut,
prune), so a config edit hot-reloads without a restart; `SaveOptions` clamps both bounds as well,
because a hand-edited config file bypasses BepInEx's own range validation.

**Failure degradation (scope 5).** Each case has a defined, logged, non-crashing behaviour:

| Case | Behaviour |
|---|---|
| Disk full mid-transaction | The transaction refuses at the step that failed (`StageFailed`/`BackupFailed`/`CommitFailed`), the previous snapshot stays live, the cut is reported as refused and the session continues; the leftover `.staging/` is reset by the next write and discarded by the loader. |
| Read-only save directory | Same path: `UnauthorizedAccessException` is caught at the transaction and at the directory creation, the run keeps playing, and the world simply cannot be saved. |
| Save root that cannot exist (drive gone, a file where the root belongs) | `WorldCatalog.TryEnsureDirectory` turns it into `WorldCreateResult.Failed` / an empty world list / `SaveWriteResult.Failed` instead of an exception thrown into the run's start path or the picker. |
| Pruning failure | Reported, world untouched, cut committed, the undeletable archive stays on disk. |
| Concurrent host instances | `WorldLease` (`world.lease`): one writer per FOLDER. A write that finds a lease another process refreshed inside 30 minutes is refused by name; a stale lease is taken over with a warning; the restore takes the lease before it applies anything (decision 181). |

**The decode-level refusal's recovery (S3 scope 7, acceptance row 6).** A refusal that happens AFTER
the reader's own fallback (the manifest read fine, but the payload contradicts it or the run baseline
cannot be read) now gets the retry it never had: `WorldRestoreRecovery` walks the world's backups
newest-first, skips the one the load already used, decodes each with the same decoder that refused the
live snapshot, and takes the first that decodes. `WorldBackupPromotion` then promotes it — the refused
snapshot moves to `damaged-<stamp>/` untouched, the snapshot about to be replaced is archived into
`backups/` with its reason rewritten to `pre-restore-backup` (the format's own reason, produced for
the first time), and the backup becomes `live/` — so the evidence of the refusal is not deleted by the
next cut's transaction. Order is §5's: nothing is moved until the replacement is unpacked and verified
in a fresh `.staging/`; a promotion that cannot finish puts the preserved folder back and refuses;
when no backup decodes the continue is refused with the reason it already had and the folder is left
exactly as found.

**Structure (the ticket's Known constraint).** `WorldSaveService` was at the architecture line limit,
so the cut-TRIGGER family moved out before the interval trigger joined it: `WorldCutTrigger` owns the
armed request, the bounded wait, the interval, the write and the retention pass, while the service
keeps WHETHER and WHERE (the session's role, the world identity, the Continue entry). The same pass
split two more classes that the new work pushed over the limit, in each case along a responsibility
the class already documented:

- `WorldCatalog` — the WORLDS a root holds (listing, creation, rename, metadata, the rebuildable
  `index.json` cache); `WorldRepository` keeps ONE world's files (the snapshot transaction, the
  backups, the lease).
- `SaveSalvagePass` — the per-entry salvage pass of §6, out of `SaveArchiveReader`, which owns OPENING
  a snapshot (the manifest gate, the live folder, the backup fallback).

New types: `WorldCutTrigger`, `WorldCutTarget`, `WorldAutosaveInterval`, `SaveOptions`,
`WorldLease` + `WorldLeaseEntry`, `WorldBackupPromotion`, `WorldRestoreRecovery`, `WorldCatalog`,
`SaveSalvagePass`.

## Mechanism evidence

| Mechanism | Evidence (file:line) |
|---|---|
| The interval autosave is a trigger like any other | `src/CasualtiesUnknownOnline.GameAdapter/Run/SaveCutSeam.cs:66` (`_saves.TryArmIntervalAutosave(inWorld);` immediately before `TakeArmedCut(frame)`), `WorldCutTrigger.Take` |
| An interval cut is the `auto` kind | `WorldCutWriter.KindOf` maps `AutoInterval` → `WorldCutKind.Auto`; `SaveArchiveFormat.CutKindName` names the archive `auto-<stamp>.cuoz` |
| Retention runs after a committed cut, never on a refusal | `WorldCutTrigger.Write` (the prune follows the `write.Success` branch), `WorldRepository.PruneBackups` |
| The config surface hot-reloads | `PluginDependencyRegistrar` (`[Save]` section → `BepInExOptionsMonitor<SaveOptions>`), `WorldSaveService.Options` read per decision |
| One writer per world folder | `WorldLease.TryAcquire` (checked by `WorldRepository.WriteSnapshot` and `TryHoldWorld`), taken by `WorldRestoreApplier` before it applies anything |
| The degradation matrix | `SaveArchiveWriter.WriteWorldSnapshot`'s catch clauses, `WorldCatalog.TryEnsureDirectory`, `WorldBackupPromotion`'s rollback |
| The refusal recovery | `WorldRestoreApplier` (the decode-refusal branch) → `WorldRestoreRecovery.TryRecover` → `WorldRepository.PromoteBackup` → `WorldBackupPromotion.Promote` |
| The pre-restore copy | `SaveArchiveWriter.ArchiveExistingSnapshot`, `WorldBackupPromotion.AsPreRestore` |

## Tests

New machine-verified coverage (all in `tests/CasualtiesUnknownOnline.Tests`):
`WorldAutosaveIntervalTests` (the pure clock), `WorldSaveAutosaveTests` (the tick's gates: in-world,
interval elapsed, autosave off, a player trigger already armed, guest, no world, restart on a
committed cut, no restart on a refused cut, restart at a Continue click), `WorldSaveRetentionTests`
(keep N, newest kept, hot-reloaded retention, an undeletable archive), `SaveOptionsTests` (defaults and
both clamps), `WorldLeaseTests` (fresh/stale/unreadable leases, our own lease, release, the refused
continue), `WorldSaveDegradationTests` (disk full, read-only, unusable root), `WorldSaveRecoveryTests`
(the end-to-end recovery: preserved evidence, promoted live snapshot, the pre-restore archive, the
refusal when nothing decodes, an unpackable archive, another world's backup).

`WorldSaveContinueTests.TryContinue_WithoutAReadableRunBaseline_IsRecoveredFromTheCutsOwnBackup`
replaces the old refusal expectation: the same damage now recovers from the cut's own backup, and the
refusal it used to pin is pinned by `WorldSaveRecoveryTests.WhenNoBackupDecodes_TheContinueIsRefused_AndTheWorldIsLeftAlone`.

## The adversarial pass, and what happened to each finding

Three findings came out of the adversarial reading of the diff, every one of them in NEW code, and
all three are fixed with tests that pin the corrected behaviour:

1. **A refused autosave re-armed on the very next frame (BLOCKER, fixed).** The interval restarted
   only on a COMMITTED cut, so a world that cannot be written (a read-only folder, a drive that went
   away) stayed "due" forever: the pump would arm and TAKE a cut every single frame, running a full
   snapshot transaction — checkpoint, world-fact capture, encoding, staging, a failed write — per
   frame, for as long as the cause lasted. The interval now counts any cut that REACHES the writer
   (`WorldCutTrigger.Write` notes the attempt before the transaction), so a failed attempt opens a
   fresh window and the next autosave is one interval away. Found red first:
   `WorldSaveAutosaveTests.ARefusedAutosave_DoesNotArmAgainOnTheNextFrame` observed the re-arm on the
   unfixed code.
2. **A crashed writer's lease locked the world for the whole staleness window (fixed).** A heartbeat
   is not evidence of life: a host that died a minute ago left a 30-minute lease and a *restarted*
   game would have been refused with "another CUO instance is writing this world". The lease now
   checks whether the recorded owner is a process on THIS machine that is still running
   (`WorldLease.IsOwnerAlive`); a gone process is taken over at once, another machine still counts on
   the heartbeat (it cannot be inspected), and anything un-inspectable counts as ALIVE, because
   stealing a world from a running writer is the one outcome the lease exists to prevent. Pinned by
   `ALeaseFromAProcessThatIsGone_IsTakenOverImmediately` and `ALeaseFromAProcessThatIsRunning_IsRefused`
   (the latter against a real child process).
3. **A refused continue kept the writer lease (fixed).** The lease is taken before the payload is
   applied, so every refusal after that point held the world until the window passed — for a restore
   that never happened. `WorldRestoreApplier.RefuseHolding` releases it on all four post-lease refusal
   paths. Pinned by `ARefusedContinue_GivesTheLeaseBack`.

Checked and found sound (no change needed): retention never touches the newest archive and runs only
after a committed write; the recovery skips the archive the load already opened; a failed promotion
puts the preserved folder back; `damaged-<stamp>/` is invisible to the world scan, the backup listing,
the pruner and the loader; the pre-restore archive is verified against its manifest before it is
renamed into `backups/`; a lease that cannot be written does not block the write it cannot protect.

Known, deliberate asymmetry: when the live snapshot's MANIFEST does not read, the load still falls
back to a backup in memory without promoting it (S1's contract: a load writes nothing), while a
DECODE-level refusal promotes. The manifest case therefore keeps its pre-existing behaviour; the
decode case needed both the retry and the promotion because the refusal arrives after the load
returned. After a promotion `world.json` still names the replaced snapshot's time until the next cut
refreshes it (the picker reads that field); the promotion deliberately does not rewrite the metadata,
because the live snapshot it now holds is older than what that field describes and inventing a time
would be worse than a stale one.

## Verification limits

- The dual-client rows (acceptance 1–3) and the entity work above them are unchanged by this stage;
  what THIS stage adds is machine-verifiable on a temp repository, and every row of the failure matrix
  is covered by a test that injects the failure rather than reasoning about it.
- Not machine-verifiable here: the seam's own wiring inside the running game (`SaveCutSeam.Update` is
  adapter code and touches `PlayerCamera`), the console rendering of the autosave's absence (a system
  trigger is deliberately not announced), and the retention behaviour on a real multi-gigabyte world.
- The writer lease is machine-verified as a FILE protocol; two real game instances pointed at one
  folder is a user-run scenario (the sandbox pair does not share a saves root by construction).
- **Process gap, recorded rather than papered over**: this session had no subagent capability (the
  available tools were file read/write and a shell), so the adversarial pass above was a fresh
  re-reading of the diff by the same agent, NOT the independent second context AGENTS.md requires for
  an architecture/cross-module change. The three findings show the pass was real work, but an
  independent reviewer with a fresh context should re-audit the interval/lease/recovery trio before
  the final acceptance pass.
