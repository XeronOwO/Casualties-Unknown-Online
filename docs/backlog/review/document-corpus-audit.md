# The document corpus audit: every page earns its place

- Status: Review
- Priority: Critical
- Category: Documentation / process
- Source: the user's 2026-10-12 directive, in their own words: "压根不会去读的文档,你写它有什么意义?…必须有意义,
  确定自己未来会读,或者用索引指向,否则不要落地…然后加一条最高优先级 todo,梳理本项目所有文档,找出类似于 lessons
  的文档,要么直接干掉,要么完全推翻重新整理" — and the rule it produced (`~/.dsh/AGENTS.md`, the landing
  threshold; `docs/AGENTS.md` §6; the review prompt's check G).
- Related: `docs/AGENTS.md`, `docs/en/contributing/documentation-standard.md` (+ its Chinese pair),
  `docs/development/review-prompt.md`, `done/delivery-checklist-gate-census.md` (the same rot in a gate)

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

## The decision register's row audit (2026-10-13)

The register was audited row by row before it is rewritten: each of its **131 rows** was read in full and
judged against the page its own cell names, and that page was opened too — a row's claim that "the detail
lives in X" is not evidence that X carries it. Measured then: 268,673 bytes as the working tree held it
(268,445 as git stores it), median row 2,160 bytes, largest
6,068.

- **12 rows are already bounded** and their pages carry them.
- **70 rows shrink** to the ruling plus a pointer, losing nothing: the page they name already states every
  normative fact they state.
- **49 rows must move first.** Most carry their facts in a record the row's own cell already names (a
  `done/` or `review/` ticket, or a self-check), so the fix is a second pointer rather than new prose; a
  minority have no page at all.

With the sizes came defects the rewrite has to fix, not shrink around:

- **Pages stating a superseded rule**: the protocol-bump policy in
  `docs/architecture/remote-inventory-native-parity.md` §3.5 and the rows around it (decision 241 froze the
  pre-release number); the pinyin self-check's implementation; the capability catalogue pinned at 14
  capability ids where the source declares 13; the carry-rider limb-anchor
  self-check's Debug-only line (decision 233 made it default-visible); decision 244's retired decode half;
  decision 247's deleted `ToPayload` surface; decision 249's native-value clause (superseded by 250);
  decision 251's "next stage" clause (superseded by 252).
- **Rows wrong as written**: decision 241's closing clause says a mismatched save manifest is refused, while
  `docs/architecture/save-archive-format.md` §6.2 and `SaveArchiveReader.NoteProtocolMismatch` open it in
  repair mode with a warning and a damage entry; decision 227's `Ragdoll()`/`standing` clause is garbled
  against its page; decision 229's file count matches no measurement.
- **A claim the artifact does not support**: decision 253 states that
  `docs/contracts/abstractions-api-baseline.txt` records two `RollCondition` method removals; the file
  carries neither the lines nor a tombstone for them.
- **Cells naming no carrier**: 13 rows carry no canonical-doc cell at all, and others name a directory, a
  test method, or a record that states the pre-fix state (decision 235 names the rejected acceptance run,
  not the fix that followed it).
