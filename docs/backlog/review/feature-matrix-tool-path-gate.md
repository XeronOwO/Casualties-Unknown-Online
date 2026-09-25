# Feature-matrix tooling: no gate covers the CSV paths or the tool/page agreement

- Status: Review
- Priority: Low-Medium
- Category: Tooling / documentation gates
- Source: Independent adversarial review of the contracts documentation cycle (2026-09-25)
- Related: `docs/contracts/README.md`, `tools/item-features.ps1`, `tools/entity-features.ps1`

## Problem (evidence)

The two feature matrices moved into `docs/contracts/`, and every path that reads them was updated by
hand in that cycle. Nothing in the tree fails when one of those paths goes stale again:

- **The tool paths have no test.** Each tool resolves its matrix through a
  `Join-Path $scriptDir '..\docs\contracts\...'` literal. Both literals were wrong before that cycle —
  they named `docs\item-features-matrix.csv` and `docs\entity-features-matrix.csv`, paths that never
  existed in the repository — and the whole gate set stayed green while both tools were dead. The
  reviewer reproduced the failure by restoring the old literal and running `validate`: exit 1 with a
  file-not-found error from `tools/item-features.ps1`.
- **The item matrix has no consistency test.** `EntityFeaturesDocConsistencyTests` copies
  `entity-features-matrix.csv` and the entity narrative page into the test output and cross-checks the
  page's entity tables against the CSV — entity coverage and the sync verdict, deliberately not the
  path wording, which the page phrases in its own words; no test reads `item-features-matrix.csv` at
  all, so its shape and its agreement with `docs/en|zh/reference/feature-matrices.md` are review-only
  facts.
- **What is already covered:** the markdown links to the CSVs (`docs/contracts/README.md`, and the
  narrative pages `docs/en/reference/feature-matrices.md` + `docs/zh/reference/feature-matrices.md`)
  are walked by the documentation-tree and backlog link gates, so a move that forgets a link fails. The
  gap is the tool literals and the inline-code path mentions, which no gate reads.

## Suggested shape

A test in the normative-gates project that resolves each matrix literal in `tools/*.ps1` and asserts
the file exists, plus — optionally — copying the item CSV to the test output and asserting its header
carries the 12 feature columns the reference page names. Follow the gate discipline in
`docs/AGENTS.md`: derive the scan surface from `tools/*.ps1`, keep a census floor, pin the matcher with
a synthetic case, and re-run the three-way acceptance (HEAD red, worktree green, no false positives).

## Why it is not part of the contracts cycle

That cycle was a path migration: it moved the artifacts, re-pointed every reader and reference, and
left the item matrix's missing coverage as it found it. Widening the gate surface is a separate
change with its own matcher contract, so the reviewer recorded it instead of folding it in.

## What landed (2026-09-25)

- **The script-relative literals have a gate.** `FeatureMatrixToolPathGateTests` derives its scan
  surface from `tools/*.ps1`, reads every `Join-Path $scriptDir` path literal, keeps a discovery floor
  over the scripts it walked, and asserts both that every literal names a file that exists and that each
  matrix tool still resolves its own matrix — the pairing is per tool, so a tool leaving the scan cannot
  hide behind another tool's literal. The check runs against the repository tree, so a tool that stops
  resolving its matrix fails here instead of at the next `validate`.
- **Both matrices' columns are one list in three places.** The same gate compares each CSV header
  (`item` + 12 feature columns, `entity` + 9) with the column list under the matching section of both
  reference pages, in order: the CSV, `docs/en/reference/feature-matrices.md` and
  `docs/zh/reference/feature-matrices.md` must agree, so a renamed, added or dropped column fails until
  CSV and both pages are updated together. Building that comparison also surfaced the entity page's
  stated "10 columns", which disagreed with its own 9-column table and was corrected in both blocks.
- **Both matchers are pinned.** `TheLiteralMatcher_ReadsTheJoinPathFormAndIgnoresTheOtherForms` fixes
  the PowerShell reading (the `$scriptDir` form, single- and double-quoted, and the forms it must not
  read) and `ThePageColumnMatcher_ReadsOnlyTheBacktickedFirstColumn` fixes the Markdown reading
  (backticked single-word first column, section-bounded).

### Verification

- `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --filter "FullyQualifiedName~FeatureMatrixToolPathGate"`:
  4 passed / 0 failed, and 4/4 again after the negative controls were reverted.
- Negative control 1 — the pre-migration literal `..\docs\item-features-matrix.csv` put back into
  `tools/item-features.ps1`: red, naming `tools/item-features.ps1 → ..\docs\item-features-matrix.csv`.
- Negative control 2 — one column renamed on the English reference page (`randomroll` → `randomrollx`):
  red, printing the CSV list and the page list side by side.
- No "HEAD red" control: the pre-change tree already resolves both matrices correctly, so this gate adds
  a missing line of defence rather than replacing a broken one — the two controls above are what proves
  it bites.
- Normative-gates project after the review fixes: 149 passed / 0 failed.
- Full suite with build after the review fixes: 149 gates + 3803 main suite, 0 failed, exit 0.
- `dotnet format CasualtiesUnknownOnline.slnx`: exit 0.

### Limits

- The gate proves that a literal names an existing *file* and that the column lists agree. A
  `Join-Path $scriptDir` literal must therefore point at something a clean checkout carries: a directory
  or a build output is reported as a failure, which is the price of checking existence at all.
- It does not read the matrix contents: row count, per-row verdicts and the narrative's entity tables
  stay with `EntityFeaturesDocConsistencyTests` and with review.
- The column counts are pinned at 12 and 9 because the reference pages state them in prose; a deliberate
  change updates the pages and this gate together.
