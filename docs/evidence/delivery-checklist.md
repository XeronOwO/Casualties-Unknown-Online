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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the entry is the native release of a dragged item onto the displayed inventory, and the call site it mirrors is the host's own custody core that the receiver-initiated half already uses — one core, two directions, so the two halves of one hand-over cannot disagree. The gesture carries its operands (which member, which destination) from what the pointer was on, resolved at the call site rather than inferred later. It does NOT ship a fallback: a release whose item has no identity, or whose target is this client, is refused with its line rather than guessed at. One user-visible behaviour IS cut — the container target — and it is recorded as a cut in the ticket rather than silently degraded
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: four mechanisms, each read before it was used, and one that the review corrected. (1) `InvButton.isBody` is `slot >= 0` and `InvButton.slot` is the ring index — so a release on a body slot resolves a slot and a release on an item resolves an item, and the two shapes are cleanly separated; (2) the item's own identity comes from `ItemInstanceId` through the tracing helper, NOT from `item.id`, which is the template string — the first survey got this wrong and the review caught it, and following the survey would have put a template name on the wire and refused every give; (3) the custody core's two ends are `from = owner`, `to = sender`, which is what the mirrored call has to swap; (4) the host entry guard's `owner != requester` is a take-direction rule — the review proved the give direction violates it BY CONSTRUCTION, which made the whole feature dead until the guard was scoped by direction. UNVERIFIED and named in the ticket: whether the receiver's own native placement rules ever run (the review showed they do not), and the actor-side visibility of a refusal after the send
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "every reader of the item's owner and the destination body", and the audit found the one member the first cut missed: the host's entry guard, which read the direction as a constant rather than as a property of the kind. The same sweep found two more of the same shape and they are fixed with it — the in-world check assumed the destination was the requester (it is a different player here, so it is checked separately now), and the line-of-sight gate still reads the take direction's end. The seam family was swept too: the widened member's own stale summary was deleted rather than stacked on top of the new one, and the return value that nothing read was removed instead of kept as decoration
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the kind (16, with a real producer and a real host consumer — and the review proved the consumer was unreachable until the guard was fixed); the release target (carries its two operands instead of describing them); the seam (widened by two operands, void rather than a bool nothing reads, its contract case pinning arity and types); the classifier (sends when the item can be named and the target is another player, keeps the old refusal line otherwise — the review asked whether any previously-refused release is now silently sent, and the answer is no: the old condition refused unconditionally, the new one refuses exactly the cases that cannot be named); the host core (one core, two directions); the host entry (direction-scoped guard, destination checked in world); the tests (the mirrored host-half case is green, which the first attempt could not achieve). NOT covered: the container destination (refused by design, recorded), the receiver-side native rules (shown not to run), and every acceptance row — all named in the ticket
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: three layers, and this cycle is the proof that the middle one matters. Unit-provable and proven: the mirrored host-half case (the same item leaves the giver's snapshot and appears in the receiver's at the named slot), the self-destination refusal, the seam's arity and types, and the wire's round trip. The classification half cannot be unit-tested at all — it needs a Unity `Item` — so its evidence is the log line it writes on the actor's client plus the host's answer. Only a run can show: the receiving client's placement, the actor-side visibility of a later refusal (which the review showed is currently nowhere), and whether the container release's refusal reads honestly. The ticket's own acceptance table lists all six rows, and the review re-judged every one of them as unpassable as the tree stood — which is why the ticket is still open
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket is the user's own question and their own promoted ruling ("it matches intuition — it should be done"), and the gesture it describes is implemented exactly: drag your own item onto the other player's displayed backpack. The ONE user-visible scope cut this cycle makes (the container target) is written into the ticket as a cut rather than shipped as if complete — which is the rule this box encodes, applied rather than restated
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no touched class is near the limit; the largest change is a new method on an existing service (`HandleRemoteGiveToBody`, four lines, a mirror of its sibling) plus a direction-scoped guard. State went DOWN: the seam's new `bool` return was DELETED in the same round the review showed nothing reads it, and the duplicated `<summary>` block was removed rather than left beside the new one. The round also confirms the family's own rule at its sharpest: a new enum member with an unreachable consumer passed every build, every gate and 5519 behaviour cases — the only thing that caught it was an adversarial reviewer reading the direction of a guard
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet format` exit 0 after the last edit; `dotnet test … --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5521/5521** behaviour cases and **662/662** gates. Two of the repository's own contract cases caught this change and were updated deliberately rather than waved through: the seam's reflection contract (it pins the widened member's arity and types — it did NOT see the return-type change, which the review noted as a gap in what that pin can reach) and the family's own mirrored case, which is the strongest evidence in this cycle: it FAILED before the guard was fixed, was removed rather than left red, and PASSES now that the direction is read correctly. The independent adversarial review ran against the FROZEN tree BEFORE this line and found **2 blockers, 4 majors, 7 minors and 2 nits** — the first blocker being that the feature was dead and the second that the test meant to prove otherwise agreed with it. Both, the identity-consistency major, and the cheap minors are fixed here; the rest is named in the ticket with why it blocks acceptance
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
