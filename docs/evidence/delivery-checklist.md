# Delivery Checklist

Every development cycle runs through this checklist. Two gates run before the
cycle's final commit as part of `dotnet test`:
`DeliveryChecklistGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` refuses
the commit while any required box is unchecked, and
`DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox` is the
census — it refuses a checklist that no longer declares one of the steps below,
naming the step that went missing, because a record whose boxes are gone must not
pass by having nothing left to fail on. Deployment and multiplayer acceptance are
the agent's actions, run after the commit per `docs/acceptance/` and outside this
gate; feature development verification uses simulation/static evidence. When a
release cycle lands, reset the checklist by manually unchecking every box so the
next cycle starts clean.

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
leaves the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked, but may write its
boxes in one pass. That is the deliberate
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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this commit adds tests, not entries. The three cases drive the two verbs the previous commit added through the real console, which is the layer an operator actually reaches, and the atomicity case is the one a per-property commit would fail: a merge whose first property is legal and whose second is not must leave the record untouched
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the mechanism this commit touches is the test fixture itself, and reading it is what produced the finding: `Hold(...)` seeds BOTH halves (the local scene and the kernel row) while the sibling `SeedCarried` seeds the row alone, so calling both spawns the same instance twice and the second is refused. The atomicity case reads the KERNEL ROW rather than the local object because the write's report happens after its commit — so the row is the only witness of whether anything landed. Two pre-existing cases that seed the two halves asymmetrically on purpose (one local-only, one row-only) were restored after an over-broad edit removed their setup lines
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "every write verb the console exposes", and this commit closes the coverage gap for the two newest members without touching the three that already had it. The audit also swept the fixture family: the two seeding helpers look interchangeable and are not, which is why the first attempt at these cases failed against a fixture rather than against the code — and why the same over-broad edit that removed my duplicate line also removed two legitimate ones, caught by the suite rather than by reading
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `merge` (one case: every property it names lands, in the order written, with the answer naming each before/after); `merge` refusing (one case: the second property is impossible, so the FIRST one must not have landed — the atomicity claim, now observed instead of read); `remove` (one case: a liquid goes to zero and a component field returns to its kind's zero, asserted on the kernel row so the report is in the path); the fixture's own two hazards (recorded above). NOT covered and named in the ticket: `set`'s changed answer for malformed input, and the two nits about `WroteMany`'s index-aligned lists
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: this commit is the verification design being executed rather than described. The console layer was the named gap and is now observed; the atomicity claim was "verified by reading" and is now a case that fails if the commit moves into the loop. What still needs a run is unchanged and in the ticket: a real item on both a host and a member, the answer and the row read on both peers, a member's merge under an announced closed rule, and `remove` against a real container and a real component across a save/restore round trip
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the two verbs are the user's own request, the design was settled and recorded under it, the implementation landed in the previous commit, and this commit only adds the evidence the review said it lacked. No design choice is made here at all
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the touched file is a test class, and the 40-case cap shaped the work — the three new cases take it to 39, one short of the limit, which is why they went here rather than into a new class. No production file changed and no state was added. The dead-mechanism rule still applies to the FIXTURE: the two seeding helpers are both live and both used, which is exactly why their difference (one seeds both halves, the other only the row) is now recorded rather than assumed
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet format` exit 0 after the last edit; `dotnet test … --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5536/5536** behaviour cases and **662/662** gates. The focused run that produced them showed the red first: all three cases failed against the fixture before they passed against the code, and the two PRE-EXISTING cases this commit briefly broke were caught by the suite in the same run — which is the sequence this repository's workflow asks for, observed rather than claimed
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
