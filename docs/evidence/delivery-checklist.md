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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this change adds no entry that mirrors a native action — it is a console verb that WRITES the item state the game's own model already carries, through the same report route a native item use takes, so the write lands in the record exactly as a played one does. The one place the gesture cannot carry its own information is the target: `remove` names properties without values and cannot know which of them exist, so a property that is not on the item is refused BY NAME with what the item does carry rather than skipped. No fallback is introduced: a bounded property asked to be removed is refused rather than quietly written to its range's floor, which is a choice the operator owns and not the verb's
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: three mechanisms, all read before use. (1) The write path the shipped `set` verb already had — session check, announced-rule interception, one-item resolution, an in-memory capture mutated by the apply step and committed once — which is reused unchanged, so the atomicity of a merge is a property of a path that already existed rather than a new guarantee. (2) The capture itself (`ItemStateCodec.CaptureItem` → fresh `LiquidStackMsg`/`ComponentStateMsg` objects), which is why a failing property cannot leak into the scene: the commit is the only write. (3) The field kinds (`SaveableFieldKind` 1..6, with `Unsupported = 0` never captured) mapped to the member each restore reads, checked kind by kind so a reset cannot write the wrong member of the union-ish message. UNVERIFIED and named in the ticket: every console-level behaviour of the two new verbs, and the atomicity on a real scene — no test reaches either
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "every `/data` verb", and the audit found the one member the change had left behind: an operator typing a bare `/data` was told about `get` and `set` only, so the two new verbs were undiscoverable from the console even though the grammar for them existed. The three surfaces that name the verbs — the command's registration, the bare usage answer and the unknown-verb answer — now name all four. The same sweep corrected a sibling defect the review caught: the success log had lost the property name, so a merge logged three values with nothing to attribute them to; the answer line and the log now have their own lists rather than one shared one that had to serve both
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the parser (one property parse shared by three verbs, with an exactness flag so `set` still refuses a stray token while `merge` continues; 8 new cases in their own class, and the class split was itself forced by the repository's 40-case cap); the value layer (a clear path per kind, 4 new cases); the write path (generalised from one write to a list, one target, one commit); the answer (a multi-property renderer, with the exact-text cases now pointing at the live one); the log (per-property description restored); the help (all four verbs named). NOT covered and named: no console-level case for either verb, and no test for the atomicity claim. The split test class was verified to be a SIBLING rather than a move — the parser's own class is byte-identical to before at 33 cases and the new one adds 8, which is exactly the 41 that would have tripped the cap
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: two layers are unit-proven — the parse shapes and refusals (8 cases) and the clear path per field kind (4 cases) — and the console layer is where this change is currently unproven, which the review named and the ticket records. What a run owes: a `merge` and a `remove` against a live item on both a host and a member, reading the console answer, the local object, the reported row and `/data get` on both peers; a member's merge under an announced closed rule; and `remove liquid <id>` on a real container plus `remove component <Type> <field>` on a real component over a save/restore round trip. The logs are the trace: every write names the verb, the item, the property (or all of them) and the before/after values, and every refusal names the check that refused it
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the family is the user's own 2026-10-07 request ("gettable, settable, mergeable and removable"), the read half and the first write verb landed earlier under it, and this change finishes the two verbs that request named. The design it implements was settled and recorded the previous cycle, from the landed verb rather than invented, and the implementation matches it on every substantive point (one target, one commit, `remove` limited to the properties with an absent state, permission inherited with no new flag and no new wire kind). The one place the implementation departs is a printed grammar line, recorded in the ticket
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no touched class is near the line limit and no boolean state was added. The round's deletion is the finding worth recording: two answer renderers had been kept deliberately "so the verbs that already ship print what they printed before", and by the time the review looked, nothing in `src/` called either of them — dead code whose EXACT-text tests were defending a method no operator could reach. Both are deleted and those cases now point at the live renderer, which composes the same sentence byte for byte. The same round split a test class that had hit the repository's 40-case cap, into a sibling rather than a move. Unfixed and named in the ticket: a duplicated `<summary>` left on the write method
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet format` exit 0 after the last edit; `dotnet test … --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5533/5533** behaviour cases and **662/662** gates. Two of the repository's own gates caught this change: the test-class-size gate (the parser's family hit 40 cases, which is why the new class is a sibling) and the existing console case that pinned the "not implemented yet" text — that one was corrected rather than deleted, because it was pinning an answer that this change makes untrue. The independent adversarial review ran against the FROZEN tree BEFORE this line and found **no blocker: 2 majors, 8 minors, 5 nits**, both majors about evidence rather than behaviour — no console-level case for either verb and no test for the atomicity claim. Its dead-renderer finding, the lost property name in the log, the undiscoverable verbs and the ambiguous refusal test are fixed here; the rest is named in the ticket
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
