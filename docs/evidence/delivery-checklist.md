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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this batch adds NO entry and no behaviour — it is the acceptance run of `mod-cross-player-solid-food-semantics`, and the entries it exercised were mapped by the cycle that landed them (the eat is the game's own `Body.UseItem` → `ItemInfo.useAction` against the eater's own body; the standing object is the offered item's local incarnation); every row it judged was declared in `docs/evidence/acceptance/20261011-i-scope.md` before the session, and no row needed an identity its gesture could not carry
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: no `src/`, `tests/` or runtime file changed this cycle; its only code is one read-only recipe template, `tools/acceptance/recipes/draw-at.cs`, and every reading in the record cites the product path it came from — `StandingItemMaterializer`'s own `[StandingItem] … carried row(s) in the data, … standing object(s) alive` line, `ItemUseSync`'s standing branch, `PlayerInteractionService`'s offer/eat lines, `Body.Eat` against `ItemInfo.useAction`, and the draw stack `draw-at` reports (`Chunk` `TilemapRenderer` at order 1000 over an unheld item's order 0); the mechanisms the ticket's own survey cites behind the rows (`Item.Update`'s chunk-renderer guard, `WaterContainerItem`'s per-frame drink, `LightItem` / `WatchScript` / `GeigerCounterAudio`) are unchanged by this batch, and what the run could NOT establish is named rather than glossed (why the guest's own body dropped its waterbottle)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is the standing-object set, and it was re-read through its own census one member at a time (`emergencylight`, `trashbag` with its nested `geigercounter`, `bread`, `waterbottle`, `flashlight`, `watch`): every renderer, collider, light, audio source and disabled component read per object, both light-carrying items' `Light2D`, the watch's `WatchScript` and the counter's `GeigerCounterAudio` named, the bottle's `WaterContainerItem` exercised with a damaged bottle over a seeded fluid cell, and the family's retire edges walked live — "the data no longer carries the row" (the waterbottle, end to end with the world row it became), "the owner left the world" (six objects) and session end ("retired 1 standing object(s)"); the two rows a frame could not carry were measured instead of assumed, and the measurement is in the ticket
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the batch record `docs/evidence/acceptance/mod-cross-player-solid-food-semantics-20261011-i.md` carries one verdict and one evidence pointer for each of the fifteen rows the scope page declared before the session, plus a Limits section naming what the run could not establish; the ticket's own mechanism × change × evidence table is unchanged except the one row this batch re-worded, and the re-wording carries the measurement that forces it (the parked spot is inside rock and draws under the terrain tilemap, so no frame can carry that half)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: `docs/evidence/acceptance/20261011-i-scope.md` declares the two-client setup, the shared session, the probes, the declared substitutions (a placed body, forced body and item writes, a control object moved onto the spot, the production request entry for the eat) and every row's class BEFORE the session, and the record names for every row which mechanism produced its evidence (a probe JSON, a window frame, or a log window read by byte offset from `m1-session-start.txt`); the one new committed tool is the recipe the run found missing — `tools/acceptance/recipes/draw-at.cs`, which answers whether a frame can carry a row at a world point at all — and the row it was written for says in the ticket that it is judged on the machine read
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the work order is the previous cycle's handoff (run the acceptance batch for `mod-cross-player-solid-food-semantics`, the next High and the ticket with no code left), the ticket's DESIGN was frozen by the user on 2026-10-08, and this batch re-asked no work-item choice; the only design-shaped decision it took is a row re-wording, which the ticket's own Limits had already named as that row's closing and which is written into the ticket and the record rather than left implicit
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` green (0 warnings, 0 errors); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit; `dotnet test CasualtiesUnknownOnline.slnx` green at 615/615 normative gates and 5051/5051 behaviour, taken after the boxes above were filled; the first run's two gate reds — a stale `todo/` reference left in the batch's own scope page and the new recipe's local named `result`, which the evaluator's submission scope already uses — were fixed rather than exempted, and no gate was loosened
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `git status --short -- src tests` is empty and no class behaviour changed anywhere; the cycle's files are two evidence pages, the ticket move with its index row and seven prose references, four lessons entries plus the local batch notes, this checklist, and one 63-line recipe template that holds no state (one expression, one scene scan, one reflection loop) and writes nothing to the scene
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
