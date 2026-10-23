# The delivery-checklist gate reads no census, so an emptied checklist passes it

- Status: Done — accepted 2026-10-24 (agent-run; see *Accepted* below).
- Priority: Low
- Category: Repository gates (process-record integrity)
- Source: the independent NARROWED adversarial review of the entry-mapping record cycle (2026-10-08),
  finding n4 — a gate-robustness nit, filed rather than fixed because it is a `tests/` change.
- Related: `docs/evidence/delivery-checklist.md`, `docs/en/contributing/gates-and-rules.md`
  ("Writing a new gate": keep a census floor), `docs/evidence/normative-gates.md`

## The gap

The checklist's line-walking gate — today
`DeliveryChecklistGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`, and `RepositoryGateTests.…`
when this was filed, before the cycle that closed it gave the family its own file — fails on
every `- [ ]` box that is neither the deployment line nor the FORBIDDEN line, and fails a checked
FORBIDDEN line. It never asserts how many boxes the file declares, so a checklist that lost its boxes —
a bad edit, a "clean-up" that dropped the cycle's required steps — passes the gate by having nothing
left to fail on. The gate is what makes the checklist binding, so its own scan surface should fail when
the checklist stops being a checklist.

## What a fix has to decide

- **What the floor counts.** The boxes DECLARED (`- [ ]` plus `- [x]` lines), never the checked ones: the
  boxes are legitimately unchecked while a cycle runs, and the gate has to keep failing for exactly that
  state rather than passing an empty file.
- **Whether a count is enough.** A floor over the box count catches deletion but not reordering or
  replacement; a stronger check enumerates the required items by a stable token and reports the missing
  one by name. The stronger shape needs the item names to be stable across cycles (the current file's
  items are rewritten by every cycle's evidence suffix, so the check must key on something that is not
  the suffix).
- **The gate's own contract cases.** The matcher needs its positive and negative samples (a checklist
  whose boxes were deleted fails; the standing file with a cycle's boxes still unchecked passes; a
  checked FORBIDDEN still fails), and the fix must not turn the legitimate mid-cycle red into a second
  failure mode.
- **Where the number lives.** A hard-coded floor drifts the moment a cycle adds a box (the entry-mapping
  box arrived that way); the fix should say whether the floor is the measured count or the required
  item tokens.

## What closed it (2026-10-13)

