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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: (documentation-only cycle) this change adds no gesture and no code; it records the user's 2026-10-18 ruling that `/give` is HOST-ONLY first, and the survey names the carrier `/give` will ride (the landed giver-initiated push, `PlayerInventoryTakeService.HandleTakeCore` → `PlayerInventoryTransferMsg` → `PlayerInteractionApply.OnPlayerInventoryTransfer`) — ticket, *Stage C's design, surveyed*
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: (documentation-only cycle) no mechanism is touched; the survey cites the existing carrier's call sites rather than describing a new one, and the two pieces the implementation still needs (a Runtime id-allocation port and definition validation) are named as open rather than assumed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: (documentation-only cycle) nothing to align; the new ticket `todo/cheat-permission-and-op-commands.md` scopes the permission family before its first code, and names the verbs it will gate (`/data`, `/give`, `/spawn`)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: (documentation-only cycle) no mechanism x change cell exists; the survey's "what is still open" list is the equivalent for the next cycle — four pieces, each with what it must answer (id minting, definition validation, destination, property grammar)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: (documentation-only cycle) this cycle verifies the documents (backlog index + census + document tree + backlog references) over the new ticket and the survey; the verb's own runtime verification is the next cycle's, and it is host-only, so no member-facing row is owed by it
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-18 ruling is the approval itself — `/give` host-only first, and the `/op`/`/deop` whitelist filed as its own ticket with the Steam-ID/IP-direct identity question carried into it
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: (documentation-only cycle) no class touched; the new ticket is one page with a reader (the next implementation cycle and the backlog index), and its census row names the index as the reach
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: (documentation-only cycle) `dotnet format` is skipped by the checklist's own documentation-only exception (no C# changed); the normative suite runs over the new ticket, its index row and its census row, and is green
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
