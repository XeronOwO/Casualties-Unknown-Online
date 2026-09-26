# Two censuses, now gates — self-check (2026-09-26)

Ticket: `docs/backlog/review/catalogue-and-manifest-census-drift.md`. Cycle scope: the catalogue's
declared-and-unread keys, the self-check MANIFEST's incompleteness and its intro's retired path, and the
two census gates that keep both honest (decision 229).

## 1. Mechanism inventory

| # | Mechanism | Evidence | Change | Evidence (ours) |
|---|---|---|---|---|
| 1 | The catalogue's two declared tables (`English` and `Chinese` in `LocalizationCatalog.cs`, `["key"] = "text"` entries) | the file as it stood at HEAD: 561 lines, 198 rows in the manifest against 263 non-manifest markdown files | 73 declared-and-unread keys deleted from both tables; the file is 416 lines | the gate's `EveryDeclaredKey_IsReadByProductSource` + `BothLanguageTables_DeclareTheSameKeySet` |
| 2 | How a key reaches the catalogue: the accessors `ILocalizationService.T` / `.Format` and the UI wrappers `OnlineUiContext.T` / `.F` | `LocalizationService.cs`, `OnlineUiContext.cs`, and a survey of every `.(T\|F\|Format)(` call site in `src/` | the census counts an accessor call's string literals, and `SourceScan.Flatten` drops comments while keeping literals | the matcher samples: a ternary read counts, a commented-out read, a log literal and prose do not |
| 3 | The interpolated key space | `OnlineUiPreferencesDrawer` builds `ctx.T($"prefs.color.{ColorKeys[…]")`; the eight `prefs.color.*` keys are declared | kept: an interpolated argument declares a key space, and every declared key under it counts as read | `TheCensus_ReadsAnInterpolatedKeySpace`; the eight keys survived the sweep |
| 4 | The key handed to a key-carrying helper | `OnlineUiAdminDrawer` passes raw keys (`"admin.rule_piggyback_weight"`, `"admin.rule_native_binding_parity"`) to its number and parity helpers, whose `labelKey` parameter is resolved by `ctx.T(labelKey)` inside the helper | kept: the `labelKey` convention is derived from the source and its premise is asserted | `EveryKeyCarryingHelper_ResolvesItsKeyParameter`; both keys survived the sweep and their labels still render |
| 5 | The key-producing helper | `OnlineUiWorldsDrawer.KindKeyOf` returns three `worlds.kind.*` literals that reach `ctx.T` at the call site | kept as a declared helper, with its definition count, its literals and their declaration asserted | `EveryKeyProducingHelper_StillReturnsDeclaredKeys` |
| 6 | The self-check MANIFEST and the index claim above it | `docs/evidence/selfchecks/MANIFEST.md` (198 rows) and `docs/evidence/selfchecks/README.md` ("the complete per-file index") | 66 rows added (the 65 files the red listed as unindexed plus this cycle's own sheet) against the 264 markdown files now under the tree; the intro's `docs/selfchecks/` path corrected | `SelfcheckManifestGateTests` — completeness, uniqueness, resolvability, the directory claim, and a floor |
| 7 | The keys the review's exploratory census reported | the previous cycle's review: 81 declared-and-unread keys across `src/`, `tests/` and `tools/` | reconciled to 73 (see §8: eight `prefs.color.*` keys the interpolation shape keeps, and one false positive, `players.section`, which `OnlineUiPlayersDrawer` reads) | the red record in §5 |

## 2. What decides it

The ticket carries the decision the previous cycle's review recorded: an index that asserts completeness
while reality moved on is fixed once, and the fix is a gate rather than a sweep. No new user-facing
decision is taken here, and no work-item question was asked. The disposal rule applied to each key is
evidence-based, not a preference: a key survives only if some source reads it, or a declared read shape
produces it; otherwise it is deleted from both tables. The `medical.*` family's disposal is settled by the
project's own repeated ruling that the native `WoundView` is the only medical surface — the 59 keys are the
rejected CUO IMGUI panel's vocabulary (`review/remote-player-medical-panel.md`), so they are deleted rather than reserved.

## 3. Whole-family audit

| Family | Verdict | Reason |
|---|---|---|
| `medical.*` (59 keys) | DELETED | the rejected CUO IMGUI medical panel's vocabulary; the native `WoundView` remote focus is the only medical surface |
| `member.*` (3), `hud.*` (3), `common.*` (3) | DELETED | superseded leftovers; the surfaces read other keys (`member.status_*`, `common.active`/`idle`) |
| `chat.send`, `home.already_in_lobby`, `ip.session_title`, `lobby.title`, `players.not_in_lobby` | DELETED | stale duplicates of the keys their surfaces read (`home.already_in_session`, `players.not_in_session`); the `chat.send` key itself appears nowhere outside the two tables (the only "chat send" in `src/` is a prose comment) |
| `prefs.color.*` (8 keys) | KEPT | read through the interpolated key space; the review's exploratory census could not see it |
| `admin.rule_piggyback_weight`, `admin.rule_native_binding_parity` | KEPT | handed to helpers whose `labelKey` parameter the helper resolves; the labels render through those reads |
| `worlds.kind.*` (3 keys) | KEPT | returned by the declared key-producing helper |
| The remaining ~190 keys | UNCHANGED | read by a literal accessor call, which the gate re-verifies on every run |
| The two tables | IN STEP | the parity fact; the sweep removed the same 73 keys from both |
| The MANIFEST | COMPLETED | every markdown file under the tree carries exactly one row, and no row points at a file that is gone |
| `docs/evidence/selfchecks/README.md` | UNCHANGED | its "complete per-file index" claim is now true and gated instead of being rewritten |
| The console-page family (previous cycle) | UNCHANGED | its pin still holds: the six pages, the drawer's absence and the `tab.`/`console.` census |
| Wire protocol, saves, host rules | UNCHANGED | no key had a reader, and nothing rendered differently |

## 4. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| The declared tables | 73 keys removed from each, 561 → 415 lines | `EveryDeclaredKey_IsReadByProductSource`, `BothLanguageTables_DeclareTheSameKeySet`, `TheCensus_MeetsItsFloor` |
| The read census | accessor literals, interpolated spaces, `labelKey` helpers, declared key-producing helpers; comments stripped | eleven matcher samples, positive and negative, run against the same pure functions the facts use |
| The key-carrying convention | derived from the source and asserted, not listed | `EveryKeyCarryingHelper_ResolvesItsKeyParameter` (a `labelKey` parameter that nothing resolves fails) |
| The key-producing helper | declared, with its definition count and its literals asserted | `EveryKeyProducingHelper_StillReturnsDeclaredKeys` |
| The dangling direction | a read key the catalogue does not declare fails | `EveryKeyTheSourceReads_IsDeclared` + `TheGate_FlagsAReadKeyThatIsNotDeclared` |
| The MANIFEST | 66 rows added, intro path fixed | `EverySelfcheckFile_HasExactlyOneManifestRow`, `EveryRow_NamesASelfcheckFileThatExists`, `TheManifest_NamesTheDirectoryItIndexes`, `TheIndex_MeetsItsFloor` |
| The manifest matcher | table rows only; prose, a bullet and a note do not index a file | `TheIndex_ReadsOnlyTheRowShape`, `TheIndex_ReportsAFileWithoutARow`, `TheIndex_ReportsARowWithoutAFile` |

## 5. Red → green record

The red was taken on the HEAD-shaped tree, rebuilt outside the repository from git (`git archive HEAD`
plus this cycle's three new files): HEAD's `LocalizationCatalog.cs` (561 lines), HEAD's 198-row MANIFEST
with its stale intro path, and the 263 other markdown files.

- **Red, with the DELIVERED matchers: 4 failed / 22 passed / 26**, exit 1
  (`%TEMP%/cuo-red3-census-gate.txt`). The four failures are the contract itself:
  `EveryDeclaredKey_IsReadByProductSource` (the 73-key list), `EverySelfcheckFile_HasExactlyOneManifestRow`
  (65 files), `TheIndex_MeetsItsFloor` (198 rows) and `TheManifest_NamesTheDirectoryItIndexes`. The first
  two messages are the work list this cycle executed.
- **Earlier reds, kept as matcher history rather than as verification**: 5 failed / 16 passed / 21 before
  the census learned the key-carrying shape (`%TEMP%/cuo-red-census-gate.txt`), and 5 failed / 19 passed /
  24 after that shape was added but before `KeyCarryingMethods` stopped matching the accessor call
  `T(labelKey)` itself (`%TEMP%/cuo-red2-census-gate.txt`). The independent review rebuilt that third
  state and found this document had mislabelled the second run as "the delivered matchers" (review M1);
  the red above is the re-take, and neither earlier run is reproducible from the delivered tree.
- **Green** — after the sweep, the index and the review's findings: gates **26 passed / 0 failed**, exit 0
  (`%TEMP%/cuo-greengates2-census.txt`); the localisation and Online UI families **109 passed / 0 failed**,
  exit 0 (`%TEMP%/cuo-family-census.txt`).
- What the red tree contained, exactly: HEAD's catalogue and MANIFEST with this cycle's three new files
  added — so no failure can be attributed to anything but the unfixed state.

## 6. Verification

| Claim | How it was checked | Result |
|---|---|---|
| The two census gates pass on the delivered tree | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --filter "FullyQualifiedName~LocalizationCatalogueGateTests\|FullyQualifiedName~SelfcheckManifestGateTests"` | 26 passed / 0 failed, exit 0 (`%TEMP%/cuo-greengates2-census.txt`); the run before the review's two added samples was 24/24 (`%TEMP%/cuo-greengates-census.txt`) |
| The localisation and Online UI families did not regress | `dotnet test tests/CasualtiesUnknownOnline.Tests --filter "FullyQualifiedName~Localization\|FullyQualifiedName~OnlineUi"` | 109 passed / 0 failed, exit 0 (`%TEMP%/cuo-family-census.txt`) |
| The repository gates hold | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` (unfiltered) | 232 passed / 1 failed, and the one failure is `DeliveryChecklist_NoIncompleteRequiredBoxes` while this cycle's own checklist is still open (`%TEMP%/cuo-gates-census-final.txt`) — which is why the full run below filters that gate out |
| The build is clean | the build inside the full suite below | 0 warnings / 0 errors (`%TEMP%/cuo-full-prereview3.txt`) |
| The pre-review full suite runs green with build | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist"` | main 4075 passed + gates 232 passed, exit 0 (`%TEMP%/cuo-full-prereview3.txt`). Its first attempt (`%TEMP%/cuo-full-prereview2.txt`) caught a defect the focused runs cannot: `BacklogReferenceGateTests.EveryBacklogReference_ResolvesToATicket` flagged this cycle's citation of a ticket that does not exist (`review/remote-medical-panel.md`); the citation was corrected to `review/remote-player-medical-panel.md` |
| Formatting is clean | `dotnet format CasualtiesUnknownOnline.slnx` with its exit code captured and `git status --short` compared before and after | exit 0, and the two status files are byte-identical — no file was rewritten (`%TEMP%/cuo-format3-census.txt`, `%TEMP%/cuo-status-before-format2.txt` / `-after-format2.txt`); the three new files were normalized to CRLF before it, which is what the review's m5 asked for |
| The whole suite runs green with the checklist checked | `dotnet test CasualtiesUnknownOnline.slnx` (with build), unfiltered | main 4075 passed + gates 235 passed, exit 0 (`%TEMP%/cuo-full5-census.txt`). Its first attempt (`%TEMP%/cuo-full4-census.txt`) was red on `DeliveryChecklist_NoIncompleteRequiredBoxes`: this cycle's item 5 had received its evidence suffix on the continuation line without its checkbox being flipped, so the row was re-anchored from the `- [ ]` line — the gate doing its job |

## 7. Adversarial review round

An independent reviewer (fresh context, frozen working tree; full report at
`%TEMP%/cuo-review-catalogue-census.md`, its ports and mutation harnesses under `%TEMP%/cuo-review-work/`)
returned **0 blocker / 1 major / 7 minor / 5 nit**. It rebuilt the HEAD-shaped tree from git and reproduced
every load-bearing number itself: the 73-key orphan set (symmetric difference empty, 146 deleted rows, 0
added), zero exact-token readers for any deleted key in `src|tests|tools`, the `prefs.color.` space being
exact against `ColorKeys`, all five `labelKey` helpers resolving, the green gate run, the 109-case family
run, the 232 + 4075 full suite, and the manifest's completeness (264 files, 264 rows, 0 unindexed, 0
dangling, 0 duplicated). Its sharpest attacks — a hidden reader, a matcher that can be fooled, a
manifest status that overstates — found no blocker.

Disposition, all in this commit:

- **M1 — the second red run was not reproducible with the delivered matchers** (it predates the
  `KeyCarryingMethods` filter that stops the `labelKey` pattern from capturing the accessor `T` itself), and
  the delivery checklist cited it as verification evidence. Fixed: the red was re-taken on the HEAD-shaped
  tree with the delivered matchers (**4 failed / 22 passed / 26**, `%TEMP%/cuo-red3-census-gate.txt`), §5
  now separates the two earlier runs as matcher history, and the checklist line carries the re-taken number.
- **m1 — the delivered catalogue is 415 lines, not 416.** Fixed in the ticket and §4.
- **m2 — 66 rows were added against 264 files, not 65 against 263** (the red-tree gap plus this
  cycle's own new sheet). Fixed in the ticket, §1 and §4.
- **m3 — the ticket's gap section claimed `players.section` has no reader** while
  `OnlineUiPlayersDrawer` reads it; the real orphan in that family is `players.not_in_lobby`. Fixed: the
  ticket names the exploratory census's false positive, and §8 states the reconciliation exactly.
- **m4 — §6 pointed at the red checklist run as if it were the green unfiltered record.** Fixed.
- **m5 — one line of `SourceScan.cs` was LF in a CRLF file.** Fixed by the format pass recorded in §6.
- **m6 — the census had an undocumented SILENT direction**, sharpest shape: an interpolated key
  whose hole is not the last segment (`ctx.T($"hud.{kind}.title")`) opened the prefix `hud.` and kept every
  declared `hud.*` key alive with no reader. Fixed in the matcher, not only in prose: `KeyPrefixes` accepts
  a hole only when it ends the literal, and the census reads only an accessor's FIRST argument (a
  key-shaped literal in a later argument is a message parameter). Two negative samples pin both behaviours,
  and the one residual silent shape — an unrelated `Format` method whose first argument happens to be
  a key-shaped literal — is recorded in §8.
- **m7 — eight citations in the first new `normative-gates.md` row dropped the `TheCensus_` prefix**
  and resolved to nothing, because the backlog anchor gate does not check the shorthand form. Fixed, and
  the row now also cites the two samples this round added.
- **n1 — the `chat.send` wording is exact now; **n2 — the census's unresolved counter is
  bounded by `UnresolvedCeiling` instead of being carried unused; **n3 — `MemberCallArguments`'s doc
  comment describes the matcher it has; **n4 — the stage-3 `historical` row's note names the drift it
  rests on (the sheet's `ProtocolVersion.Current = 14` against the tree's 41); **n5 — the two newly
  indexed sheets that point at a retired `docs/backlog/todo/` path carry that in their notes.

The reviewer's own limits: it did not run `dotnet format` (the tree was frozen) and observed no rendering; it
read 11 of the 66 newly indexed sheets' heads and recorded that the rest were checked mechanically, so
whether each `current` status is judged well stays a review duty the gate leaves open — the same limit
§8 states.

## 8. Limits

- The census scans `src/` alone. A key read only by a test is dead product data by design, and test
  fixtures embed read-shaped text that must not count as a read — so the surface is narrower than the
  review's exploratory census, which also scanned `tests/` and `tools/`. The reconciliation with the exploratory 81 is exact:
  eight `prefs.color.*` keys are kept by the interpolation shape, and `players.section` — reported
  unread there — is read by `OnlineUiPlayersDrawer` (its exchange with `players.not_in_lobby`, a genuine
  orphan absent from the exploratory list, nets to zero), so 81 — 8 = 73. Two keys
  (`admin.rule_piggyback_weight`, `admin.rule_native_binding_parity`) went the other way: they are live and
  reached the catalogue through a helper parameter, which is now a counted shape.
- The census's silent direction, named after the review round: an accessor's first argument that is a
  key-shaped literal counts as a read even when the receiver is an unrelated `Format` method, so a dead key
  sharing a name with such an argument would stay alive. The two sharper shapes that round found are closed
  in the matcher (an interior-hole interpolation no longer opens a prefix, a later argument's literal is no
  longer a read), and this remainder is narrow: it needs a `Format`-shaped receiver outside the localisation
  surface whose first argument is a catalogue key's exact text.
- The census is text, not a compiler. An accessor reached through a receiver it cannot resolve — a
  pass-through such as `OnlineUiContext.T`, an echoed `labelKey` argument, an unrelated `Format` method
  that shares the name — is a recorded limit, and the limit is in the loud direction: a key that only such
  a site could produce is reported as unread, so a live key is never swept silently. The converse (a site
  that produces a key text without declaring it) stays invisible until it renders as its own key text.
- The census reads declarations and reads, never types: a key passed through a variable whose name does not
  follow the `labelKey` convention is outside the shape, and it fails loudly as an orphan rather than
  passing as read.
- The MANIFEST pass is a triage, not a re-audit: presence, uniqueness, resolvability and each sheet's own
  header for the current-versus-historical status. Whether a note is judged well stays a review duty — the
  manifest's own boilerplate says as much for the rows it does not single out.
- No rendered string was observed: the deleted keys had no reader in the tree, which the census proves, but
  a frame is still the user's run. Nothing in this cycle changes what any surface draws.
