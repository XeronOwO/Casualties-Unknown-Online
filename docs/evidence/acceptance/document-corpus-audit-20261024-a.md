# Acceptance — the document corpus audit: every row judged on this tree

- Ticket: `docs/backlog/review/document-corpus-audit.md` — verdict: **all five rows pass, the ticket moves to
  `done/`**
- Batch: `.acceptance/` (local, gitignored; the preflight for this interactive run was `pre-20261024-d.log`,
  the gate run `doc-gates.log`)
- Artifact: the repository tree at `61f157ac` (the ticket is documentation-only; no game process was needed and
  no deployment was involved)
- Date: 2026-10-24
- Result: **rows 1–4 are machine-checked in this run, row 5 is the reading this review is written as.**

## Rows

| Row | Evidence |
|---|---|
| 1 — a census exists in which every tracked document has exactly one disposition, and the counts are equal | re-derived against `git ls-files`: **1,135 tracked `.md` files under `docs/`**, **1,135 census disposition lines**, **1,135 distinct paths**; tracked-but-uncensused **0**, censused-but-untracked **0**. The disposition set is uniform: `keep` on every line (the register's `rework` row returned when its split landed). |
| 2 — every surviving human page is reachable from its section index | `DocumentationTreeGateTests` and `DocumentCensusGateTests` green in this run: **23 passed, 0 failed** (`dotnet test —filter DocumentCensusGateTests\|DocumentationTreeGateTests`); the full normative suite had run green on the same tree (779 tests). |
| 3 — every surviving list-shaped file is a ledger | the same gate run carries the ledger-shape checks (documentation-standard §6 + the census's reader/reach columns); no list-shaped file was reported. |
| 4 — the deleted log-shaped files have no dangling citation anywhere in the tree | the three records the audit deleted — `guest-block-mutation-re-report-20261002-k`, `item-creation-registration-first-20261002-k`, `source-shape-gate-modifier-blindness-20260927` — are each found in exactly **one** file: the audit ticket itself, where their deletion is recorded. `git grep` over `docs/`, `src/`, `tests/` and `tools/` finds no other reference, so nothing live cites a deleted page. |
| 5 — for every kept file the reader's moment can be written in one clause | read across the census: **all 1,135 `keep` lines carry a reader kind and a reach page, none lacks a reader**; the kinds are five (`attribution`, `claim`, `index`, `loaded`, `spec`) and the reach names the page that sends that reader. The reader moment of any line is therefore the (kind, reach) pair — "a reader arriving from `<reach>` for a `<kind>`" — and the one reach value that is `-` is the auto-loaded instruction contract (`docs/acceptance/AGENTS.md`), which needs no index by design. |

## Limits and substitutions

- Row 5's judgement reads the census's ENCODING, not a per-file sentence: the audit chose a kind + a reach page
  rather than one prose clause per document, and the accepting review's own reading is that the pair answers the
  row's question for every line. A reader who expects prose per file would find the encoding thinner than the
  row's wording; the ticket's "What landed" records that choice.
- Rows 2 and 3 are judged through the gates that were built for them, run in this batch; their negative cases
  (an unindexed page, an appended narrative entry) are pinned by those tests, not re-staged by hand.
- No game client, deployment or sandbox was used: this ticket's surface is the document tree, which is why the
  batch is a document-tree batch rather than a two-client run.
