# docs/ — document-system rules

Binding rules for every page under `docs/`. Human entry: [README.md](README.md).
**Status:** these rules supersede the per-page sibling pairing contract that
[i18n/README.md](i18n/README.md) still describes; that file survives only until the migration
finishes.

## 1. Where a page lives

Two human blocks, path for path identical:

- `zh/` — Chinese; `en/` — English. Same directories, same file names, same page set.
- Inside a block: `start/` (first run, read in order), `how-to/` (one task per page), `internals/`
  (why it works this way), `reference/` (lookup: API, protocol, configuration, matrices, glossary),
  `contributing/` (build, gates, review, this standard).
- `standard/` — document-system registries (terminology, alignment record).
- `contracts/` — machine baselines and tables that code and gates read.
- `backlog/`, `evidence/`, `decisions/`, `history/`, `phases/` — process records: not part of the
  human navigation, never translated.
- Each directory carries `README.md` as the human index; `AGENTS.md` sits only at junctions
  (`docs/`, `docs/en/`, `docs/zh/`, `src/`, `tests/`, `tools/`), holds guidance rather than
  knowledge, and stays under its byte ceiling.

Everything that is not a process record belongs in these two blocks, in both languages: the
architecture and protocol explanations, the mod API contract, the feature tables, and the operations
and contribution knowledge. A document that only records what a past cycle did — a phase plan, an
audit, a delivery self-check, a decision register — stays out of the blocks, and its conclusions are
absorbed into the pages that need them.

## 2. What a page looks like

One page answers one question, and its title says what the reader can then do.

Breadcrumb head → one-sentence purpose → difficulty and prerequisites → steps → a runnable
example → why it works this way (link into `internals/`) → pitfalls → how to verify success →
`Related reading` (3–6 links) → breadcrumb tail.

Depth rule: a `start/` page shows the single path that works and then stops; the detail lives in
`how-to/` or `internals/` and is linked, never duplicated.

## 3. Language

- Both blocks hold the same page set; a page is added, renamed, moved or deleted on both sides in
  the same change.
- The Chinese page addresses Chinese readers instead of following the English sentence by sentence:
  same facts, natural Chinese.
- The language switcher exists only in `docs/README.md`; sub-pages navigate by breadcrumb.
- Project words are written exactly as `standard/terminology` records them; a new word is recorded
  there before its first use. Invented words, stiff translations and unexplained jargon are
  defects, not style.

## 4. Links

- The first use of a project word in a page links to `reference/glossary.md`.
- Every page ends with 3–6 `Related reading` links: what the reader most likely needs next.
- Breadcrumbs name the real path (`Documentation > How-to > Send a network message`) and link to
  `docs/README.md` plus the section index.

## 5. Truth

- Commands, outputs and code excerpts come from a run, never from memory.
- A claim about our own code cites the path plus the quoted text, never a line number (it drifts);
  `reversing/` may carry line numbers because that tree is never edited.
- Behaviour that only a real two-client session can confirm is marked as awaiting the user's
  acceptance run; green tests are not that evidence.

## 6. What machines check, and what they cannot

Gates check path parity between the two blocks, link targets that exist, the page shape above, the
`AGENTS.md` byte ceilings and the terminology blacklist. A gate proves presence and shape; it cannot
tell whether a page teaches. Wording quality remains a review duty.

## 7. Decay control

- Editing one side of a page obliges the other side in the same change; `standard/alignment.txt`
  records each confirmed pair, and a gate reports drift.
- Keep distilled conclusions (decisions, rule-to-gate maps, checklists, self-checks). Delete process
  logs once their conclusions are absorbed and inbound links re-pointed — delete, do not archive:
  git keeps the history, and an archive invites stale reading.
