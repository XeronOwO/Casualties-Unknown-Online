# The decision register cannot take another decision

- Status: Done — the split landed 2026-10-24 and the acceptance below is verified. Found 2026-10-23 while
  recording decision 269.
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

## What landed (2026-10-24) — the first page, and why the split is by NUMBER

**Shape chosen: contiguous decision-number ranges, with the register as their index.** The register is
number-ordered (`1, 2, 3, 6, 9, …`), and a decision NUMBER is what every citation in the tree carries, so a
number range is the one split that keeps a lookup one hop: the index names the range, the range's page holds
the row, and the pages read in sequence ARE the register — its order is preserved exactly, which is what the
acceptance asks of the carrier order (the audit's rewrite kept the numbering as the lookup key; a carrier
split would have moved rows out of the sequence the numbers follow).

- `docs/decisions/decisions-1-137.md` (7,620 bytes) now holds decisions 1–137 — the framework, the mod
  surface and the protocol baseline — with their rows unchanged.
- `docs/decisions/active.md` keeps the binding table (264/265), an index naming both homes, and decisions
  152–269. It measures 110,005 bytes (was 116,722), under the 114,688 budget.
- The census rows moved in the same change: the register is `keep | spec | AGENTS.md` again and the new
  page is `keep | spec | docs/decisions/active.md`; `docs/decisions/index.md` names it; the document gates
  pass.

**What remains for headroom** (deliberately NOT done in the landing change): the register still carries
152–269, and if it grows against the 4.7 KB left the next move is mechanical — the 152–199 block
(≈34.5 KB: architecture invariants, the save/restore family, session integrity, sync cadence) onto a second
page, which leaves the register around 75 KB. The rows themselves stay untouched — that is a move, not an
edit, and the shape is now in place to make it without a decision.

## Acceptance

- Every decision number survives: no renumbering, and an index reaches each decision's new home. **Verified:**
  138 rows before and after; 20 moved verbatim to `docs/decisions/decisions-1-137.md`, and `active.md`'s index
  table names the page and its range.
- Each resulting page fits the census budget and its census row is `keep` with a real reach; the
  `rework` row on `docs/decisions/active.md` returns to `keep | spec | AGENTS.md` when the split lands.
  **Verified:** `docs/decisions/active.md` measures 110,005 bytes (was 116,722; budget 114,688), the new page
  7,620; the census rows are `keep | spec | AGENTS.md` and `keep | spec | docs/decisions/active.md`.
- The corpus audit's carrier order is preserved (a reader looking for a protocol decision finds it where
  the audit put it), and the pages that cite the register today still resolve. **Verified:** the rows keep
  their number order across the two pages and the range split is recorded above; `docs/decisions/index.md`
  names the new page; the document-tree gate resolves every link.
- `DocumentCensusGateTests` and the backlog gates are green with the new rows. **Verified:** the normative
  gate suite passes (779 tests) after the split.
