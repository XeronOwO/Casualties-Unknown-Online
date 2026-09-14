# S4.2 — The restore account becomes player-visible, and every save event gets one log line

- Status: Review (landed 2026-09-14; awaiting the final unified acceptance pass)
- Priority: High
- Category: Persistence / save system
- Source: `docs/backlog/in-progress/save-multiplayer-restore-and-backups.md` scopes 2 and 3
- Related: decision 163 (repair mode, per-entry salvage), decision 168 (a restore reports its
  live-world halves), decision 177 (the claim verdict), decision 179 (this stage's surface and
  observability rule), `docs/architecture/save-archive-format.md` §6,
  `docs/backlog/review/save-guest-restore-claim-and-legacy-store-retirement.md` (S4.1)

## The gap

§6 says it twice and both times in the imperative: "Every skipped entry, every fallback and every
mismatch is surfaced **in-game** (not only in the log): the count per domain, the reason, and the
affected content id. Silent loss is forbidden." And S4.1 added a third class to that account — a
stored character refused to an ambiguous claimant (decision 177).

What actually happened to all three: `WorldRestoreApplier` folded them into
`WorldContinueOutcome.Summary`, and the only production consumer of that summary was
`RunSaveCoordinator`, which wrote it to `CUO.log` at Information level. The command console rendered
the CUT report and the restore's live-world half, but not the click-time account — so the one
surface a player has said nothing about the half that decides whether the run starts at all. Three
concrete failures followed from that:

- a refused continue (a damaged archive, a layer-end snapshot that contradicts itself) blocked the
  native `LoadRun` (decision 165) and the player saw only "the button did nothing";
- a restore that dropped a skipped entry, a stored character or fell back to a backup looked exactly
  like a clean one;
- an attempt that was applied and then abandoned (no run baseline published) had no player-visible
  end at all.

Scope 3 was missing the other half of the same coin: no single log line carried the facts needed to
diagnose a mismatch. The cut's counts and the restore's counts were written by different types, in
different vocabularies, on different lines — a "my base is gone" report meant grepping four lines
across two layers and still guessing which domain disagreed.

## What landed

- **`WorldRestoreReport`** (Runtime/Session/Persistence) is the attempt's account as a value: the
  world id, a three-valued `Disposition` — `Refused`, `Applied`, `Abandoned` — the one-line summary
  and the itemized `Details`. `IWorldSaveControl.RestoreReported` raises it once per resolved
  attempt: `WorldSaveService.TryContinue` raises `Applied`/`Refused`, and `AbandonRestore` raises
  `Abandoned` (the second and last word on an attempt the click had already applied).
- **The itemized account exists in the format layer.** `DamageReport.DescribeLines()` renders one
  line per reported item — the domain file, the affected content id, the reason and the detail —
  because `Describe()`'s grouped counts cannot name an id. `WorldRestoreApplier` builds the details
  from the Runtime damage (claim refusals, native fields the body cannot take) plus these lines, and
  a BACKUP FALLBACK is named in both halves: the summary says which snapshot was read
  ("restored from the live snapshot" / "restored from backup `<file>.cuoz`"), the itemized account
  carries the reader's own entry ("…so backup `<file>.cuoz` was opened instead"). The summary no
  longer embeds the source PATH — that stays in the log line, where a path belongs.
- **The console renders it** (`CommandConsoleService.OnRestoreReported`): the disposition becomes ONE
  notification ("CUO continue refused: …", "CUO restored world `w-…` with 3 damaged item(s); the
  console history names them"), and the account's own line plus every itemized line go into the
  history. `ConsoleLine` gained `Notifiable`, and the closed console's selection moved out of the
  IMGUI overlay into a pure Runtime policy (`ConsoleNotificationPolicy.Recent`) so "what is a
  notification" is machine-verified instead of living in a `for` loop in the UI.
- **ONE log line per cut and per restore.** `WorldSnapshotCounts` describes the same domains in the
  same order on both sides (items, players, enemies incl. tombstones, fluids, world-entity rows,
  characters, world blocks, world transients, native run fields), so the two lines are directly
  comparable — that comparability is the contract, not the logging itself. Each line also carries the
  cut kind (`layer-end` / `mid-run`, via the format's own spelling), the cut phase, the revision and
  the layer. Two lines that used to repeat the same facts at Information level were demoted to
  Debug and keep their content: the writer's mid-run row-count line (whose counts now ride the commit
  line) and the decoder's decoded-count line (whose counts now ride the restore's account line) — and
  the adapter's duplicate `Continuing CUO world …: <summary>` line, since the restore applier logs
  that summary once with more context.