**The gap, measured before the fix** (at HEAD, on the working tree's CRLF copy of both files). With every
box line deleted from `docs/evidence/delivery-checklist.md` (10,119 → 2,835 bytes, 10 boxes → 0) the
standing gate passed 1/1 — a checklist with nothing left to fail on. The same probe over
`docs/contracts/event-replay-matrix.csv` (33 data rows → 0, header kept) passed
`RepositoryGateTests.EventReplayMatrix_Completeness` 1/1: the identical hole one register over.

**What the fix decided**, against the four questions above:

- **The floor counts the boxes DECLARED** — `- [ ]` and `- [x]` lines alike, the two standing boxes
  included — and it is presence, not state: the mid-cycle file (every box declared, none checked yet)
  satisfies the census while the incomplete-box check refuses the cycle, so the legitimate mid-cycle red
  stays the only red.
- **A count is not enough, so the required steps are enumerated by name.** Every entry of
  `RequiredChecklistItems` must be the LEADING text of a declared box. A cycle rewrites only the line's
  tail (its evidence suffix) and its checkbox, so keying on the leading text survives every cycle's
  rewrite — which is what made a whole-line match impossible.
- **The contract cases are pinned** by three samples that feed synthetic text to the same pure functions
  the facts use: the reader accepts only box lines (prose mentioning `- [ ]` is not a box); a deleted or
  renamed step is reported by name while the standing file reports nothing missing; a checked honey-pot
  fails, while the acceptance reminder may be checked without failing.
- **The floor is the item list, not a measured count.** Adding a box needs no gate edit; dropping or
  renaming a required step fails until this list is edited deliberately — the act the gate exists to
  force. Order is deliberately not enforced.

**What landed:**

- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/DeliveryChecklistGateTests.cs` — the checklist
  family, moved into its own gate file, as the other register gates already are, and given
  `DeliveryChecklist_DeclaresEveryRequiredBox` beside the existing
  `DeliveryChecklist_NoIncompleteRequiredBoxes`. The standing-box exemption now keys on the box's own ITEM
  rather than `Contains` over the whole line, so an evidence suffix that happens to mention FORBIDDEN no
  longer exempts an open box. The move's arithmetic, because the file sizes are the reason for it:
  `RepositoryGateTests.cs` held 415 lines at HEAD, this cycle's first pass pushed it to 609 — past the
  600-line discipline, which no gate reaches (`SourceShapeGateTests` scans `src/`) but the checklist's own
  "touched classes <= 600 lines" box does — and the split left it at 418, with the new file at 220.
- `RepositoryGateTests.EventReplayMatrix_Completeness` — the matrix must carry one row per
  `EntityEventKind` and no row for a kind the enum does not declare, read from the enum the dispatch
  census already parsed (the parse is now shared, and the dead local beside it is gone).
- References re-pointed and the census recorded: root `AGENTS.md`,
  `docs/en|zh/contributing/{gates-and-rules,review-and-delivery}.md` (with
  `docs/standard/alignment.txt` re-hashed), `docs/evidence/{delivery-checklist,verification,normative-gates}.md`,
  the moved ticket's own census row and its inbound link in `docs/backlog/review/document-corpus-audit.md`.
  Two dated acceptance records keep their historical anchor, which is what
  `docs/evidence/ticket-anchor-exceptions.json` is for.

**Evidence after the fix** (focused runs, `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests`):

- the two probes re-run against the fixed gate, both records emptied and both families in one filtered
  pass (`--filter "FullyQualifiedName~DeliveryChecklist|FullyQualifiedName~EventReplayMatrix"`): 6 cases,
  **2 failed / 4 passed** — the two census facts, naming the 10 boxes and the 33 kinds they lost, while
  the incomplete-box check PASSED on the emptied checklist. The census is the half that catches it.
- the real file mid-cycle (recorded while two of the ten boxes were checked, right after the family
  moved): the incomplete-box check reported `the delivery checklist (2 of 10 box(es) checked): 6
  violation(s)` while `DeliveryChecklist_DeclaresEveryRequiredBox` was green — one red, not two.

**The family sweep.** Every gate file that names a `docs/` path was listed (18 files) and each was
checked for a census of the record it reads: an entry floor in `AcceptanceLessonsGateTests`, an index
floor in `SelfcheckManifestGateTests`, a document floor in `DocumentCensusGateTests`, a matrix-row
minimum plus an evidence minimum in `SyncCoverageGateTests`, a live-document floor in
`ProtocolNumberGateTests`, a declared-key floor in `LocalizationCatalogueGateTests`, or a census derived
from the source of truth (`SourceShapeGateTests`, `ApiSurfaceGateTests`, `BacklogIntegrityGateTests`).
Exactly two carried none, both in `RepositoryGateTests.cs`, and both are closed here.
`AgentInstructionBudgetGateTests` is a ceiling over instruction files rather than a record gate, and the
gates that merely cite a ticket in a comment are not record readers at all.

**Limits.**

- Order is not enforced: a cycle may move its boxes; the census checks presence, not sequence.
- The required-item list is hand-kept on purpose, so a future process change edits the gate in the same
  change — that is the point, but it is a coupling to know about.
- The event-replay census reads the enum: a kind declared but never dispatched stays
  `EntityEventDispatch`'s finding, and a kind documented in prose without a row is outside its reach.
- Neither gate judges whether a box's evidence is true, or whether a row's verdict is right; that stays
  the review's question. Nor does either judge the file's PROSE: a checklist reduced to its ten item
  lines, all checked, with every operating rule and evidence suffix deleted, passes both — the census
  reads boxes, not arguments.
- A box marker written without the space after the bracket (`- [ ]Item`) is not read as a box at all, so
  the incomplete-box check no longer flags it (the code this replaced matched the bare `- [ ]` prefix).
  The census still reports the required step it hides, and a `- [x]FORBIDDEN` spelling leaves FORBIDDEN
  reported missing, so the escape is closed — but it is a narrowing, not a widening.

## Accepted (2026-10-24, agent acceptance)

- The ticket's six gate cases re-ran green in this cycle: `dotnet test
  tests/CasualtiesUnknownOnline.NormativeGates.Tests --filter
  "FullyQualifiedName~DeliveryChecklist|FullyQualifiedName~EventReplayMatrix"` → 6/6, 0 failed
  (`.acceptance/20261024-c/gate-census-acceptance.log`). That set is the two census facts, the
  incomplete-box fact and the three contract samples this record names.
- The two emptied-record PROBES were not re-run: they require deleting the boxes from the tracked
  checklist and the rows from the tracked matrix, which an acceptance run may not do. Their behaviour is
  pinned by the contract samples (a checklist whose required item is deleted is reported by name; a
  matrix missing a kind id fails), and the census cases read the REAL files and pass.
- Limits carried into `done/`: the two listed limits above are unchanged, and the emptied-file probe
  remains a one-time measurement recorded in this ticket rather than a repeatable acceptance step.
