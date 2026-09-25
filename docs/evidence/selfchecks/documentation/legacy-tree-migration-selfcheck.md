# Legacy documentation tree — the last migration stage: absorb, re-point, delete

Date: 2026-09-25
Scope: the final stage of the human-documentation migration. The old `docs/` layout —
`api/`, `features/`, `developer/`, `guide/`, `operations/`, `history/`, `phases/`, `i18n/` and
`docs/architecture/phase-decisions.md` — is gone; the two blocks under `docs/en/` and `docs/zh/` are
the only human documentation. No runtime code, no wire change, no behaviour claim.

## What landed

- **Two conclusions absorbed as pages, because they had no home in the blocks.**
  `docs/api/advanced-modification-policy.md` was only *linked* from the blocks, and
  `docs/features/enemies.md` was the page the feature matrices named as the creature narrative — so the
  migration could not have deleted either tree honestly. They became
  `docs/en|zh/reference/modification-policy.md` and `docs/en|zh/internals/enemy-sync.md`, with the
  four-layer "where does a feature belong" rule moved into
  `docs/en|zh/contributing/repository-map-and-pitfalls.md` (the page whose question is exactly which
  project a file belongs to).
- **Every live inbound reference re-pointed**, including the three places the old pages were reachable
  from outside `docs/`: the root `README.md` doc list, the `-Doc` synopsis of both matrix tools, and the
  XML doc comments of nine `src/` files plus six test files.
- **The old trees deleted, not archived**: 62 tracked files — `docs/api/` (2), `docs/features/` (4),
  `docs/developer/` (27: nine topics as `.md` + `.zh.md` + `.i18n.yaml`), `docs/guide/` (18),
  `docs/operations/` (1), `docs/history/` (6), `docs/phases/` (1), `docs/i18n/` (2) and
  `docs/architecture/phase-decisions.md` (1) — plus the two retired gate sources under `tests/`.
- **The loose audit table moved to its family**: `docs/event-replay-matrix.csv` →
  `docs/contracts/event-replay-matrix.csv`, with `RepositoryGateTests` and the test project's copy item
  following it.
- **`docs/standard/terminology.txt` grew by fourteen rows** before the new pages used them (enemy,
  contract, implementation, public surface, stability level, tier, promotion funnel, framework core,
  satellite mod, repository tool, reusable component, diagnostics, attack announcement, runtime spawn),
  and both glossaries gained the matching entries.

## Mechanism inventory

| Mechanism | Change | Evidence |
| --- | --- | --- |
| Content with no home | The advanced-modification policy and the enemy design become page pairs in the blocks instead of being deleted with their trees | `docs/en/reference/modification-policy.md`, `docs/zh/reference/modification-policy.md`, `docs/en/internals/enemy-sync.md`, `docs/zh/internals/enemy-sync.md` |
| Feature-location rule | The four-layer rule + six-question test move to the contributor page whose question they answer | `docs/en/contributing/repository-map-and-pitfalls.md` "Where a new system belongs"; the decision register row 205 cites it |
| Protocol-number census floor | `docs/api/**` disappears from the scan surface, so the six mod-facing reference pages (the mod API contract, the modification policy and the protocol-message table, in both blocks) replace it and the floor moves 11 → 15 | `ProtocolNumberGateTests.LiveDocumentPaths`, `LiveDocumentFloor` |
| Entity narrative | The narrative the CSV is cross-checked against is now the entity tables of the reference page, so the test follows it there | `EntityFeaturesDocConsistencyTests.DocPath` = `feature-matrices.md`; the csproj copy item |
| Sibling-pairing contract | Retired with the trees it governed: the mirrored blocks plus `docs/standard/alignment.txt` are the contract, and the workspace instruction budget was already a second gate's job | `HumanDocsPairing.cs` + `HumanDocsPairingGateTests.cs` deleted; `AgentInstructionBudgetGateTests` (unchanged) already covers the budget |
| Process-record exemption | `docs/evidence/**` records of a past state keep the old paths they were written with, as do the backlog tickets and the completed phase documents; only pointers from LIVE documents were re-pointed | `docs/architecture/evolution/phase-{c,e}-*.md`, `docs/decisions/archive.md`, `docs/evidence/sync-coverage-matrix.md`, `docs/evidence/selfchecks/**`, `docs/backlog/**` |
| Dereferenced inbound | The historical blueprint and the compressed phase register were the targets of five live pointers, all removed or re-pointed before the files went | `docs/development/agent-reference.md`, `docs/evidence/normative-gates.md`, `docs/architecture/README.md`, `docs/decisions/active.md`, `docs/architecture/evolution/session-workflow.md`, `.../templates/phase-session.md` |

