# Documentation-tree follow-ups and the feature-matrix path gate

Date: 2026-09-25
Scope: the code cycle after the legacy documentation-tree migration. Four leftover defects — three
comment or doc-path defects in `src/`, one duplicated spelling in the evidence tree — the English
`how-to` breadcrumb brought back to the spelling both standards give, and the backlog ticket
`feature-matrix-tool-path-gate`: a gate over the matrix tools' path literals and the feature columns of
both matrices. The `src/` edits are comment-only: no runtime behaviour, no wire and no save change, so
there is nothing to deploy and no real-client claim is made.

## What landed

- **Four leftovers closed, one of them a family.** `ModContentPolicy`'s id-grammar comment said
  `[a-z0-9][a-z0-9_.-]{0,95}` while the grammar it describes is `{0,94}` and the constant beside the
  parser is `ContentId.MaxPathLength = 95`; the comment now reads `{0,94}`, and the same wrong spelling
  in the mechanism table of `docs/evidence/selfchecks/tooling/content-id-selfcheck.md` was corrected
  with it. Two XML comments still pointed at `docs/game-internals.md`, a file the migration deleted:
  they now name `docs/en/internals/game-internals.md`, and the one that cited the vanished section
  `Clone & Render Chain` cites the heading the page actually carries — "A remote player is a clone of
  the scene's own character".
- **The English `how-to` breadcrumb matches the standard.** `docs/AGENTS.md` and
  `docs/en/contributing/documentation-standard.md` both write the breadcrumb segment as `How-to`, and
  the eight English pages of that section wrote `How to`; they now write `How-to`. The page title
  (`How to do one thing`) and the block index's reading line (`Do one thing`) keep their own roles —
  one names the section's path, one says what the section is, one says what the reader can do — so the
  three are consistent by role rather than by repeating one string. The Chinese block was already
  internally consistent and is untouched.
- **The tools' matrix literals have a gate.** `FeatureMatrixToolPathGateTests` derives its scan surface
  from `tools/*.ps1`, reads every `Join-Path $scriptDir` path literal, keeps a discovery floor over the
  scripts it walked, and asserts both that every literal names a file that exists and that each matrix
  tool still resolves its own matrix — the pairing is per tool, so the tool that leaves the scan cannot
  hide behind another tool's literal. Nothing read those literals before: the documentation and backlog
  link gates walk markdown links, not PowerShell strings.
- **Both matrices' feature columns are one list in three places.** The same gate compares each CSV
  header (`item` + 12 feature columns, `entity` + 9) with the column list under the matching section of
  both `reference/feature-matrices.md` pages, in order, so a renamed, added or dropped column fails
  until the CSV and both pages agree. The entity page's own "10 columns" figure had drifted from its
  9-column table and was corrected with it, in both blocks.
- **The ticket moved.** `docs/backlog/todo/feature-matrix-tool-path-gate.md` became
  `docs/backlog/review/feature-matrix-tool-path-gate.md` with its `## What landed` record, and the
  backlog index row moved from the Todo section to Review. `docs/backlog/todo/` now holds only its
  `.gitkeep`.

## Mechanism inventory

| Mechanism | Change | Evidence |
| --- | --- | --- |
| Id grammar spelling | Both copies of the wrong `{0,95}` — the policy comment and the ContentId self-check's mechanism table — stop contradicting the grammar they quote | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModContentPolicy.cs`, `docs/evidence/selfchecks/tooling/content-id-selfcheck.md`; `ContentId.cs` `MaxPathLength` and its `{0,94}` grammar |
| Dead doc path in `src/` | Two XML comments point at the page that exists, in the block that owns it | `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldStartParams.cs`, `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs`; the heading list of `docs/en/internals/game-internals.md` |
| Vanished section name | The patch comment cites a real heading instead of the section the deleted page had | `BodyPatches.cs` doc comment; `docs/en/internals/game-internals.md` |
| Breadcrumb segment | The eight English `how-to` pages spell the section the way both standards do | `docs/en/how-to/README.md` + the seven task pages; `docs/en/contributing/documentation-standard.md`, "Links" |
| Pair alignment record | Every edited page is re-recorded at its new contents | `docs/standard/alignment.txt` (the eight `docs/en/how-to/` rows and the `feature-matrices` row), `git hash-object` |
| Matrix tool literals | A gate reads the tools' `Join-Path $scriptDir` literals out of `tools/*.ps1` and pairs each matrix with the tool that owns it | `FeatureMatrixToolPathGateTests.EveryScriptRelativeLiteral_NamesAFileThatExistsAndResolvesItsOwnMatrix` |
| Matrix feature columns | Each CSV header, the English page and the Chinese page are compared as one ordered list (item 12, entity 9) | `FeatureMatrixToolPathGateTests.TheMatrixHeaders_EqualTheFeatureColumnListsTheReferencePagesPublish` |
| Entity column-count drift | The reference pages' stated entity column count now equals their own table and the CSV | `docs/en/reference/feature-matrices.md`, `docs/zh/reference/feature-matrices.md`; the gate above |
| Ticket lifecycle | The finished ticket left `todo/` and the index section follows it | `docs/backlog/review/feature-matrix-tool-path-gate.md`, `docs/backlog/README.md` |

