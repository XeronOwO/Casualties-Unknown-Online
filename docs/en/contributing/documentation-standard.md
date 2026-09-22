# Writing documentation

[Documentation](../README.md) > [Contributing](README.md) > Writing documentation

---

**After this page** you can add a page to the documentation, keep its other-language side in step,
and know which parts a gate checks. The binding rules live in [`../../AGENTS.md`](../../AGENTS.md),
which an agent loads automatically on entering `docs/`; this page is the human explanation of them.

## Two blocks, one page set

- `docs/en/` — English; `docs/zh/` — Chinese. Same directories, same file names, same pages: a page is
  added, renamed, moved or deleted on both sides in the same change.
- Inside a block: `start/` (first run, read in order), `how-to/` (one task per page), `internals/`
  (why it works this way), `reference/` (lookup: API, protocol, configuration, matrices, glossary) and
  `contributing/` (build, gates, review, this standard).
- `docs/standard/` holds the registries this work depends on: the terminology list and the pair
  alignment record. Machine baselines that code and gates read live beside their subject today —
  `docs/api/abstractions-api-baseline.txt`, the JSON baselines under `docs/evidence/`; the standard
  declares a `docs/contracts/` section for them, which is not populated yet.
- A document is a layer, not just a file: an agent document answers "how do I do the work" and is
  English only, while a human page answers "what is the product" and is paired English + Chinese.
- Process records — `docs/backlog/`, `docs/evidence/`, `docs/decisions/` and the per-cycle logs — stay
  English, stay where they are, and are not part of the human navigation. Their conclusions are
  absorbed into the pages; the record itself is not translated.
- Every directory carries a `README.md` as its index. An `AGENTS.md` sits only at junctions
  (`docs/`, `docs/en/`, `docs/zh/`, `src/`, `tests/`, `tools/`), routes rather than teaches, and stays
  under its byte ceiling.

## What a page looks like

One page answers one question and its title says what the reader can then do:

head breadcrumb → thematic break → one-sentence purpose ("After this page you can …") → difficulty
and prerequisites → steps → a runnable example → why it works this way → pitfalls → how to check
that it worked → `Related reading` (3–6 links) → thematic break → tail breadcrumb.

A `start/` page shows the single path that works and stops there; the detail belongs in `how-to/` or
`internals/` and is linked, never duplicated. An index page is the exception: it lists the section's pages, names its own directory
(`docs/en/how-to/`), carries the same head-and-tail breadcrumb and thematic breaks as any other page
— pointing at the entry one level above it — and needs no `Related reading`.

## Language

- The English page is written for an English reader; the Chinese page is written for a Chinese
  reader, with the same facts and natural Chinese — not a sentence-by-sentence translation.
- Project words are written exactly as [`../../standard/terminology.txt`](../../standard/terminology.txt)
  records them, and a new word is registered there before its first use, so two pages cannot invent
  two renderings of one mechanism. Invented words, stiff translations and unexplained jargon are
  defects, not style.
- Personal, uncommitted communication follows the owner's own preference, recorded in the gitignored
  `AGENTS.local.md`.
- The language switcher lives only in the three entry pages: [`../../README.md`](../../README.md),
  [`../README.md`](../README.md) and [`../../zh/README.md`](../../zh/README.md) — that is
  `docs/README.md`, `docs/en/README.md` and `docs/zh/README.md`. Sub-pages navigate by breadcrumb and
  carry no switcher of their own.
- A paired artifact in the older sibling shape (`foo.zh.md`, `foo.i18n.yaml`) belongs only to the
  trees still being migrated; do not add one under `docs/en/` or `docs/zh/`.
- Chinese punctuation joins Chinese text: `，。、；：？！`, Chinese quotes and full-width
  parentheses; ASCII punctuation stays inside code spans, paths, identifiers and any English name or
  phrase (`Casualties Unknown: Online`). The gate refuses the plain defect — an ASCII connective
  between two Chinese characters; judging the rest stays a review duty.

## Links

- Use relative links, and make sure they resolve: a gate walks every markdown link target in the two
  blocks.
- The head breadcrumb sits in the first lines and both breadcrumbs name the real path, for example
  `Documentation > How-to > Send a network message`, and link to the block's own overview
  (`docs/en/README.md`) and the section index.
- End a content page with 3–6 `Related reading` links: what the reader most likely needs next; an
  index page needs none.
- The first use of a project word in a page links to [`../reference/glossary.md`](../reference/glossary.md).

## Truth

- Commands, outputs and code excerpts come from a run, never from memory.
- A claim about our own code cites the path plus the quoted text, never a line number — a line number
  drifts with every edit above it. The decompiled tree `reversing/` may carry line numbers because
  that tree is never edited.
- Behaviour only a real two-client session can confirm is marked as awaiting the user's acceptance
  run. Green tests are not that evidence.

## Adding a page

1. Write the English page and the Chinese page at the mirrored paths in the same change.
2. Link them from the section index in both blocks, and from `docs/README.md` if the section itself is
   new.
3. Register any new project word in `docs/standard/terminology.txt`, and add it to both glossaries.
4. Record the pair in [`../../standard/alignment.txt`](../../standard/alignment.txt): both paths and
   both git blob hashes, which `git hash-object <file>` prints. The record is a content comparison —
   re-record both hashes whenever either side is edited.
5. Run the documentation gate (see below) before committing.

## What the gates check, and what they cannot

`DocumentationTreeGateTests` checks path parity between the two blocks, that every relative link
points at a file that exists, the page shape above, and that every pair is recorded at its current
contents. `AgentInstructionBudgetGateTests` keeps every nested `AGENTS.md` under 5 120 bytes and the
whole instruction chain — root, `docs/AGENTS.md`, the block files and the machine-local file — inside
the shared 65 536-byte loader budget.

A gate proves presence and shape. It cannot tell whether a page teaches, whether a rendering is
natural, or whether a claim is true; that stays a review duty, checked against the terminology list.

## Decay control

- Editing one side of a page obliges the other side in the same change; the alignment record reports
  the drift when you forget.
- Keep distilled conclusions — decisions, rule-to-gate maps, checklists, self-checks. Delete a process
  log once its conclusions are absorbed and its inbound links re-pointed: delete, do not archive, git
  keeps the history and an archive invites stale reading.

## How you know it worked

- `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --filter "FullyQualifiedName~DocumentationTree"`
  is green, and so is the instruction-budget gate.
- Both sides of the pair exist at the mirrored path, both are listed in the section index, and both
  hashes in `alignment.txt` match the files on disk.
- Reading the Chinese page tells a Chinese reader the same facts without following the English
  sentences.

## Related reading

- [Repository map and pitfalls](repository-map-and-pitfalls.md) — where files and documents live
- [Gates and binding rules](gates-and-rules.md) — the gates that enforce this standard
- [Build, test and deploy](build-and-test.md) — running the gate project alone
- [Glossary](../reference/glossary.md) — the words a page may use, in everyday language
- [Terminology registry](../../standard/terminology.txt) — the exact Chinese rendering of each word

---

[Documentation](../README.md) > [Contributing](README.md) > Writing documentation
