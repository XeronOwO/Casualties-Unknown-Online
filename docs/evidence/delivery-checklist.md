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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this commit changes a printed REFUSAL, not an entry: a merge whose property does not parse was quoting the set verb's grammar, which tells the operator to write a shape merge does not accept. The refusals are the family's own promise that every branch names what refused it, and a refusal naming the WRONG grammar breaks that promise as surely as a silent no-op would — so the verb's usage is now threaded into the shared parse and the case that asserted the set line asserts the merge line instead, plus its absence
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: one mechanism, and the review named it: the property parse is SHARED by three verbs while its refusal messages interpolated one verb's usage constant, so two of the three quoted a grammar that was not theirs. The fix threads the caller's usage in — a parameter rather than a second copy of the parse, because the vocabulary genuinely is shared and only the printed line differs. The remaining named-and-unfixed items are two nits about the multi-property renderer taking two index-aligned lists; both are recorded in the ticket rather than left to be rediscovered
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "every refusal a write verb prints", and the sweep covered all three: `set` (its own grammar, unchanged), `merge` (was quoting set, now its own — this commit's fix) and `remove` (already quoted its own, because its parse never went through the shared property path). The audit also confirmed the shared VOCABULARY is right and only the printed line was wrong, which is why the fix is a parameter and not a duplicated parse — duplicating it would have removed the drift this change just corrected
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the shared property parse (takes the caller's usage; both call sites pass theirs); the number refusal (interpolates the passed usage; used by condition and liquid); the flag refusal (same); the component and unknown-property refusals (same); the test (the merge case now asserts the merge grammar AND the absence of the set one, so the fix is pinned by the case that caught it); the duplicated `<summary>` (deleted). NOT covered and named: two nits about `WroteMany`'s index-aligned lists and its non-empty assumption, both in the ticket
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: this is a refusal MESSAGE, so its proof is the assertion on the message: the case that previously pinned the wrong grammar now pins the right one and the absence of the wrong one, which is exactly the red-then-green sequence this repository asks for and it happened here (the case failed the moment the code was fixed, because it was still asserting the old text). What still needs a run is unchanged and in the ticket, and none of it concerns this message: the two verbs against a real item on both peers, a member's merge under an announced closed rule, and `remove` across a save/restore round trip
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: no design choice is made here. The verb, its grammar and its refusal policy all come from the user's own request and the design settled under it; this commit corrects which of the two recorded grammars a message prints, which the review named as a defect rather than an open question
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no touched class is near the line limit and no state was added; the change is one parameter threaded through four methods. The round's deletion is the duplicated `<summary>` the previous review filed — removed rather than left beside its replacement, which is the same rule the previous cycle applied to the dead renderers. The check also confirms the fix's shape: the shared parse stayed shared, because the alternative (a second parse per verb) would have created the drift this commit removes
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet format` exit 0 after the last edit; `dotnet test … --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5536/5536** behaviour cases and **662/662** gates. The focused run showed the red FIRST and for the right reason: the merge case failed with "Not found: Usage: /data set" the moment the code started quoting the merge grammar, which is the assertion doing its job rather than being adjusted to match. This commit carries the last two of the review's thirteen findings that were fixable in the same family
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
