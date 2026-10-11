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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle adds NO entry — it fills the function behind two call sites the sibling ticket's entry-mapping table already names (`WaterContainerItem.Drink` → `onDrink`, `WaterContainerItem.ApplyToLimb` / `Inject` → `onHealthUse`), and the effect receives the limb that call site named, never a framework-chosen substitute (`ModEffectLimb` is built from the limb the delegate received); the two halves are registered independently so the liquid's own data decides which runs; the one user-visible behaviour change — a mod liquid's effect now runs — was the approved scope question of 2026-10-11
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's *What landed* names all six mechanisms with their files, and the census behind the operation list cites `Liquids.cs` (73 registry entries, 73 `onDrink` / 32 `onHealthUse` bodies) with the write set per group; the two mechanisms L0 cannot reach — the invoker's guarded branch and the operation tables over a live `Body` — are marked unverified in the ticket's *Limits* and carry the acceptance row
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the census swept the WHOLE liquid registry rather than the reported case, and the family audit reached all FIVE sites that run an effect (the game's own drink, the CUO cross-player appliers, CUO's custom-byte `LiquidTileDrink`, CUO's own provider build site, and `NonDescriptCan.Eat`'s weighted random registry pick — the fifth one the independent review caught) — all five route through the one installed delegate; the sibling mechanisms outside this stage (the item's `useAction` / `useLimbAction`) are named as the declared follow-on rather than left silent, and the two tickets that described the old "no function is expressible" state were corrected in the same commit (`mod-declared-behaviour-with-no-function.md`, `mod-content-ceiling.md`)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's *Self-check table (as delivered)* has seven rows, each naming its mechanism, its change and its evidence, including the two mutation checks (deleting one mapping arm and drifting one read/write pair each turned `ModEffectOperationGateTests` red) and the honest empty cell (no red was observable for the invoker or the tables at L0, recorded in *Limits*)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: three layers, each with its own trace — the registration rails are driven directly (`ModLiquidEffectRegistrationTests`, 9 cases), the mapping tables are reconciled against the enums by the source gate (`ModEffectOperationGateTests`, mutation-checked in both directions), and the RUNTIME half is designed as the acceptance row: the invoker's no-effect line (`[LiquidContent] {Id} was used …`) and its thrown-effect line (`… effect for {Id} threw — the action completed without it.`) are the traces a live session reads, together with the mod's own reported body state; L0 builds no live `Body`, which the ticket's *Limits* states rather than papering over
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user approved the operation list's scope on 2026-10-11 from the census ("按普查全量开放"), which the ticket's *Plan* records together with the design points the ticket itself had already frozen (the code registration, the CUO-typed callback, the named operations, the engine-typed hatch and the affected-side execution)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; `dotnet format CasualtiesUnknownOnline.slnx` exit 0; the full suite gates 591/591 and behaviour 4922/4922 (was 574 and 4913 — +17 gate cases and +9 registration cases)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the largest touched class is `ModLifecycle` at 356 lines and the new ones are 21-231 (`ModEffectBody` is the biggest at 231), so nothing approaches the 600-line gate; the four `Alive`/`Conscious`/`Standing`/`InCardiacArrest` and the four limb classification members are READ-ONLY pass-throughs to the game body rather than state this type owns; and the mechanism the provider carried before — the pair of inline no-op delegates — is REPLACED by the invoker rather than left beside it, with its load-time declaration warning moved to the injection point that can actually see whether a function was registered
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
