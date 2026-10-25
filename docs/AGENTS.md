# docs/ — document-system rules

Binding rules for every page under `docs/`. Read [`../AGENTS.md`](../AGENTS.md) first. Full statements, the field-by-field page
shape and examples: [`en/contributing/documentation-standard.md`](en/contributing/documentation-standard.md).

## 1. Where a page lives

- Human documentation is paired, path for path: `zh/` and `en/`, same directories, names and page set. Inside a block: `start/`
  (read in order), `how-to/` (one task per page), `internals/` (why it works this way), `reference/` (lookup), `contributing/`
  (build, gates, review).
- `standard/` holds document-system registries (terminology, alignment, census); `contracts/` holds baselines code and gates
  read. `backlog/`, `evidence/`, `decisions/` are process records: never translated, outside the human navigation.
- `architecture/`, `development/`, `acceptance/` are specs and agent-facing pages, English only. An agent-only area indexes
  itself with `AGENTS.md` and keeps machine facts in the gitignored `AGENTS.local.md` beside it.
- Every instruction file stays under its 5,120-byte ceiling (`AgentInstructionBudgetGateTests`): it routes, it never carries
  knowledge.
- Each human directory carries `README.md` as its index, naming its own directory and listing that section's pages.
- Everything a reader needs lives in the two blocks, in both languages. A document that only records what a past cycle did
  stays out; its conclusions move into the page that needs them.

## 2. What a page looks like

One page answers one question, and its title says what the reader can then do. Breadcrumb head → rule → one-sentence purpose →
difficulty and prerequisites → steps → a runnable example → why it works this way (link into `internals/`) → pitfalls → how to
verify success → `Related reading` (3–6 links) → breadcrumb tail. A `start/` page shows the single path that works and stops;
detail lives in `how-to/` or `internals/`, linked, never duplicated.

## 3. Language

- Both blocks hold the same page set; a page is added, renamed, moved or deleted on both sides in the same change.
- The Chinese page addresses Chinese readers: same facts, natural Chinese, not a sentence-by-sentence translation.
- The language switcher lives only in the three entry pages; sub-pages navigate by breadcrumb.
- Project words are written exactly as `standard/terminology` records them; a new word is registered before first use.
- Chinese punctuation joins Chinese text; ASCII punctuation stays inside code, paths, identifiers and English names.

## 4. Links

- The first use of a project word links to `reference/glossary.md`.
- Every content page ends with 3–6 `Related reading` links.
- Breadcrumbs name the real path and link to the block overview and the section index; the language switch is not a crumb.

## 5. Truth

- Commands, outputs and excerpts come from a run, never from memory.
- Our own code is cited by path plus the quoted text, never by line number; `reversing/` may carry line numbers (that tree is
  never edited).
- Behaviour only a real two-client session can confirm awaits the agent-run acceptance in [`acceptance/workflow.md`](acceptance/workflow.md);
  green tests are not that evidence.

## 6. Decay control

- Editing one side obliges the other in the same change; `standard/alignment.txt` records each confirmed pair and a gate reports
  drift.
- Keep distilled conclusions only; delete process logs once absorbed and re-point their inbound links — delete, do not archive.
- **A page earns its place by having a reader**: answer who opens it and when before landing it; a record of what a cycle did is
  not landed, its conclusion goes into the page a reader already opens.
- **Every tracked document carries a row** in [`standard/document-census.txt`](standard/document-census.txt) naming what reaches
  it: `DocumentCensusGateTests` fails on a document with no row, a row naming a missing document, and a kept document nothing
  names back.
