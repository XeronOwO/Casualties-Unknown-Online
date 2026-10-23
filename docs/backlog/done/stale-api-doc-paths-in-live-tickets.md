# Live tickets cite the deleted `docs/api/` pages

- Status: Done — swept 2026-10-24; the live ticket set now resolves, with the two exclusions recorded
  under *The sweep* below.
- Priority: Low
- Category: Documentation / reference integrity
- Source: the first review of the `/gamerule` change found that
  `done/mod-native-binding-handshake-parity.md` (then in `review/`) cites `docs/api/mod-api.md`, a path the documentation
  reorganisation deleted. The second review corrected the inventory: a sweep of the live folders finds
  NINE references in FOUR tickets, and the deleted tree also carries the old modification-policy page.
- Related: `done/mod-native-binding-handshake-parity.md`, `review/recipe-unlock-fallback.md`,
  `review/pinyin-search-standalone-mod.md`, `resolved/bilingual-human-docs.md`, the pages that replaced the
  old tree (`docs/en/reference/mod-api.md`, `docs/en/reference/modification-policy.md` and their Chinese
  pairs), and `docs/AGENTS.md` (the document standard and the census).

## Problem

The live backlog still points at the deleted tree, and at section numbers that no longer exist:

- `done/mod-native-binding-handshake-parity.md` (then in `review/`) — three references to `docs/api/mod-api.md` §5 (the
  handshake-matrix paragraph, the honest-boundary bullet, and the "handshake matrix gains the declaration
  column" bullet);
- `review/recipe-unlock-fallback.md` — two references to `docs/api/mod-api.md` §7;
- `review/pinyin-search-standalone-mod.md` — three references: `docs/api/mod-api.md` §4l, and two to
  `docs/api/advanced-modification-policy.md` (§6 and §1.1);
- `resolved/bilingual-human-docs.md` — one `docs/api/**` in its reference-layer plan list.

The pages now live at `docs/en/reference/mod-api.md` and `docs/en/reference/modification-policy.md` (and
their Chinese pairs), and the mod-api page's sections have titles ("The manifest", "Host commands", …)
rather than numbers; the migration is recorded in
`docs/evidence/selfchecks/documentation/legacy-tree-migration-selfcheck.md`. A record under `done/` or
`evidence/` is a point-in-time snapshot and is deliberately left alone — only the live folders are in
scope.

## The sweep (2026-10-24)

- **Scope.** Every live folder — `todo/`, `review/`, `in-progress/`, `future/`, `watchlist/` — scanned for
  `docs/<path>.<ext>` references and checked against the tree; `done/`, `resolved/` and `evidence/` are
  point-in-time records and were excluded by design.
- **Fixed (the nine).** `done/mod-native-binding-handshake-parity.md` (then in `review/`) — three references now name
  `docs/en/reference/mod-api.md` and its "Handshake consistency" section;
  `review/recipe-unlock-fallback.md` — two now name the "Versioning discipline" section;
  `review/pinyin-search-standalone-mod.md` — three now name `docs/en/reference/mod-api.md`'s "Resource
  completion" and "The manifest" sections and `docs/en/reference/modification-policy.md`'s "The tiers"
  and "The promotion funnel" sections. No `§` number is left behind: every rewritten reference names the
  heading the page carries today.
- **Excluded, recorded.** (1) `resolved/bilingual-human-docs.md`'s two design tables list the PRE-MIGRATION
  tree this plan started from; they are labelled as the design record (and its reference-layer line now
  names the landed `docs/en|zh/reference/**` pair). Rewriting the tables would falsify the plan, so a
  path scan will still see those 22 names — by design. (2) This ticket quotes `docs/api/...` as the
  SUBJECT of the defect; a ticket about deleted paths must name them.
- **The section-reference rule** was applied to the references this ticket covers (the replacement
  reference pages): each now names a heading, because a path fixed but a `§5` left behind is the same rot
  one edit later. The rest of the live set's section references point at pages and registers whose
  numbering is their own (decisions, matrices) and were not in the reported defect.
- **Gate or audit? Decision: periodic audit, no new gate.** The two exclusions above are legitimate
  live-ticket content, so a broad "every docs path in a live ticket must resolve" gate would either fail
  on the plan record or force it to be rewritten; the existing backlog reference gate keeps ticket links,
  and the document census keeps the landed pages. The audit is re-run with this one check: extract
  `docs/<path>.<ext>` from every live ticket and require each to resolve, allowing exactly the two
  recorded exclusions.

## Acceptance

- The sweep covers every live folder (`todo/`, `review/`, `in-progress/`, `future/`, `watchlist/`) and the
  ticket records which records were excluded as point-in-time.
- Every `docs/` reference in a live backlog ticket resolves to a file.
- Every section reference in a live ticket names a heading the target page carries today (a path fixed but
  a `§5` left behind is the same rot one edit later).
- Decide whether a gate should check doc paths in live tickets (the backlog reference gate checks ticket
  links only) or whether this stays a periodic audit, and record the answer here.
