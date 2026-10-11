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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no gesture is added, changed or re-scoped; this is an identity rule inside the session (which counter an id space may resume from), and its native counterpart is the Continue click, whose native save carries no item instance identity at all (`reversing/Assembly-CSharp/Assembly-CSharp/SavedItem.cs` holds `id`/`condition`/`slot`/`wearSlot`/`favourited`, no counter), so the id space is CUO's own rule and its scope is argued in the ticket rather than inherited from an older table; no fallback supplies information a gesture cannot carry because no gesture is involved; the one user-visible effect — a continued world stops re-issuing live ids — is the defect the opening handoff ordered fixed, not a new user-visible design choice
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every touched mechanism names its own file and the quoted text — the format (`ItemInstanceIds`: compose, the counter, the id space, the synthesized high bit), the resume (`ItemIdCoordinator.ResumeRestoredIds`: per-space maxima from the restored rows, local event, member grant), the seam (`ItemKernelProjectionWiring.OnCheckpointRestored`: the checkpoint's rows at the click, before the generation that can allocate), the one consumer (`ItemIdAllocator.SetWatermark`, raise-only, reached through `GameAdapterSessionBinding.OnItemIdWatermark`), the session boundary (`ResetSessionState` keeps the marks), and the two composers that now share the format (`ItemIdAllocator.Next`, `OtherMedicalRemovalApplier.NextAwardedItemId`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "an id a restore re-stamps while a per-process counter restarts", and each member was walked: the plain counter is fixed in BOTH directions (the local resume and the member grant, one rule and one table), the synthesized namespace's ids are skipped by the resume so the plain counter can never be pushed into that namespace (its own counter is the sibling gap), and the two paths the seed does not read — the character file's `CharacterItemMsg.InstanceId` and the mid-session-crash grant window — are named in the landing's Limits and filed as `todo/id-resume-sources-still-unseeded.md` rather than left silent
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the moved ticket's `What landed` table is the mechanism × change × where map (7 rows, each naming the file), its Limits name the three paths the rule still does not cover, and the behaviour was seen RED twice before it was green: the three restore-driven tests failing on the unfixed tree (`Collections differ` / `The collection was empty` / `Values differ (0, expected 9)`) and, inside the fix itself, the reverted owner comparison failing exactly `RestoredCut_ResumesTheLocalAllocator_AboveTheIdsTheArchiveReissued` while the other five passed
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: three layers, each with its reach named — the format contract (`TheIdSpaceKey_IsTheAccountIdsLowBits_AndTheSynthesizedNamespaceIsMarked`, which is what makes the fixture's account-sized ids meaningful), the Runtime integration cases over the shipped services (`ItemIdCoordinatorTests`: local resume above the LOCAL space's maximum and not the archive's overall one, present member granted now with the other member untouched, absent member's rejoin grant carrying the restored mark across a session reset), and the real-machine probe the ticket's step 1 demanded (pre-fix red reproduced against the deployed artifact: a re-issued counter-0 id plus the kernel's `already exists` refusal; post-fix green read from the resume log line and the new id's counter)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the opening handoff names the work item, its order and that it needs no user action ("下一步建议:做 High 优先级的 `item-id-counter-is-not-resumed-by-a-restore`"), the ticket itself prescribes the fix's shape and asks for the reproduction first, and nothing user-visible was parked as a limit: the change removes a defect, invents no gesture and changes no design the user chose
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet test CasualtiesUnknownOnline.slnx` green — behaviour 4952/4952 (was 4951; the four id-coordination cases replace none and the format contract adds one), gates 615/615; the one gate this change broke was fixed inside it (the sync-coverage evidence quote that named the old watermark lookup line, now the space-keyed one) and the fixture change (`ItemSimWorld.Create`'s account-id parameters) left every other suite green
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the touched classes are small and single-purpose (`ItemInstanceIds` 50, `ItemIdCoordinator` 312, `ItemKernelProjectionWiring` 170, `ItemIdAllocator` 98, `OtherMedicalRemovalApplier` 164 lines), all far under the 600-line gate, and `ItemService` (537) gained one constructor argument only; no state bool was added — the new state is the per-space watermark table the class already owned, now keyed by the id space and no longer cleared at a session end; the duplicated format literal was DELETED rather than left beside the new owner (`ItemIdAllocator.Next`'s inline `(counter << 32) | (uint)…)` and `OtherMedicalRemovalApplier`'s private high-bit constant both moved into `ItemInstanceIds`), and the fix's own first version was corrected rather than patched around: the owner-half comparison it got wrong is now pinned by a test that uses account-sized ids
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