## Verification

| Check | Result |
| --- | --- |
| Focused filter (`FullyQualifiedName~FeatureMatrixToolPathGate`) | 4 passed / 0 failed, and 4/4 again after the negative controls were reverted |
| Negative control 1 — the pre-migration literal `..\docs\item-features-matrix.csv` put back into `tools/item-features.ps1` | red, naming `tools/item-features.ps1 → ..\docs\item-features-matrix.csv`; reverted → green |
| Negative control 2 — one column renamed on the English page (`randomroll` → `randomrollx`) | red, printing the CSV list and the page list side by side; reverted → green |
| `dotnet format CasualtiesUnknownOnline.slnx` | exit 0 (also after the review fixes) |
| `docs/standard/alignment.txt` | the eight `docs/en/how-to/` rows and the `feature-matrices` row carry hashes equal to `git hash-object` of their pages; `DocumentationTreeGateTests.EveryPair_IsRecordedAtItsCurrentContents` is green |
| Normative-gates project, after the review fixes | 149 passed / 0 failed in 5 s |
| Full suite with build, after the review fixes | 149 gates + 3803 main suite, 0 failed, exit 0 (gates 9 s, main suite 62 s) |

## Independent review round (2026-09-25)

A separate review session (fresh context, read-only, frozen worktree, full report outside the
repository at `%TEMP%\cuo-review-followups-and-matrix-gate.md`) returned 0 blocker / 4 major /
4 minor / 4 nit. Every finding was addressed in this commit:

- **major — the coverage assertion was weaker than its claim.** It checked that *some* literal resolved
  each matrix, not that the tool owning the matrix resolved it, so a tool leaving the scan could hide
  behind another literal. The gate now asserts the pairing per tool.
- **major — the directory case was undeclared.** `File.Exists` refuses a directory and refuses a build
  output a clean checkout does not have, and the limits did not say so. The ticket and this sheet now
  state what a `Join-Path $scriptDir` literal is allowed to be.
- **major — the entity column count was wrong.** Both reference pages said the entity matrix carries 10
  columns while its header is `entity` plus 9 feature columns. Both pages were corrected, their hashes
  re-recorded, and the gate now checks the entity column list too, not only the item one.
- **major — the discovery floor sat at the measured minimum.** `MinimumMatrixPathLiterals = 2` was the
  exact count, so any real contraction would have been a false positive. The floor is now over the
  scripts walked (4 of the 6 present today) and the exact contract is carried by the per-tool pairing.
- **minor / nit** — the Scope line and the `What landed` count disagreed on what the four leftovers
  are; the `git diff --numstat` figure is replaced by the reproducible statement about
  `alignment.txt`; the class comment no longer implies nothing reads a matrix path at all; the
  page-column matcher stops at `###` as well as `##`; the literal matcher's synthetic sample covers a
  doubled-quote literal. The review's note that the handoff briefing misnamed where the `{0,95}` copies
  lived is recorded here, not as a repository defect.

## Limits

- The new gate proves that a literal names an existing *file* and that each matrix's three column lists
  agree. A `Join-Path $scriptDir` literal must therefore point at something a clean checkout carries —
  a directory or a build output is reported as a failure — and the matrices' contents (row counts,
  per-row verdicts, the narrative tables) stay with `EntityFeaturesDocConsistencyTests` and with review.
- The breadcrumb change is wording: `DocumentationTreeGateTests` checks that a breadcrumb exists, that
  it resolves and that an index names its own directory, not which words the segment uses.
- The gate reads the two tool literals and the two column tables, not every path a tool mentions nor
  every table a page carries.
- Two checks the retired sibling-pairing gate used to make still have no successor — the
  language-switcher links and the structural mirror. That limitation was recorded by the migration
  cycle and is unchanged here.
- No runtime behaviour changed, so no deployment was performed and no dual-client behaviour is claimed.