- **Rules with no live page anywhere**: decisions 63 (the fail-closed NetMsg direction registry), 75 (the
  adapter's narrow control interfaces) and 181 (the world-lease rules beyond the one-writer rule) live only
  in an evidence self-check and in source doc comments.
- **A coupling**: decision 237's carrier is `docs/acceptance/lessons.md`, so distilling that log re-points
  this row in the same change.

The rewrite is ordered by CARRIER rather than by decision number, so that one page is opened and changed
once: `docs/en/reference/mod-api.md` and its pair (about 18 rows), `docs/architecture/save-archive-format.md`
and its cells (about 18), the architecture pages (about 20), the instruction files (7, whose moves must not
land in `AGENTS.md`), the ticket carriers (about 45) and the self-check carriers (the rest).

**What landed (2026-10-13).** The register is rewritten: **268,673 bytes as the working tree held it
(268,445 as git stores it) → 102,335**, 228 lines → 161 (the blank runs that had broken its tables are
gone), all **131** numbers kept in place, and every row is now a ruling plus at most two pointers. The
facts that existed only inside a row landed on the page that owns the topic first — the star topology with
its reliability criterion and the fail-closed `NetMsg` direction registry
(`docs/architecture/protocol.md`), the adapter's narrow control interfaces, the partial-aware line budget
with its debt ledger and the ruling that a cue naming its target must agree with the rule that resolves
the gesture (`docs/development/agent-reference.md`), the handler-context narrowing
(`docs/architecture/current.md`), the kernel identity floor (`docs/architecture/domains.md`), the
world-lease answers beyond the refusal (`docs/architecture/save-archive-format.md` §5), the
`Stable`-by-intent rule for a contract that replaces a stable shape
(`docs/en/reference/modification-policy.md` and its pair), the host's own half of an attack with the
announcement's own field list (both sides of `docs/en/internals/enemy-sync.md`), and decision 257's
shrapnel-seam detail (both sides of `docs/en/internals/judgment-ownership.md`); every other moved fact
already sat in a record the row's own cell names, so its pointer now names that record. The stale
protocol-bump text the audit flagged in `docs/architecture/remote-inventory-native-parity.md` §3.5 was
rewritten to point at decision 241. Seven evidence records the bounded rows stopped citing were re-pointed
at the document that actually names them, and the pair registry was re-recorded for the three pairs this
change touched.

The 40 KB aspiration above was **not** reached and is withdrawn: the long rows turned out to be dense
rules rather than narrative, and the next cut would move normative clauses onto pages that do not carry
them yet. What stands is measured — 131 entries, the largest 1,615 characters, the file at 89% of the
census budget, and one definition of "bounded" still to be made enforceable by the ledger-shape gate in
item 3.

## The lessons ledger's entry audit (2026-10-13)

`docs/acceptance/lessons.md` was audited entry by entry before it was rewritten, the way the register was.
Measured before: **3,211 lines, 220 entries, 258,374 bytes as the working copy held it** (255,163 as git
stores it; the 222 in this ticket's opening paragraph counted the two structural headings — "How to add an
entry" and the template inside its fenced block). Measured after: **117 entries, 59,861 bytes as the
working copy holds it**, in the original chronology (the page's own contract is "newest last"), the largest
entry line 940 characters.

- The judgement rule is the ledger's own: an entry whose conclusion has a carrier — a page, a tool, a
  recipe, a committed test, a self-check or a ticket — is **deleted**, because that carrier is where the
  next reader already looks; a pitfall of running a batch that has no other home is **kept** as one line
  naming what its reader can open. **117 entries survive — 85 of them under their own headline, 32 under a
  headline rewritten or merged from two or three old entries that had recorded one pitfall from different
  batches — and the rest were deleted on that rule.**
- Deleted in bulk: the product-mechanism discoveries whose fix, test or self-check now states them; the
  scenario recipes' own setup rules (the recipe header carries them); the machine gotchas that live in
  `AGENTS.local.md`; the judgment rules already in `workflow.md` §6/§7; and the batch-specific
  observations the batch's own record holds. Kept and bounded: the preflight's code-page contract, the
  evaluator's frame loop and its REPL's refusals, the park and blackout windows against the 15-second
  watchdog, the release-time staging rule, the key channel's verification step, and the reading rules for
  a zero-warning row, a divergence warning, a capped extract and an observer's own state at capture.
- Nine census rows moved: the evidence records reached only through this page now name a document that
  actually cites them — four acceptance records under `docs/evidence/acceptance/` and three `done/`
  tickets. Decision 237's carrier left the page for `docs/acceptance/dependencies.md`'s `input` row, and
  one `done/` ticket's `Related:` line now names the record carrying the lesson it cited.
- The shape is enforceable now: `AcceptanceLessonsGateTests` holds the page to a dated headline plus ONE
  line (ceiling 1,200 characters), requires that line to name a backticked carrier, refuses the
  Symptom/Cause narrative, keeps an entry floor and holds the file under a 64 KiB budget — the ledger
  shape item 3 asks for, defined and pinned by a failing sample rather than described.

Not reached: the 40 KB aspiration withdrawn for the register is withdrawn here too. The kept rules are
dense, and the next cut would move normative clauses onto pages that do not carry them yet. Measured now:
the page is 59,861 bytes — 52% of the census's 112 KiB ceiling, 91% of the gate's own 64 KiB budget, and
the census carries **967 keep, 2 rework, 0 delete**.

The adversarial review of this rewrite left two residuals worth carrying. **Five deleted rules now live
only in a point-in-time batch record**, one hop further from a next batch than a page: the
triage-delegation contract, the "a current value that arrives as an absolute base is judged by a live
comparison at both ends" rule, the crop-coordinate rule, the `[BlockSync] answered` attribution rule and
the asserted-held-count reading. They were kept from the deleted state because their record is what the
run cites, but a page that owns those topics should absorb them the next time it is edited. And the
**first assembly of this ledger was written through a code-page round trip** — Windows PowerShell 5.1
reading UTF-8 files without a BOM through the system code page — which left the installed page full of
mojibake and split its header; the review caught it, the page was rebuilt from its clean sources and the
general rule is the ledger's own 2026-09-27 entry, which the gate's own header now repeats.

## The coverage matrix's row audit (2026-10-13)

`docs/evidence/sync-coverage-matrix.md` was audited row by row before it was rewritten, the way the register
and the ledger were. Measured before: **244,427 bytes as the working tree held it** (244,792 as the ticket
opened it), 66 matrix rows whose average length was 3,100 characters and whose largest was 8,169. Measured
after: **91,745 bytes, the same 66 rows, average 1,150 characters and largest 1,196** — 20% of the census's
112 KiB ceiling and 80% under the budget the census gate enforces.

- The judgement rule is the page's own: a row states the POLICY — the surface and its owner, the direction
  and roles, the event carrier with its reliability, the periodic fallback with its cadence and apply
  semantics, the backfill path, and what a loss costs — plus one pointer to the record that carries the
  closing detail. Everything else was a second home for a fact that already has one: `file:line` citations
  (which `docs/AGENTS.md` §5 forbids for our own code, and which drift), the source paths whose quotes are
  gate-verified in `docs/evidence/sync-coverage-evidence.json`, and the cycle narrative ("landed
  2026-09-19", "the review found …") that made a policy table read as a batch log.
- **Two tables were one table twice.** The nine `Transient-by-design` rows each appeared a second time in an
  `Accepted losses` table, and every gap appeared a third time in a 10 KB `Gap list` whose entries carried
  the whole closing story of a ticket that also states it. Both tables are gone: the reason lives in its own
  row's loss cell, the closing record in its last cell, and the gap list is now a single statement that the
  queue is empty with a pointer to the convention that refills it.
- **Stale claims were fixed rather than compressed**: W2's "protocol 32" (no protocol number is current
  pre-release — decision 241 froze the baseline), I1's "there is no guest-side re-report" (the kernel tracks
  and re-sends every unacknowledged command, which is what made I5 `OK`), R3's "SceneState is reported only
  on two edges" (it re-asserts on a bounded 5 s window), R4's "no recovery while both peers are online" (a
  1 Hz warm-up pump and a 2 s member check exist), N4's self-contradiction about its own fallback cap (15 s,
  per the catalog), three broken citations (`KernelProtocolService.cs/472`, `…/407`, a truncated quote) and
  nine `docs/`-relative pointers that were not repo-relative paths at all.
