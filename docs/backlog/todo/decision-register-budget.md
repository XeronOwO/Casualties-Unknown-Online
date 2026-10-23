# The decision register cannot take another decision

- Status: Todo — found 2026-10-23 while recording decision 269.
- Priority: Low-Medium
- Category: Documentation / process / register shape
- Source: the document census gate refused the register once decision 269 landed: an over-budget document
  may stand only while its census row says `rework`. `docs/decisions/active.md` measured 114,495 bytes at
  `4edf54d1` — 193 bytes under the 114,688-byte budget — so ANY new binding decision overflows it. The
  register's census row now says `rework` and names this ticket.
- Related: `review/document-corpus-audit.md` (the distillation that brought the register from 268,673 bytes
  to 102,335 and set the per-document budget; 114,495 is later growth), decision 269 (the row that found the
  ceiling), `docs/AGENTS.md` §6 (decay control: a document earns its place by having a reader).

## Problem

The decision register is the normative record every agent opens while working in an area, and it has
grown by appending one row per decision ever since the corpus audit distilled it. The audit left it at
102,335 bytes (about 12.3 KB under the budget); later decisions carried it to 114,495, and the next binding
decision — 269, the host-rule console verb — took it over. The census gate's own rule is that an
over-budget document stands only while it is queued for rework. The ceiling is therefore not an accident of
one long row: the register's shape (one table, one prose paragraph per decision, appended) has reached its
limit, and the next decision would break it again even if 269 were shortened to a pointer.

## The question

Three shapes, to be chosen before the split starts:

1. **Split by carrier** — the register's own audit ordered its rewrite by carrier rather than by
   decision number; the same division can carry the split (protocol/wire, session/lifecycle, console and
   operator surfaces, process). Each part is a page with its own reader, and a one-line-per-decision
   index stays as the entry point.
2. **A thin index plus the owning pages** — each decision keeps its number and one-line statement in the
   register, and the normative detail lives on the page that already owns its topic (the shape the audit
   used for the facts that existed only inside a row). This is the smallest register, but it edits the
   authority of decisions whose text currently reads as self-contained.
3. **Raise the budget** — rejected at design: the budget was measured to sit in the gap between the four
   append-only logs the audit queued (111 KB–268 KB) and the largest document that is not a log
   (107,575 bytes); raising it would readmit the logs the audit was created to catch.

The choice is the agent's (document structure), not a player-facing question; the ticket records the
reason the shape it picks is the one the readers can follow.

## Acceptance

- Every decision number survives: no renumbering, and an index reaches each decision's new home.
- Each resulting page fits the census budget and its census row is `keep` with a real reach; the
  `rework` row on `docs/decisions/active.md` returns to `keep | spec | AGENTS.md` when the split lands.
- The corpus audit's carrier order is preserved (a reader looking for a protocol decision finds it where
  the audit put it), and the pages that cite the register today still resolve.
- `DocumentCensusGateTests` and the backlog gates are green with the new rows.
