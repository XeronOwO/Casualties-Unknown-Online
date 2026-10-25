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

**Input rule (added 2026-10-22)**: the Entry mapping box also records the exact input sequence the
player performs for a new or changed player-facing gesture — which mouse button is held, which window is
open, what can still be clicked or pressed while it is held — and walks it through the game's own control
flow. A gesture the controls cannot perform is a design defect, not evidence; a call seam a probe can
drive is not evidence that a player can reach it. An "impossible" verdict lists EVERY entrance that opens
the window the gesture needs and the button each one takes (a conflict on one entrance is not the control
scheme), and an entrance behind a hotkey the player cannot be expected to know is not the primary entry:
the gesture opens what it needs from the act itself.

- [x] Design comprehension — before the first code edit the ticket states, in its own section, the surface being changed
      (`docs/architecture/surfaces.md` row), what that row says the behaviour is judged against, the record quoted, and what the
      change must NOT alter; a `GAP` row is closed by reading and recording the authority first, never by guessing —
      evidence: this cycle changes documentation only (it lands `docs/architecture/surfaces.md` itself), so the first ticket that
      owes the statement is `review/selectors-exclude-the-executor.md`, whose measured fallout (32 tests, migrated in
      `7fb1556d`) is exactly what the row would have exposed before the change
- [x] Native parity — the three lines (player's action and expectation / the native chain the game itself runs / how the
      remote case reaches that same chain, or the named reason it cannot) are written in the ticket BEFORE the first code
      edit, and no forbidden shape is used to fit a mechanism — evidence: this cycle is documentation-only (it lands
      `docs/development/native-parity.md`), so no behaviour owes the three lines; the first ticket that does is
      `todo/cross-player-eat-skips-native-steps.md`, whose three lines landed in `c6c4b607` before any code, and the
      forbidden shapes plus the two that shipped this session are named on that page
- [x] Native parity probe — for a behaviour that mirrors a native action, the SAME action was run locally and across
      players in one session, and the two effect lists (native call, frame, audio, both clients' state) were compared,
      with every difference either fixed or named as a user decision — evidence: this cycle is documentation-only, so no
      run is owed; the first owed run is the eat ticket's run-sheet step 6 (give + eat, both clients' item state, the peel
      step's presence, a frame at the eating moment), which the probe table on the new page defines
- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the entry is the existing cross-player DRAG-USE release (a native mouse drag of an item onto a teammate), unchanged by this cycle; the change decides what that release does with a definition whose native action is not a drink: it refuses by name BEFORE the action runs, instead of running it and refusing after (which was the destructive shape). No new gesture or window; the refusal sentence is the only player-visible change and it replaces a refusal the player already read
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the destructive path is decompiled (`bloodbag`/`bloodbaghuman` are `usable = true`, their `useAction` is `Item.DrawBlood(item.GetComponent<WaterContainerItem>(), body.limbs[1])` at `Item.cs:1762-1765`, `1928-1931`, and it writes `bloodVolume`/`AddLiquid("blood", …)`); a real drink's action calls `WaterContainerItem.Drink` (the water bottle's delegate, read in `Item.cs`); the reader is Harmony's `PatchProcessor.GetOriginalInstructions`, the same one `GameSolidFoodFacts` already uses (one level of game calls); the capture window and the two callers (`CrossPlayerDragUse`, `GameAdapterBridge.MeasureRemoteDrinkDose`) were read; no runtime log was needed for the mechanism because the change is a definition classification (the machine row is the behaviour half, owed)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the compiled-body reader is now ONE shared type (`UseActionInstructions`) used by both content classifiers, so the food and drink families cannot drift apart in how they read a use action; `GameSolidFoodFacts` keeps its exact verdicts and only its call sites changed; both `TryMeasure` callers inherit the new refusal; the food classifier was checked for the same "run it to find out" flaw (it already reads, never runs); the topical sibling that still runs its limb action before checking is FILED (`review/topical-limb-action-shape.md`, since settled on native parity) and the injection sibling is not vanilla-reachable (every vanilla injectable's limb action reaches `Inject` and its admission requires an injectable stack); `LocalUseItemEligibility`'s drink admission was read and needed no change (it decides the family from data, the new check decides the definition's action); the source gate now pins both classifiers' reader use and the refusal's order
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: (1) `GameDrinkActionFacts` answers `Drinks` only when the item's own action reaches `WaterContainerItem.Drink` (waterbottle's delegate read in the decompiled `Item.cs`); `NotADrink` for the two blood bags (`DrawBlood`), absent/unusable/no-action items; `Unreadable` for a dynamic body; cached per id; (2) `RemoteDrinkUseHandler.TryMeasure` refuses on both non-drink verdicts BEFORE the capture window opens and names the definition, and a source gate now pins that ORDER (classifier call before `_capture` and `info.useAction`); (3) the source gates: `SolidFoodChainGateTests` 12/12 (food fact updated to the shared reader, the drink-verdict fact, the order fact, and the drink classifier added to the no-id-table scan, `.acceptance/20261024-c/solidfood-gate2.log`); (4) build 0/0; full behaviour 6357/6357; no unit test can reach the IL (the test host does not load the game assembly) — stated in the ticket as the reason the source gate is the L0 half
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the two content classifiers' source gate (the IL cannot be built by the unit host, so the gate pins the reader and the call it looks for); L1 is the ticket's two-client machine row — drain/refill a blood bag with water, then with a health-usable liquid, drag it onto a teammate, and read the operator's bag, the target's blood volume and the render clone's blood volume before/after (unchanged means the action never ran). The drag is a native mouse gesture the driver's UI cannot perform, so the BEHAVIOUR half can be probed through the release seam (`MeasureRemoteDrinkDose`/`TryMeasure`) while the GESTURE's reachability is the decompiled drag path — the ticket and the checklist name that split rather than pretending the seam proves the gesture
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket's own "What a fix has to decide" names the delegate-body classification as the shape (the `GameSolidFoodFacts` pattern), and the change implements exactly that; the refusal/drop behaviour for a released non-drink item is the ticket's own choice (refuse by name); no user-visible design was invented
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `UseActionInstructions` 107 lines (the reader moved out of `GameSolidFoodFacts`, one copy), `GameDrinkActionFacts` 74, `GameSolidFoodFacts` shrank from 208 to 118, `RemoteDrinkUseHandler` 153 (the refusal switch added); no mutable state beyond the classifier's cache, a `ConcurrentDictionary<string, Verdict>` read once per id (concurrent because the bridge method is public and a mod could call off-thread; the food classifier's cache is a DI-singleton instance field, so it is a different shape and this one names its own); the reader helpers were DELETED from the food class, not duplicated; one `PatchProcessor.GetOriginalInstructions` call site in the adapter for this question, which the gate pins by naming the reader file
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors (`.acceptance/20261024-c/drink-build5.log`); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`.acceptance/20261024-c/drink-format3.log`); the final full run after this checklist was completed is `.acceptance/20261024-c/drink-final-full2.log` — behaviour 6357/6357 and gates 779/779 green (the gate project gained the drink-verdict and order facts)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