- **Five rows carry the audit's own scope limits**, recorded so the next reader does not mistake a carried
  statement for a re-verified one: E1 and E2 admit a lost guest report that nothing reconciles while their
  verdict stays `OK` (the same shape that made W2/W6/I5 gaps), and the rows for P8's three timed-medicine
  chains and P11's adaptive base cadence keep the previous text compressed rather than re-measured.
  `docs/backlog/review/sync-cadence-review.md` itself is still `Review` while the matrix's gap list had
  called its gaps closed — the row now states the policy and names the record without claiming a status.
- The shape is enforceable now: `SyncCoverageGateTests.SyncCoverageMatrix_EveryRowIsBounded` fails on a row
  over 1,200 characters, on a source path outside the closing-record cell and on a line number anywhere, and
  `BoundedRowValidation_FlagsAnOverlongRowAPathAndALineNumber` pins the matcher with a synthetic overlong
  row, a path and a line number — including the false positive the first matcher produced, an ordinary word
  (`coordinates`) that carries `tests/` inside it.
- Nine census rows moved with the rewrite. `docs/evidence/sync-coverage-matrix.md` itself left `rework` for
  `keep` with `docs/evidence/normative-gates.md` as its reach (the rule-to-gate map names it and its rows),
  and `docs/architecture/glossary.md` was re-pointed from the matrix — which only ever mentioned "glossary"
  in passing, while the census gate's reach check accepts a bare stem — to its own section index,
  `docs/architecture/README.md`. **Measured now: 968 keep, 1 rework, 0 delete**, the one `rework` being
  `docs/evidence/selfchecks/MANIFEST.md`.

