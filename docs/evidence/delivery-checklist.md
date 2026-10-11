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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle adds NO CUO entry and no player-visible behaviour of the PRODUCT — it is the fixture change the previous cycle's handoff ordered, and what it adds is example-mod content whose reach is call sites this repository already maps (`PlayerCamera.TryPerformRadialAction` → `Body.UseItem` for the dressing's use half, `PlayerCamera.ApplyWoundItem` and CUO's cross-player limb-tool chain for its limb half, the tile's `FluidManager.DrinkLiquid` prefix and the container's `Drink` for the tonic's drink half); the example mod's own dressing does become usable, which only an acceptance run drives, and the two shapes with no function are the state `mod-declared-behaviour-with-no-function.md` settled rather than a new gesture; the one action that needs information its gesture cannot carry (the limb) is answered by the native call's own limb rather than a fallback, and a null there now writes a line instead of returning in silence
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the mechanisms this cycle touches are the example mod's declaration and registration surface (`src/CasualtiesUnknownOnline.ModExample/ExampleMod.cs`: `Bind`, `RegisterAuthoredBehaviour`, the dressing's `Usable`/`UsableOnLimb`) and the reach they depend on, read at the source: `ModItemInfoFactory.Build` gives a declared-usable definition with no `Tool`/`Gun` the invoker's delegate that reports when nothing is registered, `GameAdapterItemContentProvider.ReportUnregisteredActions` and `GameAdapterLiquidContentProvider`'s `HasLimbUse` warning name a declared flag with no function, and a mod liquid reaches the game's own `onHealthUse` LOCALLY through a vanilla container (`WaterContainerItem.cs:289-304` reading `FluidManager.WorldFluidToLiquidID`, `FluidManager.cs:508`, which the tile provider fills with the normalised `FillLiquidId` — `LiquidId` itself for this fixture), while CUO's topical and injection chains select a container by `LiquidItemInfo { usableOnLimb: true }` and no such container auto-fills (every `usableOnLimb` liquid item in `Item.cs` sets `autoFill = false`), which is why the cross-player half is a limit the batch drives or names rather than a claim here; nothing is left as an assumption, and the one thing no source can show — that the registrations succeed on a live client — is the batch's row rather than a claim here
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is the two content kinds the registration faces serve (item, liquid) across their three halves (item use, item limb, liquid drink) plus the two no-function outcomes, and the fixture covers all of them in one pass rather than one shape at a time: both halves registered on `example.dressing`, the drink half registered on `example.tonic`, and the reporting path left as the only function for `example.placebo` and for the tonic's limb half; both build sites were re-read in this cycle to confirm the coverage is the one they actually gate
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: this cycle's table is its fixture change, one row per shape: the item use half → `example.dressing.Usable` plus `TryRegisterUse` → the bind-time verdict line the mod logs (`[Example] authored behaviour registered — dressing use={Use}, dressing limb={Limb}, tonic drink={Drink}.`) and the framework's own registration line; the item limb half → `UsableOnLimb` plus `TryRegisterLimbUse` → the same pair of lines, and on a session the cross-player limb-tool chain that reads exactly that pair; the liquid drink half → the `example.tonic` definition, its tile and `TryRegisterDrink` → the same line, and `LiquidTileDrink`'s own log when a run drinks the cell; the no-function shapes → `example.placebo` and the tonic's limb half → the provider warnings at injection; every cell is a line a batch reads rather than an inference from the source
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the runtime proof is the acceptance batch's, and the design is that the fixture reports its OWN registration verdicts at bind time in one line naming all three, so a fixture that failed a rail says so before any row is driven instead of looking like an effect that does nothing; the reach is the mechanism `mod-declared-behaviour-with-no-function.md` row 6 already records (the mod's liquid tile, drunk directly or filling a vanilla container), and the rows are numbered in the batch's scope page before its session, in the shape `docs/evidence/acceptance/20261011-j-scope.md` set
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the work order is the previous cycle's handoff, which named this exact step ("the example mod can now build a real item but registers no item action, so `mod-authored-effects` and `mod-item-authored-actions` still lack a mod that registers an action — the next cycle's small job, and then the same acceptance batch covers them"), and the three tickets carry that gap in their own *Limits*; no work-item choice was re-asked, and the fixture's shapes follow the tickets' own rows rather than a new design decision
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit; `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` green at 614 normative gates and 5064 behaviour cases, and re-run INCLUDING the checklist gate after this box was checked; this cycle's only red was its own checklist gate while its boxes were still being ticked, and the independent review's own runs reproduced both totals independently
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `src/CasualtiesUnknownOnline.ModExample/ExampleMod.cs` measured after the last edit — 448 file lines, of which the `ExampleMod` class body is 403, against the 600-line class gate, and the source-shape gate is green in the run above; no field and no state bool was added (the class still holds `_context` alone, and the new `RegisterAuthoredBehaviour` is static and stateless), no top-level type and no new file was added, and nothing became dead — the three registrations and the content they key on are exactly what the next acceptance batch drives; the three ticket files this cycle edited carry 309 / 203 / 235 lines and the checklist 51, none of them under a gate
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
