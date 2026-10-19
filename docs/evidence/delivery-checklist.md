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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle adds no gesture, no entry and no wire member; it changes what the RECEIVING client does with an item a hand-over already delivered (the refused container now places its object in a slot instead of losing it) — the receiver's own rules deciding is the user's own row-4 ruling (2026-10-17, restated 2026-10-18), and the give gesture's entry has no native counterpart by the ticket's recorded design; the cycle adds NO new player-visible outcome, and the one it first added — dropping the item at the receiver's feet when no slot exists — was WITHDRAWN after review (its spawn report is refused by the kernel), so that case reports the item's end instead: the same loss the old code produced, now loud and consistent, recorded in the ticket's limits and carried by `todo/placement-outcome-never-returns.md`
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: six mechanisms, all read: `ItemStateCodec.RestoreContent` (the object it materializes is answered, not destroyed) and `MaterializeItem` (split out of `RestoreItem`, id stamped before `Item.Start`), `PlayerInventoryPlacement`'s container rung and its verified slot loop (`Body.GetItem(int)` at Body.cs:1346 reads `slots[slot].transform.GetChild(0)`, so the read-back is a real landing test), the game's own placement guard — `Body.PickUpItem` at Body.cs:1390 ends with `(!item.Stats.onlyHoldInHands || this.slots[slot].isHand)`, which `force` does NOT bypass (the reason the loop tries every free slot and verifies each attempt) — the destroy chain `ItemPatches.ItemOnDestroyPatch` → `ItemWorldSync.OnItemDestroyed` (the deferred destroy that reports an id dead — read in the code and in batch 20261021-a's own `op=6 … OnItemDestroyed result=Committed(1)`), and the bulk restore's discard policy (`ItemStateCodec.RestoreContentDiscarding`, unchanged and ticketed); no new game API was touched
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every `RestoreContent` call site was walked — `PlayerInventoryPlacement` (the reported defect: it places the materialized object), `ItemStateCodec.RestoreContents` (the bulk loop), `RemoteItemSceneOps.ApplyAuthoritativeState` (the scene apply) and the codec's own recursion; the shell-out `RestoreItem` was split into `MaterializeItem` + its own hand-off so BOTH placement paths share the verified slot loop and neither can hand the item to an unverified `PickUpItem`; the bulk/scene half has no destination to place into, so its policy is unchanged, named and filed as `todo/restore-refused-child-keeps-its-id.md`; the same walk produced `todo/placement-outcome-never-returns.md` for the host-ledger half AND for the no-room case (the withdrawn drop rung's failure is recorded there with the code it depends on)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's fix section carries the five-row table (the codec's refusal; the container rung's hand-off; the verified slot loop; the materialize/place split; the destroy chain it no longer reaches), each row naming its gate, its source read or its log line
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: two halves, because the behaviour suite cannot host the refusal (real Unity objects on both sides of it) — a source gate written and run RED against the old code first (`.acceptance/20261021-b/red-container-refusal.log`, six named violations) and green after it, with its own matcher contract covering seven discard shapes including the ones the first version could not see (`.acceptance/20261021-b/focused-container-refusal.log`); and, at runtime, the next acceptance batch's first row re-driving the rejected shape on the deployed artifact, reading the receiver's refusal/fallback/landing lines, the item alive in a slot with its id, the absence of an `OnItemDestroyed` for that id, and the host's kernel not storing it terminal
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-19 handoff names this fix as the round's first work item and orders its sequence (fix → redeploy → re-read row 4), and the rejection's own note freezes the shape: "do not destroy a materialized child the fallback is about to own — hand the same object to the slot fallback … and add a regression case that reads the item alive in a slot afterwards"
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured after the final edit with the gate's own count (`File.ReadAllLines().Length`) — `ItemStateCodec` 556, `PlayerInteractionApply` 497, `PlayerInventoryPlacement` 268, `WorldItemCreation` 90, the new `RestoredContent` 28, all under the 600 ceiling. The gate's ceiling is a `src/` measure (the registry `docs/architecture-debt.json` covers `src/`), so the touched TEST file `SourceShapeGateTests.cs` (1002 lines) is outside it and stays a test suite; no new bool state (the result struct carries the object, a flag and a reason); nothing left dead — `DropCreatedItemAtLocalBody` keeps its `Destination.Drop` caller, `AddCarriedItemToLocalBody` keeps the unmaterialized branch and the slot destination, `MaterializeItem` is used by both placement paths and by `RestoreItem`, and the withdrawn drop rung (`ReportCreated`/`DropRestoredItemAtLocalBody`) was deleted with its gate pin
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0, recorded in `.acceptance/20261021-b/format3.log` (the exit code is appended to the log); `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors (`.acceptance/20261021-b/build-solution3.log`); focused family 11/11 with the filter `FullyQualifiedName~ContainerRefusal|FullyQualifiedName~GiveLadder|FullyQualifiedName~ContainerDestination` (`.acceptance/20261021-b/focused-container-refusal.log`, exit 0); the full run with ONLY the checklist gate excluded — `--filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` — is 749/749 normative gates and 6019/6019 behaviour tests, exit 0 (`.acceptance/20261021-b/full.log`); and the unfiltered gate project, which is what proves this checklist complete, is 750/750 (`.acceptance/20261021-b/gates-final.log`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
