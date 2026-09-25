# Feature-matrix tooling: no gate covers the CSV paths or the tool/page agreement

- Status: Todo
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
