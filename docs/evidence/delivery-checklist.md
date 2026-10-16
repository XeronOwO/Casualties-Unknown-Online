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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this change adds no entry and no behaviour of its own; it moves a rule two entries already read into one place (`SpectatorPolicy`), so the console and the member card answer "who may watch" from the same predicate instead of from two copies that had already drifted. No fallback is introduced and none is removed: the card still refuses where the camera would, and the one term it cannot answer (the target's body on this client) remains refused by the camera rather than guessed
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: one mechanism moved, and its evidence is the two readers that disagreed. The camera's terms were `!localBody.alive || (hostRules.SpectateWhileUnconscious && !localBody.conscious)` over the local `Body`, guarded by the session flag; the card's were the same two terms over the published `PlayerEntity` minus the unconscious one entirely. The new predicate takes the four plain facts and is pinned by four unit cases (dead / alive / unconscious with the switch off / unconscious with it on) plus one for no session. The residue is unchanged and named in the ticket: whether the TARGET's body exists on this client is adapter-only, so the card can be right about the watcher and still have its click refused
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "every reader of the watch rule", and there were two — the camera and the member card (the right-click menu reads the card's projected fact rather than the rule, so it is covered by the same answer). Both now call one predicate, and the second copy is deleted rather than left beside it. The sweep also checked the family's other axis: what each reader knows about the TARGET. The camera checks two things the card cannot (the clone body and the published liveness), and that asymmetry is now stated in the ticket instead of being an implicit assumption the two sides happened to share
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the predicate (new, four cases plus a session case, all green); the camera (its `MayWatch` now delegates, keeping only what it alone knows — the local body and, for its log line, what state that body was in); the projection (takes the rule's four inputs instead of a precomputed bool, so a caller cannot answer the question differently); the card (passes those four from the same sources the camera reads: the published local entity, the announced host rule, the session flag); the tests (the projection's five cases moved to the new parameter names and still pin the same two facts). The one gap left — a refused card click has nowhere to report itself — is named in the ticket as the reason it stays in `todo/`
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the rule is pure and is pinned directly (five unit cases over its four states plus the session flag), which is the strongest form available for it — the alternative, proving it through the camera, needs the game running and would test Unity's field reads rather than the rule. The two readers then have their own evidence: the card's two projected facts have five cases, and the camera's behaviour is the ticket's own two-client run. What none of them can read is the one asymmetry this cycle had to name: the card can be right about the watcher and still be refused by the camera for a reason it cannot see
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: this is not a new design. It is the repair of a defect an adversarial review found in the previous cycle's implementation of the user's own ticket, and the review named the repair (one shared rule, in the Runtime, called by both readers). The user's own ticket already fixes what the rule SAYS — the three switches and their defaults are its table — so nothing here is a choice left to make
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: one new file, `SpectatorPolicy`, 25 lines including its doc, holding one static predicate — no state, no instance, no dependency. Every touched class is far from the line limit. The round's deletion is the important half: the card's own copy of the terms is GONE rather than kept beside the shared one, and the projection's precomputed `localMayWatch` bool (which let a caller answer the question differently) became the rule's four inputs, so a future caller cannot quietly reintroduce the second answer
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet format` exit 0 after the last edit; `dotnet test … --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5519/5519** behaviour cases and **662/662** gates. No gate caught anything this time, which is itself worth recording: a rule duplicated across two readers is invisible to every scan in this repository — the last cycle's copy had already dropped a host switch and the whole suite stayed green — so the only thing that can catch it is the review question that did. This change is that question's answer
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