## Defect the self-review found while writing the acceptance test, fixed in the same cycle

The first version of the cut line counted the KERNEL checkpoint the writer was handed. That is the same
set as the archive's rows for a mid-run cut — and a different one for a **layer-end** cut, which drops
its in-layer rows by design (the world-rooted items, the world-entity facts, the live enemies, the
fluid chunks and the payload's world facts, §3.4/§4). The line that promises a two-line comparison
would therefore have disagreed with the restore line reading the archive it wrote, on a perfectly good
snapshot, and taught the reader to distrust both. The counts now come from `EncodedSnapshot` — the
encoder is the only thing that knows what it wrote (`EncodeWithCounts`, while `Encode` stays the
files-only projection the row-shape suites use) — and
`WorldSaveLogLineTests.LayerEndCut_LogsTheRowsTheArchiveHoldsNotTheOnesTheKernelStillHad` is the
regression test, recorded RED against the first version (the cut line said two item rows where the
archive holds one) and green after the fix. The same test pins the tombstone control: the killed
enemy's row IS carried, so the enemy count is one on both sides rather than zero.

## What the independent adversarial pass found, and what happened to each finding

Seven findings, six accepted and fixed in this cycle, one answered with evidence:

- **A refusal's reason and its itemized account** (their MAJOR): the console puts the refusal's summary in
  the HEADLINE — which is a line in the same buffer the modal console and the history read — and every
  itemized line after it, so nothing is dropped; the reviewer read the `Applied`-only guard as "the
  summary is never written", which holds for the applied case only. The real gap they pointed at WAS
  real and is closed: no test covered a `Refused` report with a non-empty detail list, which is exactly
  the decode-level refusal §6 exists for. `CommandConsoleSaveTests.RestoreReport_OfARefusalWithDamage_KeepsTheReasonInTheLineAndTheItemsInTheHistory`
  now pins it (reason announced, items in the history, items not notifiable), and the rendering rule is
  stated where it is implemented.
- **A tautological acceptance assertion** (MAJOR, test strength): `WorldSaveLogLineTests` compared the
  domain LABELS across the two lines, which passes for `7 world-block row(s)` against
  `0 world-block row(s)` — and against the pre-change code. It now parses the numbers out of both lines
  and compares VALUES per domain (and throws when a domain is missing rather than reading silence as
  agreement).
- **Unconditional `AbandonRestore` reporting** (MINOR): an abandonment with no attempt in flight (a
  public interface method, and a shape existing suites already call) would have announced a continue the
  player never started, naming the service's CURRENT world. The lifecycle now lives in
  `WorldRestoreAccountRelay`: an applied attempt is outstanding from the click until it is abandoned,
  superseded by a new run, or its live-world account closes (`WorldRestoreAudit.Reported`), and the
  report carries the ATTEMPT's world id.
- **The interface contract contradicted the implementation** (MINOR): `IWorldSaveControl.AbandonRestore`
  still said "nothing is reported". It now states the two cases — an outstanding applied attempt gets
  its final `Abandoned` report, anything else reports nothing.
- **`WorldSnapshotCounts`' documented invariant did not match `Of`'s inputs** (MINOR): the doc now says
  which domains are read off the decoded checkpoint and which are passed in.
- **`WorldCutWriteResult` kept the writer's input counts** (NIT): it now carries the same
  `EncodedSnapshot.Counts` values the log line reports, so the console's account and the log cannot name
  different numbers for one cut.
- **The class summary of `CommandConsoleSaveTests` was stale** (NIT): fixed.

Their coverage note is closed too: no test drove the PRODUCTION wiring (the rendering suites swap in a
fake save control), so "the console subscribes to the real service" was unfalsifiable.
`WorldSaveCompositionTests.ProductionRoot_WiresTheContinueAccountToTheConsole` now resolves both from a
real `CuoBootstrap` root and asserts the refusal a click produces appears in the console.

## Acceptance

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1 | A clean continue | One report, `Applied`, `Clean` true, no details, summary naming the live snapshot; the console prints one success line and the summary into the history | `WorldRestoreReportTests.CleanContinue_ReportsAppliedWithNothingLost` (a composition that CAN be clean: native reader on both sides + a character whose happiness window is the game's ten values), `CommandConsoleSaveTests.RestoreReport_OfACleanRestore_IsOneLineAndSaysNothingWasLost` |
| 2 | Two present players sharing a transport key (decision 177) | The refusal is in the itemized account, not only in the joined summary | `WorldRestoreReportTests.TwoPresentPlayersSharingAKey_ReportTheRefusedCharacterInTheItemizedAccount` |
| 3 | A damaged live snapshot, opened from a backup | Both halves name the fallback: the summary names the backup, the itemized account carries the reader's "was opened instead" entry | `WorldRestoreReportTests.BackupFallback_NamesTheBackupInBothTheSummaryAndTheItemizedAccount`, `WorldSaveLogLineTests.RestoreFromABackup_NamesTheBackupAndItsSourcePath` |
| 4 | A continue with no world at all | A `Refused` report carrying the reason, so the click that did nothing says why | `WorldRestoreReportTests.ContinueWithoutAWorld_ReportsARefusal`, `CommandConsoleSaveTests.RestoreReport_OfARefusal_NamesTheReasonTheClickDidNothing`, `.RestoreReport_OfARefusalWithDamage_KeepsTheReasonInTheLineAndTheItemsInTheHistory`, `WorldSaveCompositionTests.ProductionRoot_WiresTheContinueAccountToTheConsole` (the PRODUCTION root's wiring, end to end) |
| 5 | An attempt applied and then abandoned | A SECOND report with `Abandoned`, after the `Applied` one | `WorldRestoreReportTests.AbandonedAttempt_ReportsASecondTimeWithTheAbandonment` |
| 6 | A damaged restore as the player sees it | ONE notification (the disposition) with the itemized lines in the history and marked non-notifiable | `CommandConsoleSaveTests.RestoreReport_OfADamagedRestore_IsOneNotificationWithTheItemsInTheHistory`, `ConsoleNotificationPolicyTests` (newest-first cap, non-notifiable lines never spend the budget, faded lines dropped) |
| 7 | A skipped entry's content id | Readable in-game: the line names the domain file, the id, the reason and the detail | `DamageReportLineTests` (entry, file, backup-fallback and id-less repository entries) |
| 8 | A cut and a restore of the same snapshot | One Information line each, same domain vocabulary and the same counts for the same domains; kind, phase, revision and layer on both | `WorldSaveLogLineTests.Cut_WritesOneInformationLineWithTheKindPhaseRevisionAndCounts`, `.Restore_WritesOneInformationLineWithTheKindPhaseRevisionAndTheSameCounts`, `.LayerEndCut_LogsTheRowsTheArchiveHoldsNotTheOnesTheKernelStillHad` (red→green) |
| 9 | A cut that wrote nothing | The refusal is logged at Warning and NO commit line is written | `WorldSaveLogLineTests.RefusedCut_IsLoggedAsARefusalAndWritesNoCommitLine` |

## Verification limits

Machine-verified: the account's assembly and its three dispositions through the real service
(`WorldRestoreReportTests` drives a real cut, a real restart and a real continue), the itemized
rendering of every damage-report entry shape, the console's rendering of all four dispositions and
the notifiability of detail lines, the closed-console selection policy, the two account lines' content
AND their per-domain values, the production composition root's wiring end to end, and the gates.

Numbers for this cycle: `dotnet test CasualtiesUnknownOnline.slnx` → 3061/3061 in
`CasualtiesUnknownOnline.Tests` and 32/32 in `CasualtiesUnknownOnline.NormativeGates.Tests` (the
normative gates include the architecture shape gate this stage nearly tripped and the sync-coverage
evidence gate whose `WorldRestoreApplier.cs` reference this stage repointed). Deployment: 34 files to
the physical game directory, every hash equal to that build's output. The DEPLOYED build is the
delivery commit's own rebuild — the plugin embeds its sha in `ProductVersion` (`0.1.0+<commit>`), so it
is committed first and rebuilt and re-verified afterwards, where the same 34-file comparison is run
again.

NOT machine-verified, and NOT claimed here: how the notification LOOKS in-game (the closed console's
placement, the fade timing, the 680 px width against a long headline) and the multi-client rows. Both
need the user's pass: a host + guest restore where a claim is refused and one where a backup is
promoted, checking that the console shows one line and that the history names the items. The
notification line is deliberately short for exactly that reason — the previous behaviour pushed
whole reports into the notification area, and the wrap/clip behaviour of a long line in that window
is a rendering fact no Runtime test can prove.
