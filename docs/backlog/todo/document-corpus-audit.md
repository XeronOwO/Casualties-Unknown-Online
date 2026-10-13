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

## What has landed

**The census and its gate (2026-10-12).**

- [`../../standard/document-census.txt`](../../standard/document-census.txt) carries one row per tracked
  markdown document — 968 rows, the count re-derived against `git ls-files` in the same run — as
  `<path> | <disposition> | <reader> | <reach>`. `reader` is the moment the document is opened (a loader
  file, a section index, a citation followed during an audit, the backlog index for attribution), `reach`
  is the document that opens it. It is a machine table the gate reads, so the size budget below is on the
  documents, not on it.
- `DocumentCensusGateTests` holds it with eleven facts: a document with no row, a row naming a document that
  is not there, a kept document the named document never names back, a `rework` row with no ticket behind
  it, and a document over the 112 KiB budget that is not on the rework list all fail; three facts pin the
  matcher with synthetic samples. A new page therefore lands only after the page that sends readers to it.
  Its scan surface is the working tree with the build output, the vendored tree and the gitignored local
  areas skipped — not `git` itself, so a tracked document inside a skipped directory and an untracked
  document in the tree are the two shapes it cannot see.
- Measured state: **964 keep, 4 rework, 0 delete**.
- Nine rows started out as deletion candidates: no citation from outside the evidence tree. What the pass
  actually did with them is the useful record, because two of the nine were nearly lost:
  - **Three were deleted** — `guest-block-mutation-re-report-20261002-k`,
    `item-creation-registration-first-20261002-k` and `source-shape-gate-modifier-blindness-20260927`, each
    an acceptance record of a ticket in `done/` that cites a later record of the same family instead.
  - **Two were restored by the adversarial review**:
    `remote-inventory-native-parity-rework-20261005-c` and `-20261005-e` are the row-by-row evidence for
    rows 2, 4, 5, 10, 11, 12, 14 and 15 of the umbrella ticket, cited by the live record
    `remote-inventory-native-parity-rework-20261011-f.md` as `…-20261005-c.md` — an ellipsis that ate the
    *family prefix*, leaving a batch suffix no name or stem search can resolve.
  - **One was kept** for the same reason in its milder form: `drop-pending-single-slot-overwrite-20261006-c`
    is cited by a self-check as `…-drop-pending-…`, an ellipsis that ate the directory.
  - **Three live tickets carried an unreachable record** because the ticket never folded the verdict back
    onto its face: `save-guest-restore-claim-and-legacy-store-retirement`, `save-restore-account-surface`
    and `save-multiplayer-restore-and-backups` now carry an `- Acceptance (20261001-o …)` line naming the
    record. One of the three records was reachable only through a sibling record, which is why it looked
    unreachable.
- Method, and its limit — this is the cycle's real lesson. `reach` is derived from a citation graph over
  every tracked text file, resolved with markdown's own relative-link rule, and every deletion candidate was
  then searched for by file name and by stem. **Both checks missed an ellipsis that swallowed the family
  prefix**, and only a reader going through the citing record found it. A deletion therefore needs the
  independent search *and* a reading of the records that belong to the same family; the search alone is not
  evidence.

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

1. **A census with one disposition per file.** — **Landed** (966 rows; see *What has landed*).
2. **The append-only logs go first**: the lessons ledger, the decisions log, the coverage matrix, the
   self-check manifest, and any ticket whose body has become a cycle narrative rather than a decision. —
   **Open.** The four logs are on the rework list by measurement: `docs/decisions/active.md` 268,673 bytes,
   `docs/acceptance/lessons.md` 258,374, `docs/evidence/sync-coverage-matrix.md` 244,792,
   `docs/evidence/selfchecks/MANIFEST.md` 111,705. The largest ticket body,
   `docs/backlog/review/save-mid-run-consistent-cut.md` at 107,575 bytes, is the first candidate of the
   cycle-narrative class and has not been judged yet.
3. **A ledger shape for what stays a list.** — **Open.** An entry is bounded, says what its reader does with
   it, and names the page that carries its conclusion; an entry whose conclusion has landed elsewhere is
   deleted in the same change.
4. **Gates so it cannot grow back.** — **Partly landed**: the census gate proves completeness in both
   directions, resolvability of every `reach`, the reader vocabulary, the work queue behind `rework` and a
   112 KiB size budget measured into the gap between the four logs (111,705 bytes and up) and the largest
   document that is not one (107,575 bytes). Still open: the ledger shape check of item 3, and a dangling
   reference sweep over *every* document — today only the human blocks are swept
   (`DocumentationTreeGateTests.EveryRelativeLink_PointsAtAFileThatExists`).
5. **References move with the files**: every citation is re-pointed, the pair registry is re-recorded where
   the two blocks change, and the gate suite is green at the end. — **Partly landed**: the three deletions
   had no inbound citation to move, the three ticket repairs moved evidence back into reach, and the
   twenty-two ellipsis-shortened citations in five documents were expanded to the full path each one means
   (the shorthand is ambiguous wherever two families share a batch suffix, which is how a live record's
   reference to a deleted sibling went unnoticed). **Measured for the rework ahead**: the four logs are the
   `reach` of 36 rows today (22 `decisions/active.md`, 13 `acceptance/lessons.md`, 1
   `sync-coverage-matrix.md`), so distilling a log has to re-point those rows in the same change or the
   census gate fails — which is the gate doing its job.

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
