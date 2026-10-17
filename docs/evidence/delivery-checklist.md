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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no production file changed. The one place a fallback could have shipped is `remove` on a bounded property, and the run quotes it refusing by name and naming what the verb does accept; the container case does not fall back either — the stack empties to zero and the item keeps its property
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the record now carries the liquid half of the clear path as runtime answers (`liquid water 500 ml -> 0 ml`, then the read-back at `0 ml` with the container still present), repeating identically in every run that reached it. Still UNVERIFIED with its cause: the COMPONENT half, because no run could name an item whose prefab loads a `BatteryItem` — `type=battery` is not an id the game grants, and the probe meant to find one read a field its answer does not carry. Unit cases cover all six field kinds; the runtime reading does not exist
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "every reading this batch takes", and auditing it is what turned three separate-looking failures into one defect: a process count for a transport, an assumed default for a persisted setting, and a field name assumed rather than read. All three read a PROXY for a fact, so the lesson recorded beside the failures is structural — a step reporting an absence must first show it can see the thing — and the two other readings were fixed in place: readiness reads the member's own transport, and the step that needs a rule sets it
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the record is the table — twelve rows, each with the console's own answer and a verdict. Host: read, a two-property merge landing, the read-back following, a merge refused as a whole, the read-back unchanged, a bounded property refused by name. Member: a write landing under the open rule, its read-back, the same write refused on the member's own client under the closed rule, its read-back unchanged. Clear path: a full waterbottle, the liquid emptied to zero, the read-back showing the container intact. Not judged with its cause: the component half. Every row carries `+b1cc399c`, verified before each run
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the design is the batch, and this commit is its sixth iteration, which is itself the finding. Three of the six failed on the BATCH's own readings rather than on the verbs, every one of them reading a proxy, and that generalizable lesson is written into the run record beside the failures where the next cycle meets it. The twelve rows needed no product change across all six runs — a claim this record can make because every run verified the same artifact identity before it started
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: acceptance is agent-run here and every row judged is the ticket's own verb. Nothing in six runs changed product behaviour; the batch's three fixes were a readiness reading, a stated starting condition and a probe reading the wrong field. The one judgement call this cycle is to STOP chasing the component half rather than guess more item ids, recorded as owed with its cause instead of closed or worked around
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no production file is touched. The round's deletions are in the batch: the readiness test that counted processes is gone rather than kept beside the one that reads the transport, and the tag probe that parsed a field its answer does not carry is replaced by asking the game for the item by name. The machine was left clean after every run — both clients closed, a leftover closed by exact process id when one did not exit on its own, reuse nodes shut down — which is what made six consecutive runs possible from a known state
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: no `src/`, `tests/` or `tools/` file changed, so by the repository's own rule this documentation-only change is reviewed as a diff; the normative gates were run because a tracked document was edited and carries a census row, and they are **667/667 green**. All six runs verified the deployed artifact identity BEFORE starting and all six read `+b1cc399c`, so every row is a row of the same artifact — which is what makes six runs one evidence chain rather than six attempts
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
