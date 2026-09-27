# docs/ — document-system rules

Binding rules for every page under `docs/`. Human entry: [README.md](README.md). The full statements,
the field-by-field page shape and its examples live in
[`en/contributing/documentation-standard.md`](en/contributing/documentation-standard.md).

## 1. Where a page lives

Two human blocks, path for path identical: `zh/` (Chinese) and `en/` (English) — same directories, same
file names, same page set. Inside a block: `start/` (first run, read in order), `how-to/` (one task per
page), `internals/` (why it works this way), `reference/` (lookup: API, protocol, configuration,
matrices, glossary), `contributing/` (build, gates, review, this standard).

- `standard/` — document-system registries (terminology, alignment record); `contracts/` — machine
  baselines and tables that code and gates read. `backlog/`, `evidence/`, `decisions/` — process records:
  never translated, outside the human navigation.
- `architecture/`, `development/`, `acceptance/` — specs and agent-facing pages: English only. An
  agent-only area indexes itself with `AGENTS.md` (never `README.md`) and keeps machine facts in the
  gitignored `AGENTS.local.md` beside it.
- Every instruction file below the repository root stays under its 5,120-byte ceiling
  (`AgentInstructionBudgetGateTests`): it routes and constrains, it never carries knowledge.
- Each human directory carries `README.md` as its index; an index names its own directory and lists that
  section's pages.
- Everything a reader needs belongs in the two blocks, in both languages. A document that only records
  what a past cycle did stays out of the blocks; its conclusions are absorbed into the pages that need
  them.

## 2. What a page looks like

One page answers one question, and its title says what the reader can then do. Breadcrumb head →
thematic break → one-sentence purpose → difficulty and prerequisites → steps → a runnable example → why
it works this way (link into `internals/`) → pitfalls → how to verify success → `Related reading` (3–6
links) → thematic break → breadcrumb tail. A `start/` page shows the single path that works and stops;
detail lives in `how-to/` or `internals/`, linked, never duplicated.

## 3. Language

- Both blocks hold the same page set; a page is added, renamed, moved or deleted on both sides in the
  same change.
- The Chinese page addresses Chinese readers instead of following the English sentence by sentence: same
  facts, natural Chinese.
- The language switcher lives only in the three entry pages (`docs/README.md` and the two block
  overviews); sub-pages navigate by breadcrumb.
- Project words are written exactly as `standard/terminology` records them; a new word is recorded there
  before its first use.
- Chinese punctuation joins Chinese text: `，。、；：？！`, Chinese quotes and full-width parentheses; ASCII
  punctuation stays inside code, paths, identifiers and any English name or phrase.

## 4. Links

- The first use of a project word in a page links to `reference/glossary.md`.
- Every content page ends with 3–6 `Related reading` links: what the reader needs next.
- Breadcrumbs name the real path and link to the block's own overview plus the section index; the
  language switch is not part of a breadcrumb.

## 5. Truth

- Commands, outputs and code excerpts come from a run, never from memory.
- A claim about our own code cites the path plus the quoted text, never a line number (it drifts);
  `reversing/` may carry line numbers because that tree is never edited.
- Behaviour only a real two-client session can confirm awaits the agent-run acceptance in
  [`acceptance/workflow.md`](acceptance/workflow.md); green tests are not that evidence.

## 6. Decay control

- Editing one side obliges the other in the same change; `standard/alignment.txt` records each confirmed
  pair, and a gate reports drift.
- Keep distilled conclusions (decisions, rule-to-gate maps, checklists, self-checks). Delete process logs
  once absorbed and their inbound links re-pointed — delete, do not archive: git keeps the history and an
  archive invites stale reading.