## Verification

| Check | Result |
| --- | --- |
| Focused filter (documentation tree, protocol number, backlog, sync coverage, repository gates, instruction budget) before the fix | 46 passed / 3 failed — two of them real: ten `glossary.md` links in the two new enemy pages resolved to nothing |
| The same filter after fixing those links | 48 passed / 1 failed, the single failure being `DeliveryChecklist_NoIncompleteRequiredBoxes` (the checklist is filled after the change lands) |
| Entity-narrative consistency gate (`EntityFeaturesDocConsistencyTests` + `ReplayMatrixDataTests`) | 5 passed / 0 failed — 67 matrix entities each named exactly once with a matching sync verdict. The first run failed with all 67 "missing from the narrative tables", because the page writes `| Entity |` and the test compared the header cell case-sensitively against the CSV's `entity`; the fix is a case-insensitive column lookup, not a lowercase header in the page |
| Normative-gates project (`--filter "FullyQualifiedName!~DeliveryChecklist"`) | 144 passed / 0 failed in 5 s — the previous cycle's 154 minus the 9 deleted pairing cases |
| Normative-gates project, whole (the checklist filled) | 145 passed / 0 failed |
| Full suite with build (`dotnet test CasualtiesUnknownOnline.slnx`) | 145 + 3803 passed / 0 failed; gates 8 s, main suite 59 s |
| `dotnet build CasualtiesUnknownOnline.slnx` | exit 0 — "已成功生成。0 个警告 0 个错误" |
| `dotnet format CasualtiesUnknownOnline.slnx` | exit 0 |
| `docs/standard/alignment.txt` | 39 rows (37 + the two new pairs), every blob hash re-recorded with `git hash-object` |
| Negative control 1 — a link to the deleted `docs/features/items.md` put back into a live page | `DocumentationTreeGateTests.EveryRelativeLink_PointsAtAFileThatExists` red with `docs/en/reference/feature-matrices.md → ../features/items.md`, `BacklogIntegrityGateTests.EveryRelativeDocumentLink_Resolves` red with the same path, `...EveryPair_IsRecordedAtItsCurrentContents` red with `recorded 730937c5… but the file hashes to 4624d993…`; reverted → all three green again, and the page hashes to `730937c5…`, the value its alignment row holds |
| Negative control 2 — the census floor raised by one | red with `the scan read 15 live documents (floor 16)`, which pins the measured census at exactly 15 |
| Negative control 3 — a version claim injected into the policy page, the surface the deletion had dropped | `ProtocolNumberGateTests.LiveGovernanceDocuments_DoNotRestateTheProtocolVersionNumber` red with `docs/en/reference/modification-policy.md: claims the version is 99` — the page that carries the "Wire and save compatibility" section is scanned again, so the gate did not quietly lose the document it used to cover through `docs/api/**` |
| Negative control 4 — the same claim injected into `docs/en/reference/protocol-messages.md` | red with `docs/en/reference/protocol-messages.md: claims the version is 99`; reverted, the page hashes to `7dadfad8…`, byte-identical to its blob at HEAD |
| Live-path census after the change | no live document, test, tool or source file names a deleted path — checked by full path and by bare file name (`advanced-modification-policy.md`). The remaining hits are process records: `docs/evidence/selfchecks/**`, `docs/evidence/sync-coverage-matrix.md`, `docs/decisions/archive.md`, the completed `docs/architecture/evolution/phase-{c,e}-*.md`, 19 backlog tickets that keep the citations they were written with, and this sheet |

