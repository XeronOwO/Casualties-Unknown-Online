# Live tickets cite the deleted `docs/api/` pages

- Status: Todo — found by the reviews of `done/gamerule-host-rule-command.md` (2026-10-23/24);
  pre-existing.
- Priority: Low
- Category: Documentation / reference integrity
- Source: the first review of the `/gamerule` change found that
  `review/mod-native-binding-handshake-parity.md` cites `docs/api/mod-api.md`, a path the documentation
  reorganisation deleted. The second review corrected the inventory: a sweep of the live folders finds
  NINE references in FOUR tickets, and the deleted tree also carries the old modification-policy page.
- Related: `review/mod-native-binding-handshake-parity.md`, `review/recipe-unlock-fallback.md`,
  `review/pinyin-search-standalone-mod.md`, `review/bilingual-human-docs.md`, the pages that replaced the
  old tree (`docs/en/reference/mod-api.md`, `docs/en/reference/modification-policy.md` and their Chinese
  pairs), and `docs/AGENTS.md` (the document standard and the census).

## Problem

The live backlog still points at the deleted tree, and at section numbers that no longer exist:

- `review/mod-native-binding-handshake-parity.md` — three references to `docs/api/mod-api.md` §5 (the
  handshake-matrix paragraph, the honest-boundary bullet, and the "handshake matrix gains the declaration
  column" bullet);
- `review/recipe-unlock-fallback.md` — two references to `docs/api/mod-api.md` §7;
- `review/pinyin-search-standalone-mod.md` — three references: `docs/api/mod-api.md` §4l, and two to
  `docs/api/advanced-modification-policy.md` (§6 and §1.1);
- `review/bilingual-human-docs.md` — one `docs/api/**` in its reference-layer plan list.

The pages now live at `docs/en/reference/mod-api.md` and `docs/en/reference/modification-policy.md` (and
their Chinese pairs), and the mod-api page's sections have titles ("The manifest", "Host commands", …)
rather than numbers; the migration is recorded in
`docs/evidence/selfchecks/documentation/legacy-tree-migration-selfcheck.md`. A record under `done/` or
`evidence/` is a point-in-time snapshot and is deliberately left alone — only the live folders are in
scope.

## Acceptance

- The sweep covers every live folder (`todo/`, `review/`, `in-progress/`, `future/`, `watchlist/`) and the
  ticket records which records were excluded as point-in-time.
- Every `docs/` reference in a live backlog ticket resolves to a file.
- Every section reference in a live ticket names a heading the target page carries today (a path fixed but
  a `§5` left behind is the same rot one edit later).
- Decide whether a gate should check doc paths in live tickets (the backlog reference gate checks ticket
  links only) or whether this stays a periodic audit, and record the answer here.
