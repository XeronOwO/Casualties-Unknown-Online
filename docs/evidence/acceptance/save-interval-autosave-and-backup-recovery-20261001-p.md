# Acceptance record — S4.4 interval autosave, backup retention, failure degradation, refusal recovery

- Ticket: `save-interval-autosave-and-backup-recovery` — verdict: moved to `done/`
- Batch: `20261001-p` — tickets `save-interval-autosave-and-backup-recovery`,
  `world-and-backup-management-surface`, `save-multiplayer-restore-and-backups` (its rows 4–6)
- Commit: `f4ddfcf11499338b4125aa32846fe54172d3b3dc` · Deployed artifact:
  `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+f4ddfcf11499338b4125aa32846fe54172d3b3dc`
  (`tools/deploy.ps1` rebuilt the tree at run start, `tools/verify-deploy.ps1` exit 0)
- Run: 2026-10-01 12:19 → 12:33 +08:00 · Host: physical machine (Steam) · Guest: Sandboxie Steam1
  (one lobby, a fresh world `w-20261001-857c`)
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (host + guest), `capture`,
  `input`, `logs`, `artifacts` — preflight 11 present; `sandbox-alt` configured but not needed
- Artifacts: the `batch-p/` files named below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The interval autosave is its own kind (`auto-<stamp>.cuoz`, reason `auto-interval`) | machine | pass | `WorldCutTrigger`/`WorldCutWriter` lines in `p-host-cuts-and-autosave.txt`: `Cut AutoInterval armed` → `Cut AutoInterval committed … auto-20261001-042507.cuoz`; the file appears in the world's `backups/` |
| 2 | The interval restarts on every committed cut of any trigger | machine | pass | The manual cut committed 12:26:35.581; the next interval armed 12:27:35.454 — 60.0 s later, while the pre-restart schedule would have fired at 12:27:07 (`p-host-cuts-and-autosave.txt`) |
| 3 | A cut that reaches the writer but FAILS also opens a fresh window | machine | pass | `WorldSaveAutosaveTests.ARefusedAutosave_DoesNotArmAgainOnTheNextFrame` in the focused suite; live, the refused cuts (read-only, bad root) produced no cut storm — the next attempt was a manual one a full interval later (`p-host-latest.log`, `run-c-host-console-after-readonly.json`) |
| 4 | It only arms in a loaded world; a player-armed cut wins the seam | machine | pass | `WorldSaveAutosaveTests` (in-world / player-trigger / guest / no-world gates) in the focused suite; live, no interval cut followed the world being left (`p-host-cut-accounts.txt`) |
| 5 | Retention runs after each committed cut, oldest first, the newest never pruned | machine + live | pass | Account lines `3 archive(s) kept, 1 pruned` / `4 archive(s) kept, 1 could not be pruned`; the newest archive is present in every listing (`p-host-cut-accounts.txt`, `run-c-host-console-after-prunefail2.json`) |
| 6 | Config surface: values read at each decision, hot-reload, clamps | machine + live | pass (limit) | Live through the plugin's own `ConfigEntry` (the `SettingChanged` path): 10 → 1 minute / 3 took effect at the next decision (an immediately-due auto cut at 12:25:07); autosave off stopped the series; restore 10/10 verified (`p-saveoptions-set.json`, `p-saveoptions-after-entry-edit.json`); clamps: `SaveOptionsTests`. **Limit:** a direct edit of the `.cfg` on disk is NOT observed (`p-saveoptions-after-disk-edit.json` still reads `IntervalSeconds 600`); BepInEx 5.4's `ConfigFile` has no file watcher, so a hand edit needs a reload/restart |
| 7 | Failure: disk full mid-transaction | machine | pass (limit) | `WorldSaveDegradationTests` in the focused suite; not injected live — no bounded, safe way to fill the volume on a shared machine |
| 8 | Failure: read-only save directory | machine + live | pass | Deny-write ACL on the world folder → `cut refused … StageFailed (… .staging is denied)` + `the world writer lease … could not be recorded`; `live/manifest.json` hash unchanged (`BB8C4965…`), no new archive, session alive; removing the ACL made the next cut commit (`run-c-host-console-after-readonly.json`, `run-c-host-console-after-recovery.json`) |
| 9 | Failure: pruning failure | machine + live | pass | Read-only attribute on the oldest archive → the cut committed with `4 archive(s) kept, 1 could not be pruned` and `WRN … Backup auto-20261001-042735.cuoz … could not be pruned; it stays on disk`; clearing it pruned the backlog on the next cut (`3 … kept, 2 pruned`) (`run-c-host-console-after-prunefail2.json`, `run-c-host-console-after-prune-recovery.json`) |
| 10 | Failure: a save root that cannot exist | machine + live | pass | The `saves` root replaced by a file → `cut refused … StageFailed (… Cannot create … because a file or directory with the same name already exists)`, no exception; the Worlds page degraded to `No worlds yet` with an empty backup list (`p-worlds-badroot.png`); restoring the root committed the next cut (`run-c-host-console-after-badroot.json`, `run-c-host-console-after-badroot-recovery.json`) |
| 11 | Failure: concurrent host instances (the writer lease) | machine + live | pass (limit) | Fresh crafted lease held by a live foreign process (`<machine>:<pid>`) → `cut refused … LeaseHeld (another CUO instance … is writing this world; its last write was 1 minute(s) ago)`; a 40-minute-stale lease was taken over (`Taking world … over from <machine>:<pid>: its lease has not been refreshed for 40 minute(s)`), then the lease named the host; `WorldLeaseTests` in the focused suite. **Limit:** a real second game instance was not run (the sandbox pair does not share a saves root by construction) — the lease file was a declared setup owned by a live process |
| 12 | Decode-level refusal recovery and promotion | machine + live | pass | `live/run.json` corrupted → Continue refused the snapshot (`the snapshot has no readable run baseline (run.json)`), recovered from `mid-run-20261001-043204.cuoz`, preserved the refused snapshot at `damaged-20261001-043214/`, promoted the backup to `live/` (its manifest is live) and the world loaded; `WorldSaveRecoveryTests` in the focused suite (`run-c-host-console-after-refusal-recovery.json`, `p-host-latest.log`) |
| 13 | The pre-restore copy of §6 | machine + live | pass | The player-chosen restore wrote `mid-run-20261001-043042.cuoz` with `saveReason = pre-restore-backup` (read from the archive's own manifest); `WorldSaveRecoveryTests` in the focused suite |
| 14 | The extraction left no over-limit class / the shape gates hold | machine | pass | `SourceShapeGateTests` in the gates project (`batch-p/p-gates.txt`, 300/300); the focused suite `batch-p/p-focused-suite.txt` 104/104 with 0 failed |

## Residuals for the user

None. Every visual row of this batch was captured per window and read by the agent; no row was left
subjective.

## Limits

- **Disk full was not injected live.** The suite injects it per step; the live read-only and bad-root
  refusals exercise the same `StageFailed` refusal path and the same "previous snapshot stays live"
  outcome, but the disk-full step itself is machine evidence only.
- **The lease's two-instance case was not run.** The live lease refusals were produced with a declared
  lease file owned by a live process on this machine (the protocol's own input), not by a second CUO
  instance writing the folder.
- **The config hot-reload claim is judged on the config-entry path.** The runtime's per-decision read
  and the monitor's `SettingChanged` reload are proven live; a direct on-disk `.cfg` edit is not
  observed by BepInEx 5.4 (`p-saveoptions-after-disk-edit.json`) and still needs a restart. The ticket's
  wording ("a config edit hot-reloads") is read as the config system's own edit path; the on-disk path
  is recorded here rather than claimed.
- **The independent re-audit the ticket's own note asked for** ran as a fresh-context reviewer against
  the frozen tree (`batch-p/independent-reaudit.md`): 0 blockers, 3 majors, 3 minors, 3 nits, and it
  reproduced this batch's figures at review time (103/103 focused, 4547/4547 full, 300/300 gates). A
  bounded verification round then re-read the fixes: all findings `fixed`, nothing broken — and it
  corrected one reading, which this record keeps: the kernel's finiteness guard is a LIVE backstop on
  the WIRE path (protobuf carries NaN/Infinity), only the archive path is gated by the JSON layer,
  which is why the `RefuseHolding` change matters. After the fixes this batch's own figures are
  104/104 focused, 4548/4548 full and 300/300 gates (`p-focused-suite.txt`, `p-full-suite.txt`,
  `p-gates.txt`). Its findings are
  folded in: the kernel's post-lease refusal now releases the lease like its siblings (that guard is
  unreachable from an archive this build reads — the JSON layer refuses an out-of-range multiplier
  first, pinned by `WorldLeaseTests.AnArchiveCarryingAnOutOfRangeMultiplier_IsRefusedByTheJsonLayer_AndTheLeaseIsReleased`);
  the interval prose that still described the pre-fix "only a committed cut restarts it" rule is
  corrected in the code comments, the test class doc, `docs/architecture/save-archive-format.md` §7,
  the plugin's config description and the ticket; the lease's missing liveness clause is added in the
  format doc §5, the ticket's degradation matrix and decision 181; the promotion's two named residual
  exceptions and the ticket's pre-split line-count provenance are now stated; and a player-chosen
  restore whose pre-restore archive cannot be written no longer has its preserved folder called
  "refused" in the promotion's account.
- Machine state kept as evidence: world `w-20261001-857c` with `damaged-20261001-043214/` and its three
  archives, on the acceptance machine.
