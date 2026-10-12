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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: not applicable, and the reason is checkable — the diff changes no behaviour and mirrors no native call site: `git diff --name-only` lists no `src/` file, and the non-documentation half is one tool comment, two test fixtures and one gate's matchers
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: two mechanisms, each with the consumer named: the value sweep (its consumers are the records' readers and the re-runnable census scans) and the gate's matcher pair (its consumer is the gate itself, whose failure message names the file and line); the rule table that drives both halves is `.agent-local/rewrite-20261012/lib/rules.mjs`, gitignored by design because it is the operator's instrument rather than the repository's content
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "a tracked file that carries a value of this machine", audited from a census of every object in the database rather than from the reported files: `.agent-local/rewrite-20261012/scan-blobs.mjs` read all 15,804 blobs (572 dirty), and `discover.mjs` sweeps twenty classes so a value the rule table does not list would still surface — both are records now; 114 tracked files carry replacements in this commit (the first pass caught 116, two of them only through a log-volume rule that was then judged out of scope and reverted), and the guard answers `0 of 3775`
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: three rows, each observed: (1) the values in the tracked tree → 114 files rewritten to the records' placeholder vocabulary → `sweep-tree.mjs --check` answers 0 of 3775 and `git grep` for a 17-digit id names only the synthetic fixtures; (2) the gate's matchers → rewritten as shapes (a loopback endpoint carrying a port, a private-network address) with nine pin samples → focused run 28/28, and the pins did their job: they went RED TWICE first, catching `\b` before `[::1]` and a wrong octet count in the private-network branch; (3) the history → a blob map over all 15,804 objects plus a message filter → the rewrite and its scans run after this commit, and their evidence lands in the ticket
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: no runtime is involved and the reason is stated rather than assumed — nothing here changes behaviour, so the proof is the same instrument before and after: `verify-scan.mjs --rev master` reads 1468 content commits and 2 message commits NOW and must read zero of each after the rewrite, `sweep-tree.mjs --check` covers the tree at the tip, and the rewrite's own equivalence proof compares the backup and the rewritten ref commit by commit
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user ruled on 2026-10-12 (rewrite the history and force-push; no gate, a pre-commit check instead — decision 261), and this cycle asked only what that ruling left open, all three answered in the session: commit metadata is out of scope, every destructive step is approved, and the local backup branch is kept
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors, `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit and `dotnet test CasualtiesUnknownOnline.slnx` green at 5068 behaviour cases and 625 normative gates — the four red runs this cycle produced were all the cycle's own doing and all fixed in it: a moved-ticket reference in three files, two path literals inside this ticket's own census prose, an index row 3 characters over its budget, and the gate's new pin theory exposing two real matcher defects
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured after the last edit with UTF-8-aware reads — `RepositoryGateTests.cs` 415 lines and `PreflightToolTests.cs` 182, both far under the 600-line class gate; no state bool, no new field outside the gate's two matcher statics, and no new top-level type; the mechanism the cycle DELETED is the value-spelled matcher list, replaced by shapes rather than kept beside them, and the sweep itself adds no code — it removes values
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