## The self-check register's row audit (2026-10-13)

`docs/evidence/selfchecks/MANIFEST.md` was audited row by row before it was rewritten, the way the
register, the ledger and the matrix were. Measured before: **111,705 bytes**, 329 rows (the 328 records
plus the directory's own `README.md`), the largest note 2,707 characters, **145 rows carrying one
placeholder sentence** ("candidate current evidence; verify before citing") and 32 the same old-wire
sentence. Measured after: **70,650 bytes**, the same 329 rows, the largest note 1,193 characters.

- **The reader is named, not assumed.** `README.md` beside it and `docs/evidence/verification.md`
  §Self-checks both route a reader here *before citing a selfcheck*, and 52 of the 56 records that carry
  a `HISTORICAL` banner name it too. So this page was never a log without a reader; its defect was
  that 44% of its rows answered that reader's actual question — may I cite this? — with a shrug. The
  intro now states the register's contract once: it records what is known to have **superseded** a
  sheet, and `current` means *no supersession of the sheet as a whole is recorded*, NOT that the sheet
  matches today's code — twelve `current` rows still record that part of a sheet moved, and each says
  which part.
- **The judgement rule is the register's own**: a row carries the status and a note bounded to 1,200
  characters — one clause wherever a clause carries it — naming where the sheet's normative facts live.
  The 145 placeholder rows become a dash (the sheet's own text is the whole record of it), the 32
  old-wire rows name the live path for their family, and the 25 rows over the ceiling were cut to a
  clause plus the decision or ticket that carries the detail.
- **A supersession is one fact in two places now, and the gate keeps them in step.** Measured before:
  24 rows said `historical` for a record whose own text did not say it, and the markers were spelled
  three ways (`SUPERSEDED`, `Superseded`, `Status: Rejected`) — which is exactly why no gate could
  check them. Those 24 records carry the uniform `> **HISTORICAL**` banner with their reason now, and
  the measurement is 56 rows ↔ 56 bannered records, with no row disagreeing in either direction.
- **The citation family was swept, and the sweep was itself the trap.** 40 occurrences inside
  `docs/evidence/selfchecks/` — a tree the backlog-reference gate exempts BY NAME as a point-in-time
  record, which is why nothing had ever failed on them — plus 7 files outside it that the same gate's
  `docs/`-only scan surface cannot reach. Both sweeps answer zero now. Two lessons are the useful part:
  a citation may be written as a bare `todo/<x>.md` fragment that no prefixed matcher sees (12 more in
  the register's own notes were found and made full paths only because the first sweep missed two of
  them), and **a scripted re-point must not touch a path inside a sentence that describes the move** —
  doing so produced three sentences that said a ticket "became itself" or that a live path "is gone",
  all three repaired by hand in this change.
- **The shape is enforceable now**: `SelfcheckManifestGateTests` gained
  `EveryRow_CarriesARecordedStatusAndABoundedNote`, `EveryHistoricalRow_AgreesWithTheRecordItNames` and
  `EveryHistoricalRow_NamesWhereToReadInstead`, each pinned by a synthetic sample — an over-long note,
  an unknown status, an empty note, a row and a record that disagree, and a superseded row that names
  nothing or names what is gone. Decision 229's row and `docs/evidence/normative-gates.md` record it.
- The census moved twice. `docs/evidence/selfchecks/MANIFEST.md` left `rework` for `keep` with
  `docs/evidence/selfchecks/README.md` as its reach, the document that routes readers to it. And the
  census gate caught a **false reach** the rewrite exposed:
  `docs/evidence/acceptance/mod-data-sync-model-20260927.md` was reached by a passing mention inside
  one of the narrative notes, so its row now names the `done/` ticket that cites it for real.
  **Measured now: 969 keep, 0 rework, 0 delete.**

Two limits are measured rather than assumed. **145 rows carry a dash**, which is the honest answer: the
2026-08-30 triage never judged those sheets and this cycle did not pretend to either, because a
re-judgement needs each one read against the code and a wrong verdict is worse than a stated blank. And
**215 census rows name this register as their `reach`** — that is the rows whose reach cell is exactly
`docs/evidence/selfchecks/MANIFEST.md`, the register's own row excluded — of which **140** have no other
access path at all, measured by looking for each sheet's file name across the 639 tracked markdown
documents outside `selfchecks/`. Carrying both a supersession ledger and the completeness index is a
deliberate duplication rather than an oversight: completeness is what
`EverySelfcheckFile_HasExactlyOneManifestRow` requires, and a register listing only superseded sheets
would leave those 140 with no way in until the census can express a directory index as a reach.

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
   **Partly landed: two of the four are rewritten and off the rework list.** `docs/decisions/active.md`
   268,673 → 102,335 bytes and `docs/acceptance/lessons.md` 258,374 → 59,656, both audited entry by entry
   first (the sections above). The queue that is left is measured, not guessed:
   `docs/evidence/sync-coverage-matrix.md` 244,792 bytes and `docs/evidence/selfchecks/MANIFEST.md`
   111,705, the two largest documents in the tree that are still logs. The largest ticket body,
   `docs/backlog/review/save-mid-run-consistent-cut.md` at 107,575 bytes, is the first candidate of the
   cycle-narrative class and has not been judged yet. — **Three of the four are rewritten and off the rework
   list: the coverage matrix followed the register and the ledger (see *The coverage matrix's row audit*
   below), which leaves `docs/evidence/selfchecks/MANIFEST.md` as the last log in the queue.** — **All four
   are rewritten and off the rework list**: the self-check register followed the other three (see *The
   self-check register's row audit* above) and the census now carries **969 keep, 0 rework, 0 delete**. What
   the queue still holds is the cycle-narrative class rather than a log: the largest ticket body,
   `docs/backlog/review/save-mid-run-consistent-cut.md` at 107,575 bytes, has not been judged yet.
3. **A ledger shape for what stays a list.** — **Landed for the lessons ledger, open for the register.**
   An entry is bounded, names what its reader can open, and an entry whose conclusion has landed
   elsewhere is deleted in the same change — `AcceptanceLessonsGateTests` now fails on a two-line entry,
   on an entry naming no carrier, on a narrative shape and on a file over its budget, and the register's
   rows were rewritten to the same rule by hand. What the register still lacks is the cap itself: its
   largest row is 1,615 characters, and a ceiling there has to wait until the clauses that make those
   rows long have a carrier page of their own. — **The self-check register's rows are bounded to the same
   1,200 characters now**, gated by `SelfcheckManifestGateTests`; **what stays open is this register's own
   cap** (item 3's first half): its largest row is 1,615 characters.
4. **Gates so it cannot grow back.** — **Partly landed**: the census gate proves completeness in both
   directions, resolvability of every `reach`, the reader vocabulary, the work queue behind `rework` and a
   112 KiB size budget measured into the gap between the four logs (111,705 bytes and up) and the largest
   document that is not one (107,575 bytes); the ledger shape check of item 3 is now executable for
   `docs/acceptance/lessons.md`. Still open: the register's own cap (item 3), and a dangling reference
   sweep over *every* document — today only the human blocks are swept
   (`DocumentationTreeGateTests.EveryRelativeLink_PointsAtAFileThatExists`). — **The self-check register's
   row shape is executable too** (the three facts above), and the dangling-reference sweep found and
   re-pointed **40 stale ticket links inside `docs/evidence/selfchecks/`**; a sweep over *every* document is
   still what item 4 leaves open.
5. **References move with the files**: every citation is re-pointed, the pair registry is re-recorded where
   the two blocks change, and the gate suite is green at the end. — **Partly landed**: the three deletions
   had no inbound citation to move, the three ticket repairs moved evidence back into reach, and the
   twenty-two ellipsis-shortened citations in five documents were expanded to the full path each one means
   (the shorthand is ambiguous wherever two families share a batch suffix, which is how a live record's
   reference to a deleted sibling went unnoticed). **Measured for the rework ahead**: the four logs are the
   `reach` of 36 rows today (22 `decisions/active.md`, 13 `acceptance/lessons.md`, 1
   `sync-coverage-matrix.md`), so distilling a log has to re-point those rows in the same change or the
   census gate fails — which is the gate doing its job. — **The register's rewrite re-pointed 40 more**: every
   backlog path the self-check sheets cite was resolved against the backlog tree, and the census gate
   caught one **false reach** the rewrite exposed (`mod-data-sync-model-20260927.md` was reached by a
   passing mention inside a narrative note, not by a citation).

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
