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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no CUO entry and no native counterpart exist here: the cycle adds a discovery front-end for content declarations (an attribute, a scan and its refusals), adds no player-facing gesture and gives no action a fallback, so there is nothing to map; the one thing a player can see is the example mod declaring one item and one recipe by attribute (beside the item its `Bind` still registers by code), which is content rather than an input, and the ticket that owns it was frozen by the user on 2026-10-08
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the self-check §1 — nine rows, each naming the file or the case it rests on: the `[ModContent]` marker, `ModContentContract` (the one home of the interface-to-kind mapping), `ModContentDeclarationScanner` (census, ownership plan, refusals, member reads, registration), the `ModLifecycle` wiring (census once before the load loop, registration before `Bind`), `AssemblyTypes` (the loadable-types helper `ModRegistry` now shares), the example mod as the production consumer, the end-to-end fixture, the four documentation/alignment artifacts, and the re-reviewed baseline
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the self-check §2 — five measurements rather than assumptions: the scan is a front-end over the SAME `IModContent.TryRegister` (so the permission rail, the id/kind/schema rails, the cap, the duplicate rule, the binder and every consumer stage A converted are untouched, and the scan's member sweep is the one new read of a declaration), the ownership rule derived from the two shapes the tree actually has (`ModExample` declares two mods in one assembly; the test assembly declares dozens), the CS0535 measurement that killed the requested base-class shape (148 errors, one per unimplemented member), the refusal family checked against what the code path already refuses so the scan only adds the shapes it alone can see, and `ModNullCollectionRuleTests` re-run unchanged at its 26-row census
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the self-check §1 (nine rows: the marker, the kind mapping, the scanner, the lifecycle wiring, the shared loadable-types helper, the example consumer, the end-to-end fixture, the docs/alignment artifacts, the baseline), §2 (the five measurements), §3 (what landed against what deliberately did not, with the two supersessions and the two hand-offs), §4 (ten verification layers, each with its command, its filter conditions and its result), §5 (the independent review's seven findings and their dispositions) and §6 (seven limits)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the self-check §4 — no row needs a game process: the ownership plan and every refusal shape by `ModContentDeclarationScannerTests` with the log line asserted for each (the constructor that throws and the declaration owned by a mod discovery never loads included — both were found unguarded by the independent review and fixed in this commit), the SEAM end to end over the real `TestNode`/`ModService` stack (a mod that registers nothing in `Bind` still lands its classes in the registry, the catalog and the owner query, and its own bind already sees them), the throwing declaration driven through the REAL registry as well as through the stub, the four-representation pin (`ModContentContractTests` derives the constants and the contracts from the assembly and asserts the ready-made class agrees), and the contract SHAPE by `ApiSurfaceGateTests`, which is also the review step the modification policy names — it failed with exactly three ADDED entries and went green once the baseline was reviewed
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket's shape was settled in the user's own 2026-10-08 conversation and recorded in `review/mod-content-attribute-declarations.md` ("The shape (settled)"); the handoff orders this ticket's remaining half and states it needs no ruling. The two places this cycle departs from the ticket's own text are recorded with their measurements rather than re-asked — the multi-mod assembly rule (the example assembly declares two mods and the test assembly dozens, so the ambiguity is answered by refusing the declarations, not the mods) and the per-kind base classes (C# rejects the specified shape, measured at 148 CS0535 errors) — in the ticket's *Superseded and handed on* and decision 252, and both are reported to the user in this cycle's handoff
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build clean (0 warnings, 0 errors) on the frozen tree; `dotnet format CasualtiesUnknownOnline.slnx` exit 0, with every new C# file CRLF afterwards (measured byte-wise, not assumed); behaviour 4878/4878 (was 4854; the 24 new cases are this seam's); focused 153/153 over 15 filter conditions each written in full; gates 573/573 on the frozen tree — the mid-cycle reds were this checklist's own required boxes, the stale ticket path inside them, and the review report's own missing MANIFEST row, all three resolved before the commit — plus the baseline gate red with exactly three ADDED entries and green after the baseline was reviewed
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: every new type is small and single-purpose (`ModContentContract` 56 lines, `ModContentAttribute` 44, `AssemblyTypes` 30, `ModContentDeclarationScanner` 303 including its doc); the largest touched files are `ModLifecycle` 373 (was 349: the census call, the per-mod call and their doc) and `ModRegistry` 350 (was 365 — it SHRANK because its private `SafeGetTypes` moved into the shared helper), and the example mod is 277 (was 129: one declared item, one declared recipe and the console command) — all far under the gate's 600. No new state bool (the scanner's one field is a per-assembly plan cache). The round DELETED rather than added: `ModRegistry`'s duplicate type-loading policy is gone (both scans read `AssemblyTypes.Loadable`), and the nine base-class files this cycle first wrote are deleted with the compiler's own reason recorded in decision 252 — no shim was left beside them
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
