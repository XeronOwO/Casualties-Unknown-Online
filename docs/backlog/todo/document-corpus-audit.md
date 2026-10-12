# The document corpus audit: every page earns its place

- Status: Todo
- Priority: Critical
- Category: Documentation / process
- Source: the user's 2026-10-12 directive, in their own words: "压根不会去读的文档,你写它有什么意义?…必须有意义,
  确定自己未来会读,或者用索引指向,否则不要落地…然后加一条最高优先级 todo,梳理本项目所有文档,找出类似于 lessons
  的文档,要么直接干掉,要么完全推翻重新整理" — and the rule it produced (`~/.dsh/AGENTS.md`, the landing
  threshold; `docs/AGENTS.md` §6; the review prompt's check G).
- Related: `docs/AGENTS.md`, `docs/en/contributing/documentation-standard.md` (+ its Chinese pair),
  `docs/development/review-prompt.md`, `todo/delivery-checklist-gate-census.md` (the same rot in a gate)

## Why this ticket exists

The corpus grew by appending, and nothing ever asked who would read what was appended. Measured on the tree
the ticket was written against: **959 tracked `.md` files under `docs/`**, with the largest ones log-shaped —
`docs/decisions/active.md` 268 KB, `docs/acceptance/lessons.md` 258 KB (3,211 lines, 222 entries, of which
**13** name the file that carries the rule they teach), `docs/evidence/sync-coverage-matrix.md` 244 KB,
`docs/evidence/selfchecks/MANIFEST.md` 111 KB, and a dozen backlog tickets over 90 KB.

The consequence was measured too: in the cycle that was supposed to follow it, the agent read **730 of
`lessons.md`'s 3,211 lines** (the reader itself truncated), used five of them, and never opened the tail. A
document whose only access path is "read all of it" is not a document; it is a landfill with a title.

## The test every document has to pass

Name, before it lands, **who opens it and when** — the shape that serves each answer:

| Reader | When they open it | The shape that serves them |
|---|---|---|
| the agent, at a decision point | planning a batch, fixing a defect, moving a ticket, judging a row | a rule in a file that is loaded or routed at that moment (`AGENTS.md`, `AGENTS.local.md`, the area's page) |
| a reader, arriving from an index | browsing a topic, learning the system | a page under one of the two blocks, listed in its section index |
| the agent, auditing a claim | while checking a citation | the record itself, cited by path from the claim |

Anything that answers none of the three is not landed: the conclusion goes into the page the reader already
opens, and the file is not created.

## Required work

1. **A census with one disposition per file.** Every tracked document under `docs/` (plus the root `.md`
   files) gets exactly one line: `keep` (and the index that reaches it), `rework` (into which page or ledger),
   or `delete` (with the inbound references that move). No file may end the census as "unclassified".
2. **The append-only logs go first**: the lessons ledger, the decisions log, the coverage matrix, the
   self-check manifest, and any ticket whose body has become a cycle narrative rather than a decision.
3. **A ledger shape for what stays a list.** An entry is bounded, says what its reader does with it, and names
   the page that carries its conclusion; an entry whose conclusion has landed elsewhere is deleted in the same
   change.
4. **Gates so it cannot grow back**: a shape check for the ledgers, a size budget for the log-shaped files, and
   an index-coverage check (every surviving human page is reachable from its section index).
5. **References move with the files**: every citation is re-pointed, the pair registry is re-recorded where the
   two blocks change, and the gate suite is green at the end.

## Acceptance

| # | Row | Judged by |
|---|---|---|
| 1 | A census exists in which every tracked document has exactly one disposition, and the count of documents equals the count of disposition lines | the census file, re-derived against `git ls-files` in the same run |
| 2 | Every surviving human page is reachable from its section index | the new index-coverage gate, plus a negative case: an unindexed page fails it |
| 3 | Every surviving list-shaped file is a ledger: bounded entries, and each entry names where its conclusion lives | the shape gate, plus a negative case: an appended narrative entry fails it |
| 4 | The log-shaped files that were deleted have no dangling citation anywhere in the tree | the reference gates, run after the deletions |
| 5 | For every file this audit keeps, the moment its reader opens it can be written down in one clause | the census's `keep` lines, read in the review that accepts this ticket |

## Non-goals

- Not a rewrite of the two human blocks' content: they are already paired, indexed and gated.
- Not deleting evidence a ticket or an acceptance record cites — that evidence has its reader (the audit), and
  it stays cited by path.
- Not a new documentation framework: the standard already says "delete the log, keep the distilled
  conclusion". This ticket executes it, and adds only the gates that keep it executed.
