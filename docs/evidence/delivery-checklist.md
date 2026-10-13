# Delivery Checklist

Every development cycle runs through this checklist. The gate
(`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`) runs before
the cycle's final commit as part of `dotnet test` and refuses it while any box
is unchecked. Deployment and multiplayer acceptance are the agent's actions,
run after the commit per `docs/acceptance/` and outside this gate; feature
development verification uses simulation/static evidence. When a release cycle
lands, reset the checklist by manually unchecking every box so the next cycle
starts clean.

**Operating rule (user mandates 2026-08-10 / 2026-08-16)**: boxes are checked ONE LINE AT A
TIME with the Edit tool as each step completes. The checkbox edits do NOT get
their own commit per checkbox — fold the checklist changes into the normal
work commits (implementation/docs/verification steps). The process record is
the line-by-line Edit sequence, not one commit per box. BULK checking (sed / scripts / a single
catch-up pass) is FORBIDDEN: it fabricates the process record and voids the
gate (observed: the cycle was bulk-checked, never committed, then reset —
the user called it out). Only a deliberate cycle reset may touch multiple lines.

**Documentation-only cycles (added 2026-09-17)**: a cycle that changes no runtime or test behaviour
(backlog moves, evidence/citation updates, workflow documentation) still fills EVERY box and still
leaves item 8 and FORBIDDEN unchecked, but may write its boxes in one pass. That is the deliberate
multi-line exception the paragraph above already allows for a cycle reset, applied to a cycle with no
implementation sequence to record; the BULK-checking prohibition keeps governing every cycle that
touches `src/`, `tests/` or `tools/`. `dotnet format` may be skipped (it only rewrites C#, and such a
cycle has none). The evidence run may use
`--filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` — but the focused
normative-gate run afterwards is NOT optional: it is the only thing that proves this checklist complete
before the commit.

**Evidence rule (added 2026-09-17)**: a checked box carries a short evidence suffix on the
SAME line — `- [ ] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file.

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: not applicable, and the reason is checkable — nothing here changes behaviour or mirrors a native call site: the only `src/` edits are three XML-doc references re-pointed to a ticket that moved folder (3 insertions / 3 deletions, `git diff --cached --stat -- src/`), and no other product file is in the staged set
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: three mechanisms, each with its consumer named: (1) the register's row shape and status column — consumers are the reader the intro now routes (`README.md` beside it and `docs/evidence/verification.md` §Self-checks both send a reader here before citing) and `SelfcheckManifestGateTests.EveryRow_CarriesARecordedStatusAndABoundedNote`; (2) the banner agreement between a row and the record it names — consumer `...EveryHistoricalRow_AgreesWithTheRecordItNames`, measured 56 `historical` rows against 56 bannered records with no disagreement in either direction; (3) the carrier a superseded row sends its reader to — consumers `...EveryHistoricalRow_NamesWhereToReadInstead` and, one level up, `DocumentCensusGateTests.EveryKeepRow_IsNamedBackByTheDocumentItNames`, which is what caught the false reach. Measured before the change: 111,705 bytes, 145 placeholder rows, 32 old-wire rows, 24 rows asserting a supersession their record did not state, 40 dangling ticket links inside `docs/evidence/selfchecks/`
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "a citation whose ticket moved folder", and it was swept rather than patched where the gate pointed: 40 occurrences inside `docs/evidence/selfchecks/` (this tree is a point-in-time record the backlog-reference gate exempts BY NAME, `RecordPrefixes` in `BacklogIntegrityGateTests`, which is why nothing had failed on them), plus 7 files outside it that the same gate's `docs/`-only scan surface cannot reach, and 12 bare `todo/<x>.md` fragments in the register's own notes that no prefixed matcher sees; after the change both sweeps answer zero, the register's 49 ticket references all resolve, and the only remaining hits are the gate tests' own synthetic samples (`x.md`, `y.md`, `a-record.md`). The same shape was fixed at the register's own level: the 24 records whose row claimed a supersession the record did not state were given the uniform banner instead of the claim being dropped
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the table is the ticket's own *The self-check register's row audit* section, one row per mechanism with a measured before and after: (1) the row shape → 111,705 → 70,650 bytes, the same 329 rows, the largest note 2,707 → 1,193 characters → `SelfcheckManifestGateTests` green 13/13; (2) the status column → 145 placeholder rows and 24 unstated supersessions → 0 residual placeholders, 0 `historical` rows without a carrier → the same gate; (3) the citations → 59 stale ticket references (40 in the selfchecks tree, 7 files outside it, and 12 bare `todo/`-style fragments in the register itself) → 0 → both sweeps re-run to zero, and `DocumentCensusGateTests` 11/11 after the one false reach was re-pointed
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: no runtime is involved and that is stated rather than assumed — a register is documentation, so its proof is a gate plus a re-runnable measurement, not a trace: the three new facts feed synthetic text to the same pure functions the real checks use (an over-long note, an unknown status, an empty note, a row and a record that disagree, a superseded row naming nothing and naming what is gone), and every number in the ticket's new section was re-derived from the working tree in this run. The one runtime-shaped claim the change makes — that a reader arrives from the two routing documents — is checkable by reading them, not by running the game
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket is the user's own 2026-10-12 directive ("梳理本项目所有文档…要么直接干掉,要么完全推翻重新整理"), and this session's handoff instruction named this exact next item ("剩下最后一份大头: `docs/evidence/selfchecks/MANIFEST.md`"), so no work-item choice was re-asked; the two design questions the ticket left open were settled by the verdict the gate would enforce, not by asking: a supersession is one fact in two places (row and banner) and the row's note is bounded to the 1,200 characters the ledger and the matrix already use
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit; `dotnet test` behaviour suite **5072/5072 passed** (net48, 49 s) and the normative gate set at **652/653 on the run taken before this line was checked, the only red being this checklist's own two open boxes** — the gate named them line by line, which is the check working rather than a defect. The three deliberate reds this cycle produced were all fixed in it: the two reference findings above, the false reach the census gate caught, and two matcher samples of my own that asserted the wrong failure count
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured after the last edit — `SelfcheckManifestGateTests.cs` is 332 lines, far under the 600-line class gate, and its case count went 7 → 13 facts, inside the 40-case cap; no new state bool and no new top-level type (the helper is the nested `Row` record the sibling census gate already uses); the mechanism this round DELETED is the placeholder sentence — 145 rows that answered a reader with nothing are gone, and the two rows that still carried it as a prefix lost it too — together with the old "complete per-file index" claim in `README.md` and `verification.md`, which the register's real contract replaced. The `docs/` tree's own gates are green over the 59 changed evidence documents (`DocumentationTreeGateTests`, `BacklogReferenceGateTests`, `BacklogIntegrityGateTests` all inside the 652/653 run above)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
