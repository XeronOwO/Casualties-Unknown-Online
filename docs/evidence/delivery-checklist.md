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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: (documentation-only cycle) no gesture and no code; this records the user's 2026-10-18 combined ruling (host AND guests run `/give` under the cheat permission, every cheat verb checked on both sides — the client refuses locally, the host arbitrates) as the SUPERSESSION of the earlier host-only answer, in both tickets — `cheat-permission-and-op-commands.md` (*The combined ruling*) and `item-and-entity-data-commands.md` (*Stage C's design*)
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: (documentation-only cycle) no mechanism touched; the ruling names the two halves' homes (the client's local refusal reads the announced permission, the host's arbitration sits at the admission seam and the verb's own seam) and the family it covers (`/data` writes, `/give`, `/spawn`, `/heal`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: (documentation-only cycle) the ruling is written FAMILY-WIDE rather than for `/give` alone: the cheat verbs are listed with their two-sided check, and the host-admin verbs (`/hostrules`, `/kick`, `/ban`, `/unban`) and the `/data` read are explicitly named as needing none, so the boundary is decided before the first line of code
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: (documentation-only cycle) no mechanism x change cell exists; the two tickets now carry the equivalence: each cheat verb gets a front-end row (client refuses by name) and a back-end row (host judges and answers), with the permission ticket's open questions (IP-direct identity, session scope, the refusal's way back) named as what the implementation must answer
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: (documentation-only cycle) this cycle verifies the documents (backlog index, census, document tree, backlog references) over the changed tickets; the implementation cycle's proof is decided in the ruling itself: a local refusal read on the operator's console, and the host's refusal read in its log and answered to the sender
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's combined direction of 2026-10-18 is the approval and the scope, quoted verbatim in the permission ticket; the host-only step it supersedes is marked SUPERSEDED rather than deleted, so the record shows what changed and why
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: (documentation-only cycle) no class touched; the permission ticket is one page with a reader (the implementation cycle and the backlog index), its priority now matches its index row (`Medium-High`), and the supersession sits in one place rather than two drifting ones
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: (documentation-only cycle) `dotnet format` is skipped by the checklist's own documentation-only exception (no C# changed); the normative suite runs over the two changed tickets and the index row and is green (693/693)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
