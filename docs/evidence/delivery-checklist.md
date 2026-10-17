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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the change adds no gesture (console verbs are CUO's own surface; the one remote entry is the roster announcement, which mirrors nothing) and the new rule — a member needs the host's switch AND its grant — is the user's own 2026-10-18 ruling
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: eleven mechanisms in the ticket's *Mechanism inventory* table, from the switch and the roster through the announcement's four edges, both ends of the check, `/op`/`/deop`, the no-bump fact (decision 241) and the console size split
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the cheat family was enumerated and each member was placed rather than assumed — `/data`'s write verbs are gated (front end and the kernel seam), `/data get` stays `Anyone` by the read half's own rule, the host-admin verbs stay `HostOnly` (no member request to arbitrate), `/give` is the same cycle's next commit, `/spawn` is a future verb, and `/heal` is EXCLUDED with the reason recorded (the tree's `/heal` consumes the healer's own item, which the family's own definition excludes); the announcement family was audited too: the roster rides the same four edges as the host rules and both drop on session end
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: thirteen rows in the ticket's *Self-check table*, each naming the test that proves the cell, including the order-discriminating cases at the seam, through the real composition root and at the local gate
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the runtime proof is the two-node stack in `CheatPermissionReplicationTests` (the production composition root, the real console, the real kernel protocol: the announced roster after a handshake, a grant/revoke reaching the member, the local refusal leaving nothing written, the host's refusal settling the recorded value back, the session-end drops, the IP-direct refusal), plus the seam-level cases in `KernelCommandGatewayTests`/`CommandAdmissionIntegrationTests`; the LIVE readings are the six rows the ticket records as owed to the acceptance batch, not claimed here
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket quotes the user's three 2026-10-18 rulings (the `/op` `/deop` authority rule, the combined direction, and the switch-AND-grant clarification); the ticket's own "needs extra evaluation" for IP-direct is the ground the identity decision answers, and the decision is recorded rather than re-asked
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `CommandConsoleService` 600 → 551 lines (`CommandSuggestions` extracted); new owners 220/122/119/100/89 lines, all single-purpose
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format` clean on every changed file; `dotnet test CasualtiesUnknownOnline.slnx` green: 693/693 normative gates and 5653/5653 behavioural cases
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