## Independent review round

A separate review session (fresh context, frozen tree, read-only, report at
`%TEMP%\cuo-review-legacy-tree-migration.md`) returned **0 blocker / 3 major / 8 minor / 3 nits**. All
were fixed in this commit:

- **major — the re-point moved paths but kept the deleted documents' section numbers.** Fifteen
  citations of the two re-pointed pages still said `§1.1`, `§5`, `§4l` … which exist on no page of the
  blocks, and one decision row still named the deleted page by bare file name. Every citation now names
  the destination section, and the four-layer rule's row points at the page that actually carries it.
- **major — the protocol gate's new surface was not the replacement it claimed.** `docs/api/**` covered
  two documents; the new list covered the mod API contract and the protocol table but dropped the
  policy page — the one carrying the "Wire and save compatibility" section. Both language halves of it
  are now in `LiveDocumentPaths` (13 → 15) and the negative control injects a claim into that page to
  prove the reach; the rule-to-gate row now names the exact scanned list instead of implying the whole
  `docs/architecture/` tree.
- **major — one negative-control result in this sheet named a hash the tree does not produce** (it
  quoted `protocol-messages.md`'s blob as if it were the reverted page's). Corrected to the value the
  alignment record holds.
- **minor** — the deleted-file census (62, not 53), the live front door of the selfcheck folder pointing
  at three deleted pages, the surviving-hit list omitting the backlog, a test's dictionary rationale
  still naming the old title, a broken in-page anchor in the Chinese feature-matrices page, two retired
  checks with no successor (now stated in Limits), content of `docs/features/game-internals.md` that had
  no home (the clone-proxy component split, the input collection point, the world-defining fields — now
  on the internals page in both blocks), and the architecture README over-claiming what the archive
  register carries.
- **nit** — the `README.md`-per-directory rule scoped to the human directories, the reference summary
  in both block overviews naming the new page, and the policy page naming the gate that enforces it.

## Limits

- **The entity path assertion was narrowed, deliberately.** The old narrative file carried the CSV's
  `path` cell verbatim, and the test compared it character by character. The page states the same
  mechanism in its own words, so that assertion cannot survive the move without making the page a second
  copy of the machine table. What is still enforced: every entity-carrying table must carry `sync` and
  `path`; every matrix entity must appear exactly once; and the narrative's sync verdict must equal the
  CSV's. The `path` cell is checked for being filled only.
- **Old paths remain in process records on purpose** — `docs/evidence/selfchecks/**`,
  `docs/evidence/sync-coverage-matrix.md`, `docs/decisions/archive.md`, the backlog tickets and the
  completed phase documents under `docs/architecture/evolution/`. They record what a document said when
  the record was written; rewriting them would falsify the record. What they name no longer exists, and
  the gates that walk links do not scan those trees. The one exception is a deferred `future/` ticket's
  entry line, which is the pointer the next implementer follows, and that one was re-pointed.
- **Two checks the retired pairing gate carried have no successor.** The gate compared the two sides'
  language switchers and mirrored their block sequence; the switcher rule is now review-guarded prose
  (`docs/AGENTS.md` §3, `docs/en|zh/contributing/documentation-standard.md`), and the block sequence is
  no longer compared at all — the two blocks are checked for path parity, page shape and recorded
  hashes, not for structural mirroring. A stray `.zh.md` or `.i18n.yaml` would also be caught by review
  only. That is the deliberate cost of retiring a gate whose scope roots were the trees this cycle
  deleted.
- **The `-Doc` advisory on both matrix tools has no in-tree document with `### <feature>` sections** any
  more. The flag still works on any document that has them, and the synopsis no longer names a file that
  cannot satisfy it.
- **A gate proves shape, never teaching.** Link targets, page parity, page shape, the alignment hashes,
  the census floors and the punctuation rule are machine-checked; whether a page reads well, whether the
  Chinese is natural and whether a claim is true stay review duties.
- No runtime code and no wire change: this cycle says nothing about host or guest behaviour, and no such
  claim is made.
